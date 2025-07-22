using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CPTHub.Data;
using CPTHub.Models;

namespace CPTHub.Services
{
    public interface IStudyService
    {
        Task InitializeContentAsync();
        Task<List<StudySection>> GetAllSectionsAsync();
        Task<List<StudyChapter>> GetChaptersBySectionAsync(int sectionId);
        Task<StudyChapter?> GetChapterAsync(int chapterId);
        Task<UserProgress> GetOrCreateUserProgressAsync(int userId, int chapterId);
        Task UpdateProgressAsync(int userId, int chapterId, double completionPercentage, int timeSpentMinutes);
        Task<List<StudyChapter>> GetRecommendedChaptersAsync(int userId);
        Task<StudySession> StartStudySessionAsync(int userId, int chapterId, SessionType sessionType);
        Task<StudySession> EndStudySessionAsync(int sessionId, bool completedSuccessfully, string? notes = null);
        Task<List<StudySession>> GetRecentStudySessionsAsync(int userId, int count = 10);
        Task<double> CalculateOverallProgressAsync(int userId);
    }

    public class StudyService : IStudyService
    {
        private readonly CPTHubDbContext _dbContext;
        private readonly ILogger<StudyService> _logger;

        public StudyService(CPTHubDbContext dbContext, ILogger<StudyService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task InitializeContentAsync()
        {
            try
            {
                // Check if content is already initialized
                var sectionCount = await _dbContext.StudySections.CountAsync();
                if (sectionCount > 0)
                {
                    _logger.LogDebug("Study content already initialized");
                    return;
                }

                _logger.LogInformation("Initializing NASM study content...");

                // Content is seeded through DbContext.OnModelCreating
                await _dbContext.Database.EnsureCreatedAsync();

                // Add sample quiz questions and flashcards for each chapter
                await InitializeSampleQuestionsAsync();
                await InitializeSampleFlashcardsAsync();

                _logger.LogInformation("Study content initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize study content");
                throw;
            }
        }

        public async Task<List<StudySection>> GetAllSectionsAsync()
        {
            return await _dbContext.StudySections
                .Include(s => s.Chapters)
                .OrderBy(s => s.OrderIndex)
                .ToListAsync();
        }

        public async Task<List<StudyChapter>> GetChaptersBySectionAsync(int sectionId)
        {
            return await _dbContext.StudyChapters
                .Where(c => c.SectionId == sectionId)
                .OrderBy(c => c.ChapterNumber)
                .ToListAsync();
        }

        public async Task<StudyChapter?> GetChapterAsync(int chapterId)
        {
            return await _dbContext.StudyChapters
                .Include(c => c.Section)
                .Include(c => c.Questions)
                .Include(c => c.Flashcards)
                .FirstOrDefaultAsync(c => c.Id == chapterId);
        }

        public async Task<UserProgress> GetOrCreateUserProgressAsync(int userId, int chapterId)
        {
            var progress = await _dbContext.UserProgress
                .FirstOrDefaultAsync(up => up.UserId == userId && up.ChapterId == chapterId);

            if (progress == null)
            {
                progress = new UserProgress
                {
                    UserId = userId,
                    ChapterId = chapterId,
                    Status = ProgressStatus.NotStarted,
                    CompletionPercentage = 0.0,
                    MasteryScore = 0.0,
                    LastAccessedAt = DateTime.UtcNow
                };

                _dbContext.UserProgress.Add(progress);
                await _dbContext.SaveChangesAsync();
            }

            return progress;
        }

        public async Task UpdateProgressAsync(int userId, int chapterId, double completionPercentage, int timeSpentMinutes)
        {
            var progress = await GetOrCreateUserProgressAsync(userId, chapterId);

            progress.CompletionPercentage = Math.Max(progress.CompletionPercentage, completionPercentage);
            progress.TimeSpentMinutes += timeSpentMinutes;
            progress.LastAccessedAt = DateTime.UtcNow;

            // Update status based on completion
            if (progress.CompletionPercentage >= 100)
            {
                progress.Status = ProgressStatus.Completed;
                if (progress.CompletedAt == null)
                {
                    progress.CompletedAt = DateTime.UtcNow;
                }
            }
            else if (progress.CompletionPercentage > 0)
            {
                progress.Status = ProgressStatus.InProgress;
                if (progress.StartedAt == null)
                {
                    progress.StartedAt = DateTime.UtcNow;
                }
            }

            // Calculate mastery score based on quiz performance
            var quizAccuracy = await CalculateChapterQuizAccuracyAsync(userId, chapterId);
            progress.MasteryScore = (progress.CompletionPercentage * 0.6) + (quizAccuracy * 0.4);

            // Determine if mastery is achieved
            if (progress.MasteryScore >= 85 && progress.CompletionPercentage >= 100)
            {
                progress.Status = ProgressStatus.Mastered;
            }
            else if (progress.MasteryScore < 60 && progress.CompletionPercentage >= 50)
            {
                progress.Status = ProgressStatus.NeedsReview;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogDebug("Updated progress for user {UserId}, chapter {ChapterId}: {Completion}% complete, {Mastery}% mastery", 
                userId, chapterId, completionPercentage, progress.MasteryScore);
        }

        public async Task<List<StudyChapter>> GetRecommendedChaptersAsync(int userId)
        {
            var userProgress = await _dbContext.UserProgress
                .Include(up => up.Chapter)
                .ThenInclude(c => c.Section)
                .Where(up => up.UserId == userId)
                .ToListAsync();

            var allChapters = await _dbContext.StudyChapters
                .Include(c => c.Section)
                .OrderBy(c => c.Section.OrderIndex)
                .ThenBy(c => c.ChapterNumber)
                .ToListAsync();

            var recommendations = new List<StudyChapter>();

            // Priority 1: Chapters that need review
            var needsReview = userProgress
                .Where(up => up.Status == ProgressStatus.NeedsReview)
                .Select(up => up.Chapter)
                .Take(2);
            recommendations.AddRange(needsReview);

            // Priority 2: Next unstarted chapter in sequence
            var nextChapter = allChapters
                .FirstOrDefault(c => !userProgress.Any(up => up.ChapterId == c.Id));
            if (nextChapter != null)
            {
                recommendations.Add(nextChapter);
            }

            // Priority 3: In-progress chapters
            var inProgress = userProgress
                .Where(up => up.Status == ProgressStatus.InProgress)
                .OrderByDescending(up => up.LastAccessedAt)
                .Select(up => up.Chapter)
                .Take(2);
            recommendations.AddRange(inProgress);

            return recommendations.Distinct().Take(5).ToList();
        }

        public async Task<StudySession> StartStudySessionAsync(int userId, int chapterId, SessionType sessionType)
        {
            var session = new StudySession
            {
                UserId = userId,
                ChapterId = chapterId,
                Type = sessionType,
                StartTime = DateTime.UtcNow
            };

            _dbContext.StudySessions.Add(session);
            await _dbContext.SaveChangesAsync();

            _logger.LogDebug("Started study session {SessionId} for user {UserId}, chapter {ChapterId}, type {SessionType}", 
                session.Id, userId, chapterId, sessionType);

            return session;
        }

        public async Task<StudySession> EndStudySessionAsync(int sessionId, bool completedSuccessfully, string? notes = null)
        {
            var session = await _dbContext.StudySessions.FindAsync(sessionId);
            if (session == null)
            {
                throw new ArgumentException($"Study session {sessionId} not found");
            }

            session.EndTime = DateTime.UtcNow;
            session.DurationMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;
            session.CompletedSuccessfully = completedSuccessfully;
            session.Notes = notes ?? string.Empty;

            // Calculate focus score based on session duration and activity
            session.FocusScore = CalculateFocusScore(session.DurationMinutes, session.QuestionsAnswered);

            await _dbContext.SaveChangesAsync();

            // Update chapter progress if this was a reading session
            if (session.Type == SessionType.Reading && session.ChapterId.HasValue && completedSuccessfully)
            {
                var progressIncrease = Math.Min(25.0, session.DurationMinutes * 0.5); // Up to 25% per session
                await UpdateProgressAsync(session.UserId, session.ChapterId.Value, progressIncrease, session.DurationMinutes);
            }

            _logger.LogDebug("Ended study session {SessionId}: {Duration} minutes, completed: {Completed}", 
                sessionId, session.DurationMinutes, completedSuccessfully);

            return session;
        }

        public async Task<List<StudySession>> GetRecentStudySessionsAsync(int userId, int count = 10)
        {
            return await _dbContext.StudySessions
                .Include(s => s.Chapter)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartTime)
                .Take(count)
                .ToListAsync();
        }

        public async Task<double> CalculateOverallProgressAsync(int userId)
        {
            var allProgress = await _dbContext.UserProgress
                .Where(up => up.UserId == userId)
                .ToListAsync();

            var totalChapters = await _dbContext.StudyChapters.CountAsync();

            if (totalChapters == 0) return 0.0;

            var completedChapters = allProgress.Count(p => p.Status == ProgressStatus.Completed || p.Status == ProgressStatus.Mastered);
            var partialProgress = allProgress.Where(p => p.Status == ProgressStatus.InProgress).Sum(p => p.CompletionPercentage / 100.0);

            return ((completedChapters + partialProgress) / totalChapters) * 100.0;
        }

        private async Task<double> CalculateChapterQuizAccuracyAsync(int userId, int chapterId)
        {
            var quizAttempts = await _dbContext.QuizAttempts
                .Include(qa => qa.Question)
                .Where(qa => qa.UserId == userId && qa.Question.ChapterId == chapterId)
                .ToListAsync();

            if (!quizAttempts.Any()) return 0.0;

            return quizAttempts.Average(qa => qa.IsCorrect ? 100.0 : 0.0);
        }

        private double CalculateFocusScore(int durationMinutes, int questionsAnswered)
        {
            // Simple focus score calculation
            // Higher score for longer sessions with consistent activity
            var baseScore = Math.Min(100, durationMinutes * 2); // Up to 50 minutes = 100 points
            var activityBonus = questionsAnswered * 5; // 5 points per question
            var totalScore = Math.Min(100, baseScore + activityBonus);

            return totalScore;
        }

        private async Task InitializeSampleQuestionsAsync()
        {
            // Add a few sample questions for each chapter
            var chapters = await _dbContext.StudyChapters.ToListAsync();

            foreach (var chapter in chapters.Take(5)) // Initialize first 5 chapters with sample questions
            {
                var sampleQuestions = GenerateSampleQuestions(chapter);
                _dbContext.QuizQuestions.AddRange(sampleQuestions);
            }

            await _dbContext.SaveChangesAsync();
        }

        private async Task InitializeSampleFlashcardsAsync()
        {
            // Add a few sample flashcards for each chapter
            var chapters = await _dbContext.StudyChapters.ToListAsync();

            foreach (var chapter in chapters.Take(5)) // Initialize first 5 chapters with sample flashcards
            {
                var sampleFlashcards = GenerateSampleFlashcards(chapter);
                _dbContext.Flashcards.AddRange(sampleFlashcards);
            }

            await _dbContext.SaveChangesAsync();
        }

        private List<QuizQuestion> GenerateSampleQuestions(StudyChapter chapter)
        {
            // Generate sample questions based on chapter content
            return chapter.ChapterNumber switch
            {
                1 => new List<QuizQuestion>
                {
                    new QuizQuestion
                    {
                        Question = "What percentage of adults in the US are considered physically inactive?",
                        CorrectAnswer = "Approximately 25%",
                        IncorrectAnswer1 = "Approximately 10%",
                        IncorrectAnswer2 = "Approximately 40%",
                        IncorrectAnswer3 = "Approximately 60%",
                        Explanation = "According to NASM, approximately 25% of US adults are considered physically inactive, contributing to various health issues.",
                        ChapterId = chapter.Id,
                        Difficulty = DifficultyLevel.Medium,
                        Type = QuestionType.MultipleChoice
                    }
                },
                2 => new List<QuizQuestion>
                {
                    new QuizQuestion
                    {
                        Question = "What is the primary scope of practice for a NASM Certified Personal Trainer?",
                        CorrectAnswer = "Designing and implementing exercise programs for healthy individuals",
                        IncorrectAnswer1 = "Diagnosing injuries and prescribing rehabilitation",
                        IncorrectAnswer2 = "Providing nutritional counseling and meal plans",
                        IncorrectAnswer3 = "Treating medical conditions through exercise",
                        Explanation = "NASM CPTs are qualified to design and implement exercise programs for healthy individuals, but cannot diagnose, treat, or prescribe.",
                        ChapterId = chapter.Id,
                        Difficulty = DifficultyLevel.Medium,
                        Type = QuestionType.MultipleChoice
                    }
                },
                _ => new List<QuizQuestion>()
            };
        }

        private List<Flashcard> GenerateSampleFlashcards(StudyChapter chapter)
        {
            // Generate sample flashcards based on chapter content
            return chapter.ChapterNumber switch
            {
                1 => new List<Flashcard>
                {
                    new Flashcard
                    {
                        Front = "What are the leading causes of death in the United States?",
                        Back = "Heart disease, cancer, and stroke are the top three leading causes of death, many of which are preventable through proper exercise and nutrition.",
                        ChapterId = chapter.Id,
                        Difficulty = DifficultyLevel.Easy
                    }
                },
                2 => new List<Flashcard>
                {
                    new Flashcard
                    {
                        Front = "What does 'scope of practice' mean for personal trainers?",
                        Back = "Scope of practice defines the legal and professional boundaries within which a personal trainer can operate, including what services they can and cannot provide.",
                        ChapterId = chapter.Id,
                        Difficulty = DifficultyLevel.Medium
                    }
                },
                _ => new List<Flashcard>()
            };
        }
    }
}
