using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CPTHub.Models;
using CPTHub.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Polly;

namespace CPTHub.Services
{
    public class AIServiceWithFallback : IAIService
    {
        private readonly ILearnLMService _learnLMService;
        private readonly ICacheService _cacheService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly CPTHubDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIServiceWithFallback> _logger;
        private readonly IAsyncPolicy<string> _retryPolicy;

        public AIServiceWithFallback(
            ILearnLMService learnLMService,
            ICacheService cacheService,
            IErrorHandlingService errorHandlingService,
            CPTHubDbContext dbContext,
            IConfiguration configuration,
            ILogger<AIServiceWithFallback> logger)
        {
            _learnLMService = learnLMService;
            _cacheService = cacheService;
            _errorHandlingService = errorHandlingService;
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;

            _retryPolicy = Policy
                .Handle<AIServiceException>(ex => ex.IsRetryable)
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: _configuration.GetValue<int>("GoogleLearnLM:MaxRetries", 3),
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount} for {Operation} in {Delay}ms", 
                            retryCount, context.OperationKey, timespan.TotalMilliseconds);
                    });
        }

        public async Task<string> GetStudyAssistanceAsync(string question, string context, int chapterId)
        {
            var cacheKey = $"study_assistance_{chapterId}_{question.GetHashCode()}";
            
            return await _errorHandlingService.ExecuteWithFallbackAsync(
                primaryAction: async () =>
                {
                    // Check cache first
                    var cachedResponse = await _cacheService.GetAsync<string>(cacheKey);
                    if (cachedResponse != null)
                    {
                        _logger.LogDebug("Returning cached study assistance for chapter {ChapterId}", chapterId);
                        return cachedResponse;
                    }

                    // Get chapter context
                    var chapter = await _dbContext.StudyChapters
                        .Include(c => c.Section)
                        .FirstOrDefaultAsync(c => c.Id == chapterId);

                    var fullContext = $"Chapter: {chapter?.Title}\nSection: {chapter?.Section?.Title}\nContext: {context}";
                    
                    var response = await _retryPolicy.ExecuteAsync(async () =>
                        await _learnLMService.GenerateContentAsync(question, ContentType.StudyExplanation));

                    // Validate response
                    if (await ValidateAIResponseAsync(response, chapter?.Title ?? ""))
                    {
                        await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromHours(24));
                        return response;
                    }

                    throw new AIServiceException("AI response validation failed", "GetStudyAssistance", false);
                },
                fallbackAction: async () =>
                {
                    _logger.LogWarning("Using fallback for study assistance - chapter {ChapterId}", chapterId);
                    return await GetFallbackStudyAssistanceAsync(question, chapterId);
                },
                operationName: "GetStudyAssistance"
            );
        }

        public async Task<List<QuizQuestion>> GenerateQuizQuestionsAsync(int chapterId, int count, DifficultyLevel difficulty)
        {
            var cacheKey = $"quiz_questions_{chapterId}_{count}_{difficulty}";
            
            return await _errorHandlingService.ExecuteWithFallbackAsync(
                primaryAction: async () =>
                {
                    var cachedQuestions = await _cacheService.GetAsync<List<QuizQuestion>>(cacheKey);
                    if (cachedQuestions != null && cachedQuestions.Count >= count)
                    {
                        return cachedQuestions.Take(count).ToList();
                    }

                    var chapter = await _dbContext.StudyChapters
                        .Include(c => c.Section)
                        .FirstOrDefaultAsync(c => c.Id == chapterId);

                    if (chapter == null)
                        throw new ArgumentException($"Chapter {chapterId} not found");

                    var questions = new List<QuizQuestion>();
                    var prompt = $"Generate {count} NASM CPT exam questions about {chapter.Title}. Difficulty: {difficulty}. Include key concepts: {chapter.KeyConcepts}";

                    for (int i = 0; i < count; i++)
                    {
                        var response = await _retryPolicy.ExecuteAsync(async () =>
                            await _learnLMService.GenerateContentAsync(prompt, ContentType.QuizQuestion));

                        var question = ParseQuizQuestionFromResponse(response, chapterId, difficulty);
                        if (question != null)
                        {
                            questions.Add(question);
                        }
                    }

                    if (questions.Any())
                    {
                        await _cacheService.SetAsync(cacheKey, questions, TimeSpan.FromHours(12));
                    }

                    return questions;
                },
                fallbackAction: async () =>
                {
                    _logger.LogWarning("Using fallback for quiz generation - chapter {ChapterId}", chapterId);
                    return await GetFallbackQuizQuestionsAsync(chapterId, count, difficulty);
                },
                operationName: "GenerateQuizQuestions"
            );
        }

        public async Task<List<Flashcard>> GenerateFlashcardsAsync(int chapterId, int count)
        {
            var cacheKey = $"flashcards_{chapterId}_{count}";
            
            return await _errorHandlingService.ExecuteWithFallbackAsync(
                primaryAction: async () =>
                {
                    var cachedFlashcards = await _cacheService.GetAsync<List<Flashcard>>(cacheKey);
                    if (cachedFlashcards != null && cachedFlashcards.Count >= count)
                    {
                        return cachedFlashcards.Take(count).ToList();
                    }

                    var chapter = await _dbContext.StudyChapters
                        .FirstOrDefaultAsync(c => c.Id == chapterId);

                    if (chapter == null)
                        throw new ArgumentException($"Chapter {chapterId} not found");

                    var flashcards = new List<Flashcard>();
                    var prompt = $"Create {count} flashcards for NASM CPT study about {chapter.Title}. Focus on key concepts: {chapter.KeyConcepts}";

                    for (int i = 0; i < count; i++)
                    {
                        var response = await _retryPolicy.ExecuteAsync(async () =>
                            await _learnLMService.GenerateContentAsync(prompt, ContentType.Flashcard));

                        var flashcard = ParseFlashcardFromResponse(response, chapterId);
                        if (flashcard != null)
                        {
                            flashcards.Add(flashcard);
                        }
                    }

                    if (flashcards.Any())
                    {
                        await _cacheService.SetAsync(cacheKey, flashcards, TimeSpan.FromHours(12));
                    }

                    return flashcards;
                },
                fallbackAction: async () =>
                {
                    _logger.LogWarning("Using fallback for flashcard generation - chapter {ChapterId}", chapterId);
                    return await GetFallbackFlashcardsAsync(chapterId, count);
                },
                operationName: "GenerateFlashcards"
            );
        }

        public async Task<string> ExplainIncorrectAnswerAsync(QuizQuestion question, string userAnswer)
        {
            var cacheKey = $"explanation_{question.Id}_{userAnswer.GetHashCode()}";
            
            return await _errorHandlingService.ExecuteWithFallbackAsync(
                primaryAction: async () =>
                {
                    var cachedExplanation = await _cacheService.GetAsync<string>(cacheKey);
                    if (cachedExplanation != null)
                    {
                        return cachedExplanation;
                    }

                    var prompt = $"Explain why '{userAnswer}' is incorrect for this NASM question: '{question.Question}'. The correct answer is '{question.CorrectAnswer}'. Provide educational explanation focusing on NASM principles.";
                    
                    var response = await _retryPolicy.ExecuteAsync(async () =>
                        await _learnLMService.SendPromptAsync(prompt));

                    await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromHours(24));
                    return response;
                },
                fallbackAction: async () =>
                {
                    return $"The correct answer is '{question.CorrectAnswer}'. {question.Explanation}";
                },
                operationName: "ExplainIncorrectAnswer"
            );
        }

        public async Task<string> GetPersonalizedRecommendationAsync(int userId, string currentContext)
        {
            return await _errorHandlingService.ExecuteWithFallbackAsync(
                primaryAction: async () =>
                {
                    var userProgress = await GetUserProgressSummaryAsync(userId);
                    var prompt = $"Based on this NASM CPT study progress: {userProgress}, and current context: {currentContext}, provide personalized study recommendations.";
                    
                    return await _retryPolicy.ExecuteAsync(async () =>
                        await _learnLMService.GenerateContentAsync(prompt, ContentType.Recommendation));
                },
                fallbackAction: async () =>
                {
                    return await GetFallbackRecommendationAsync(userId);
                },
                operationName: "GetPersonalizedRecommendation"
            );
        }

        public async Task<double> PredictExamReadinessAsync(int userId)
        {
            return await _errorHandlingService.ExecuteWithFallbackAsync(
                primaryAction: async () =>
                {
                    var progressData = await GetDetailedProgressDataAsync(userId);
                    var prompt = $"Analyze this NASM CPT study data and predict exam readiness as a percentage (0-100): {progressData}";
                    
                    var response = await _retryPolicy.ExecuteAsync(async () =>
                        await _learnLMService.GenerateContentAsync(prompt, ContentType.ExamPrediction));

                    // Extract percentage from response
                    if (double.TryParse(ExtractPercentageFromResponse(response), out double percentage))
                    {
                        return Math.Max(0, Math.Min(100, percentage));
                    }

                    throw new AIServiceException("Could not parse exam readiness percentage", "PredictExamReadiness", false);
                },
                fallbackAction: async () =>
                {
                    return await CalculateFallbackExamReadinessAsync(userId);
                },
                operationName: "PredictExamReadiness"
            );
        }

        public async Task<bool> ValidateAIResponseAsync(string response, string topic)
        {
            if (string.IsNullOrWhiteSpace(response))
                return false;

            // Basic validation checks
            var validationChecks = new[]
            {
                !response.Contains("I don't know"),
                !response.Contains("I cannot"),
                response.Length > 50,
                response.Length < 5000,
                !ContainsInappropriateContent(response)
            };

            return validationChecks.All(check => check);
        }

        public async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                return await _learnLMService.CheckServiceAvailabilityAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return false;
            }
        }

        // Fallback methods
        private async Task<string> GetFallbackStudyAssistanceAsync(string question, int chapterId)
        {
            var chapter = await _dbContext.StudyChapters
                .Include(c => c.Section)
                .FirstOrDefaultAsync(c => c.Id == chapterId);

            return $"Based on NASM Chapter {chapter?.ChapterNumber}: {chapter?.Title}\n\n" +
                   $"Key Concepts: {chapter?.KeyConcepts}\n\n" +
                   $"Learning Objectives: {chapter?.LearningObjectives}\n\n" +
                   $"For detailed information about your question '{question}', please refer to the NASM CPT textbook chapter {chapter?.ChapterNumber}.";
        }

        private async Task<List<QuizQuestion>> GetFallbackQuizQuestionsAsync(int chapterId, int count, DifficultyLevel difficulty)
        {
            return await _dbContext.QuizQuestions
                .Where(q => q.ChapterId == chapterId && q.Difficulty == difficulty && !q.IsAIGenerated)
                .OrderBy(q => Guid.NewGuid())
                .Take(count)
                .ToListAsync();
        }

        private async Task<List<Flashcard>> GetFallbackFlashcardsAsync(int chapterId, int count)
        {
            return await _dbContext.Flashcards
                .Where(f => f.ChapterId == chapterId && !f.IsAIGenerated)
                .OrderBy(f => Guid.NewGuid())
                .Take(count)
                .ToListAsync();
        }

        private async Task<string> GetFallbackRecommendationAsync(int userId)
        {
            var weakAreas = await _dbContext.UserProgress
                .Where(up => up.UserId == userId && up.MasteryScore < 70)
                .Include(up => up.Chapter)
                .OrderBy(up => up.MasteryScore)
                .Take(3)
                .Select(up => up.Chapter.Title)
                .ToListAsync();

            if (weakAreas.Any())
            {
                return $"Based on your progress, focus on these areas that need improvement: {string.Join(", ", weakAreas)}. " +
                       "Consider reviewing the key concepts and taking additional practice quizzes in these chapters.";
            }

            return "Continue your systematic study through all NASM chapters. Focus on understanding the OPT model and practical applications.";
        }

        private async Task<double> CalculateFallbackExamReadinessAsync(int userId)
        {
            var progress = await _dbContext.UserProgress
                .Where(up => up.UserId == userId)
                .ToListAsync();

            if (!progress.Any())
                return 0.0;

            var averageCompletion = progress.Average(p => p.CompletionPercentage);
            var averageMastery = progress.Average(p => p.MasteryScore);
            
            return (averageCompletion * 0.4 + averageMastery * 0.6);
        }

        // Helper methods
        private QuizQuestion? ParseQuizQuestionFromResponse(string response, int chapterId, DifficultyLevel difficulty)
        {
            try
            {
                var questionData = JsonConvert.DeserializeObject<dynamic>(response);
                
                return new QuizQuestion
                {
                    Question = questionData?.question?.ToString() ?? "",
                    CorrectAnswer = questionData?.correctAnswer?.ToString() ?? "",
                    IncorrectAnswer1 = questionData?.options?[1]?.ToString() ?? "",
                    IncorrectAnswer2 = questionData?.options?[2]?.ToString() ?? "",
                    IncorrectAnswer3 = questionData?.options?[3]?.ToString() ?? "",
                    Explanation = questionData?.explanation?.ToString() ?? "",
                    ChapterId = chapterId,
                    Difficulty = difficulty,
                    IsAIGenerated = true,
                    Type = QuestionType.MultipleChoice
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse quiz question from AI response");
                return null;
            }
        }

        private Flashcard? ParseFlashcardFromResponse(string response, int chapterId)
        {
            try
            {
                var flashcardData = JsonConvert.DeserializeObject<dynamic>(response);
                
                return new Flashcard
                {
                    Front = flashcardData?.front?.ToString() ?? "",
                    Back = flashcardData?.back?.ToString() ?? "",
                    ChapterId = chapterId,
                    IsAIGenerated = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse flashcard from AI response");
                return null;
            }
        }

        private async Task<string> GetUserProgressSummaryAsync(int userId)
        {
            var progress = await _dbContext.UserProgress
                .Include(up => up.Chapter)
                .Where(up => up.UserId == userId)
                .ToListAsync();

            var summary = new
            {
                TotalChapters = progress.Count,
                CompletedChapters = progress.Count(p => p.Status == ProgressStatus.Completed),
                AverageCompletion = progress.Average(p => p.CompletionPercentage),
                AverageMastery = progress.Average(p => p.MasteryScore),
                WeakAreas = progress.Where(p => p.MasteryScore < 70).Select(p => p.Chapter.Title).ToList()
            };

            return JsonConvert.SerializeObject(summary);
        }

        private async Task<string> GetDetailedProgressDataAsync(int userId)
        {
            var sessions = await _dbContext.StudySessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartTime)
                .Take(20)
                .ToListAsync();

            var quizAttempts = await _dbContext.QuizAttempts
                .Where(qa => qa.UserId == userId)
                .OrderByDescending(qa => qa.AttemptedAt)
                .Take(50)
                .ToListAsync();

            var data = new
            {
                TotalStudyTime = sessions.Sum(s => s.DurationMinutes),
                RecentSessions = sessions.Count,
                QuizAccuracy = quizAttempts.Any() ? quizAttempts.Average(qa => qa.IsCorrect ? 1.0 : 0.0) * 100 : 0,
                AverageResponseTime = quizAttempts.Any() ? quizAttempts.Average(qa => qa.ResponseTimeSeconds) : 0
            };

            return JsonConvert.SerializeObject(data);
        }

        private string ExtractPercentageFromResponse(string response)
        {
            var match = System.Text.RegularExpressions.Regex.Match(response, @"(\d+(?:\.\d+)?)%?");
            return match.Success ? match.Groups[1].Value : "0";
        }

        private bool ContainsInappropriateContent(string response)
        {
            var inappropriateTerms = new[] { "harmful", "dangerous", "illegal", "inappropriate" };
            return inappropriateTerms.Any(term => response.ToLower().Contains(term));
        }
    }
}
