using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CPTHub.Data;
using CPTHub.Models;

namespace CPTHub.Services
{
    public interface IQuizService
    {
        Task<List<QuizQuestion>> GetQuizQuestionsAsync(int chapterId, int count, DifficultyLevel? difficulty = null);
        Task<QuizQuestion?> GetQuestionAsync(int questionId);
        Task<QuizAttempt> SubmitAnswerAsync(int userId, int questionId, string userAnswer, int responseTimeSeconds);
        Task<List<QuizAttempt>> GetQuizHistoryAsync(int userId, int chapterId);
        Task<double> CalculateQuizAccuracyAsync(int userId, int chapterId);
        Task<List<QuizQuestion>> GenerateAdaptiveQuizAsync(int userId, int chapterId, int count);
        Task<QuizAttempt> GetQuizAttemptAsync(int attemptId);
        Task<List<QuizQuestion>> GetIncorrectQuestionsAsync(int userId, int chapterId);
        Task<Dictionary<DifficultyLevel, double>> GetAccuracyByDifficultyAsync(int userId);
    }

    public class QuizService : IQuizService
    {
        private readonly CPTHubDbContext _dbContext;
        private readonly IAIService _aiService;
        private readonly ILogger<QuizService> _logger;

        public QuizService(CPTHubDbContext dbContext, IAIService aiService, ILogger<QuizService> logger)
        {
            _dbContext = dbContext;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<List<QuizQuestion>> GetQuizQuestionsAsync(int chapterId, int count, DifficultyLevel? difficulty = null)
        {
            try
            {
                var query = _dbContext.QuizQuestions
                    .Where(q => q.ChapterId == chapterId);

                if (difficulty.HasValue)
                {
                    query = query.Where(q => q.Difficulty == difficulty.Value);
                }

                var existingQuestions = await query
                    .OrderBy(q => Guid.NewGuid()) // Random order
                    .Take(count)
                    .ToListAsync();

                // If we don't have enough questions, try to generate more with AI
                if (existingQuestions.Count < count)
                {
                    var needed = count - existingQuestions.Count;
                    var targetDifficulty = difficulty ?? DifficultyLevel.Medium;

                    try
                    {
                        var aiQuestions = await _aiService.GenerateQuizQuestionsAsync(chapterId, needed, targetDifficulty);
                        
                        // Save AI-generated questions to database
                        if (aiQuestions.Any())
                        {
                            _dbContext.QuizQuestions.AddRange(aiQuestions);
                            await _dbContext.SaveChangesAsync();
                            existingQuestions.AddRange(aiQuestions);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate AI questions for chapter {ChapterId}", chapterId);
                    }
                }

                _logger.LogDebug("Retrieved {Count} quiz questions for chapter {ChapterId}", existingQuestions.Count, chapterId);
                return existingQuestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get quiz questions for chapter {ChapterId}", chapterId);
                throw;
            }
        }

        public async Task<QuizQuestion?> GetQuestionAsync(int questionId)
        {
            return await _dbContext.QuizQuestions
                .Include(q => q.Chapter)
                .FirstOrDefaultAsync(q => q.Id == questionId);
        }

        public async Task<QuizAttempt> SubmitAnswerAsync(int userId, int questionId, string userAnswer, int responseTimeSeconds)
        {
            try
            {
                var question = await GetQuestionAsync(questionId);
                if (question == null)
                {
                    throw new ArgumentException($"Question {questionId} not found");
                }

                var isCorrect = string.Equals(userAnswer.Trim(), question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);

                var attempt = new QuizAttempt
                {
                    UserId = userId,
                    QuestionId = questionId,
                    UserAnswer = userAnswer,
                    IsCorrect = isCorrect,
                    ResponseTimeSeconds = responseTimeSeconds,
                    AttemptedAt = DateTime.UtcNow
                };

                // Generate AI explanation for incorrect answers
                if (!isCorrect)
                {
                    try
                    {
                        attempt.AIExplanation = await _aiService.ExplainIncorrectAnswerAsync(question, userAnswer);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate AI explanation for incorrect answer");
                        attempt.AIExplanation = question.Explanation;
                    }
                }

                _dbContext.QuizAttempts.Add(attempt);
                await _dbContext.SaveChangesAsync();

                // Update user progress based on quiz performance
                await UpdateProgressFromQuizAsync(userId, question.ChapterId, isCorrect);

                _logger.LogDebug("Quiz attempt submitted: User {UserId}, Question {QuestionId}, Correct: {IsCorrect}", 
                    userId, questionId, isCorrect);

                return attempt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit quiz answer");
                throw;
            }
        }

        public async Task<List<QuizAttempt>> GetQuizHistoryAsync(int userId, int chapterId)
        {
            return await _dbContext.QuizAttempts
                .Include(qa => qa.Question)
                .Where(qa => qa.UserId == userId && qa.Question.ChapterId == chapterId)
                .OrderByDescending(qa => qa.AttemptedAt)
                .ToListAsync();
        }

        public async Task<double> CalculateQuizAccuracyAsync(int userId, int chapterId)
        {
            var attempts = await _dbContext.QuizAttempts
                .Include(qa => qa.Question)
                .Where(qa => qa.UserId == userId && qa.Question.ChapterId == chapterId)
                .ToListAsync();

            if (!attempts.Any()) return 0.0;

            return attempts.Average(a => a.IsCorrect ? 100.0 : 0.0);
        }

        public async Task<List<QuizQuestion>> GenerateAdaptiveQuizAsync(int userId, int chapterId, int count)
        {
            try
            {
                // Analyze user's performance to determine appropriate difficulty
                var userAccuracy = await CalculateQuizAccuracyAsync(userId, chapterId);
                var targetDifficulty = DetermineAdaptiveDifficulty(userAccuracy);

                // Get questions that the user hasn't answered correctly recently
                var recentCorrectQuestionIds = await _dbContext.QuizAttempts
                    .Include(qa => qa.Question)
                    .Where(qa => qa.UserId == userId && 
                                qa.Question.ChapterId == chapterId && 
                                qa.IsCorrect && 
                                qa.AttemptedAt > DateTime.UtcNow.AddDays(-7))
                    .Select(qa => qa.QuestionId)
                    .ToListAsync();

                var adaptiveQuestions = await _dbContext.QuizQuestions
                    .Where(q => q.ChapterId == chapterId && 
                               q.Difficulty == targetDifficulty &&
                               !recentCorrectQuestionIds.Contains(q.Id))
                    .OrderBy(q => Guid.NewGuid())
                    .Take(count)
                    .ToListAsync();

                // If we don't have enough questions, fill with AI-generated ones
                if (adaptiveQuestions.Count < count)
                {
                    var needed = count - adaptiveQuestions.Count;
                    var aiQuestions = await _aiService.GenerateQuizQuestionsAsync(chapterId, needed, targetDifficulty);
                    
                    if (aiQuestions.Any())
                    {
                        _dbContext.QuizQuestions.AddRange(aiQuestions);
                        await _dbContext.SaveChangesAsync();
                        adaptiveQuestions.AddRange(aiQuestions);
                    }
                }

                _logger.LogDebug("Generated adaptive quiz for user {UserId}, chapter {ChapterId}: {Count} questions at {Difficulty} difficulty", 
                    userId, chapterId, adaptiveQuestions.Count, targetDifficulty);

                return adaptiveQuestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate adaptive quiz");
                // Fallback to regular quiz
                return await GetQuizQuestionsAsync(chapterId, count);
            }
        }

        public async Task<QuizAttempt> GetQuizAttemptAsync(int attemptId)
        {
            var attempt = await _dbContext.QuizAttempts
                .Include(qa => qa.Question)
                .ThenInclude(q => q.Chapter)
                .FirstOrDefaultAsync(qa => qa.Id == attemptId);

            if (attempt == null)
            {
                throw new ArgumentException($"Quiz attempt {attemptId} not found");
            }

            return attempt;
        }

        public async Task<List<QuizQuestion>> GetIncorrectQuestionsAsync(int userId, int chapterId)
        {
            var incorrectQuestionIds = await _dbContext.QuizAttempts
                .Include(qa => qa.Question)
                .Where(qa => qa.UserId == userId && 
                            qa.Question.ChapterId == chapterId && 
                            !qa.IsCorrect)
                .GroupBy(qa => qa.QuestionId)
                .Where(g => !g.Any(qa => qa.IsCorrect)) // Never answered correctly
                .Select(g => g.Key)
                .ToListAsync();

            return await _dbContext.QuizQuestions
                .Where(q => incorrectQuestionIds.Contains(q.Id))
                .ToListAsync();
        }

        public async Task<Dictionary<DifficultyLevel, double>> GetAccuracyByDifficultyAsync(int userId)
        {
            var attempts = await _dbContext.QuizAttempts
                .Include(qa => qa.Question)
                .Where(qa => qa.UserId == userId)
                .ToListAsync();

            var accuracyByDifficulty = new Dictionary<DifficultyLevel, double>();

            foreach (DifficultyLevel difficulty in Enum.GetValues<DifficultyLevel>())
            {
                var difficultyAttempts = attempts.Where(a => a.Question.Difficulty == difficulty).ToList();
                
                if (difficultyAttempts.Any())
                {
                    accuracyByDifficulty[difficulty] = difficultyAttempts.Average(a => a.IsCorrect ? 100.0 : 0.0);
                }
                else
                {
                    accuracyByDifficulty[difficulty] = 0.0;
                }
            }

            return accuracyByDifficulty;
        }

        private DifficultyLevel DetermineAdaptiveDifficulty(double userAccuracy)
        {
            return userAccuracy switch
            {
                >= 90 => DifficultyLevel.Expert,
                >= 80 => DifficultyLevel.Hard,
                >= 70 => DifficultyLevel.Medium,
                _ => DifficultyLevel.Easy
            };
        }

        private async Task UpdateProgressFromQuizAsync(int userId, int chapterId, bool isCorrect)
        {
            try
            {
                var progress = await _dbContext.UserProgress
                    .FirstOrDefaultAsync(up => up.UserId == userId && up.ChapterId == chapterId);

                if (progress != null)
                {
                    // Recalculate mastery score based on recent quiz performance
                    var recentAccuracy = await CalculateQuizAccuracyAsync(userId, chapterId);
                    var newMasteryScore = (progress.CompletionPercentage * 0.6) + (recentAccuracy * 0.4);
                    
                    progress.MasteryScore = newMasteryScore;
                    progress.LastAccessedAt = DateTime.UtcNow;

                    // Update status based on performance
                    if (newMasteryScore >= 85 && progress.CompletionPercentage >= 100)
                    {
                        progress.Status = ProgressStatus.Mastered;
                    }
                    else if (newMasteryScore < 60)
                    {
                        progress.Status = ProgressStatus.NeedsReview;
                        progress.NextReviewDue = DateTime.UtcNow.AddDays(1);
                    }

                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update progress from quiz result");
            }
        }
    }
}
