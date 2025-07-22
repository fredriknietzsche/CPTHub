using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CPTHub.Data;
using CPTHub.Models;

namespace CPTHub.Services
{
    public interface IAnalyticsService
    {
        Task<UserAnalytics> GetUserAnalyticsAsync(int userId);
        Task<StudyAnalytics> GetStudyAnalyticsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<ChapterPerformance>> GetChapterPerformanceAsync(int userId);
        Task<List<StudyStreak>> GetStudyStreaksAsync(int userId);
        Task<ExamReadinessReport> GenerateExamReadinessReportAsync(int userId);
        Task<List<WeakArea>> IdentifyWeakAreasAsync(int userId);
        Task<StudyRecommendation> GetPersonalizedRecommendationsAsync(int userId);
        Task<Dictionary<DateTime, int>> GetStudyHeatmapDataAsync(int userId, int days = 90);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly CPTHubDbContext _dbContext;
        private readonly IAIService _aiService;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(CPTHubDbContext dbContext, IAIService aiService, ILogger<AnalyticsService> logger)
        {
            _dbContext = dbContext;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<UserAnalytics> GetUserAnalyticsAsync(int userId)
        {
            try
            {
                var user = await _dbContext.UserProfiles.FindAsync(userId);
                if (user == null)
                {
                    throw new ArgumentException($"User {userId} not found");
                }

                var totalStudyTime = await _dbContext.StudySessions
                    .Where(s => s.UserId == userId)
                    .SumAsync(s => s.DurationMinutes);

                var totalQuizAttempts = await _dbContext.QuizAttempts
                    .CountAsync(qa => qa.UserId == userId);

                var correctAnswers = await _dbContext.QuizAttempts
                    .CountAsync(qa => qa.UserId == userId && qa.IsCorrect);

                var overallAccuracy = totalQuizAttempts > 0 ? (double)correctAnswers / totalQuizAttempts * 100 : 0;

                var chaptersStarted = await _dbContext.UserProgress
                    .CountAsync(up => up.UserId == userId && up.Status != ProgressStatus.NotStarted);

                var chaptersCompleted = await _dbContext.UserProgress
                    .CountAsync(up => up.UserId == userId && 
                               (up.Status == ProgressStatus.Completed || up.Status == ProgressStatus.Mastered));

                var totalChapters = await _dbContext.StudyChapters.CountAsync();

                var flashcardsReviewed = await _dbContext.FlashcardReviews
                    .CountAsync(fr => fr.UserId == userId);

                var currentStreak = await CalculateCurrentStudyStreakAsync(userId);

                return new UserAnalytics
                {
                    UserId = userId,
                    TotalStudyTimeMinutes = totalStudyTime,
                    TotalQuizAttempts = totalQuizAttempts,
                    OverallQuizAccuracy = overallAccuracy,
                    ChaptersStarted = chaptersStarted,
                    ChaptersCompleted = chaptersCompleted,
                    TotalChapters = totalChapters,
                    FlashcardsReviewed = flashcardsReviewed,
                    CurrentStudyStreak = currentStreak,
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user analytics for user {UserId}", userId);
                throw;
            }
        }

        public async Task<StudyAnalytics> GetStudyAnalyticsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var sessions = await _dbContext.StudySessions
                    .Where(s => s.UserId == userId && s.StartTime >= start && s.StartTime <= end)
                    .ToListAsync();

                var dailyStudyTime = sessions
                    .GroupBy(s => s.StartTime.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationMinutes));

                var sessionsByType = sessions
                    .GroupBy(s => s.Type)
                    .ToDictionary(g => g.Key, g => g.Count());

                var averageSessionDuration = sessions.Any() ? sessions.Average(s => s.DurationMinutes) : 0;

                var focusScore = sessions.Any() ? sessions.Average(s => s.FocusScore) : 0;

                return new StudyAnalytics
                {
                    UserId = userId,
                    StartDate = start,
                    EndDate = end,
                    TotalSessions = sessions.Count,
                    TotalStudyTime = sessions.Sum(s => s.DurationMinutes),
                    AverageSessionDuration = averageSessionDuration,
                    AverageFocusScore = focusScore,
                    DailyStudyTime = dailyStudyTime,
                    SessionsByType = sessionsByType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get study analytics for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<ChapterPerformance>> GetChapterPerformanceAsync(int userId)
        {
            try
            {
                var chapters = await _dbContext.StudyChapters
                    .Include(c => c.Section)
                    .ToListAsync();

                var performances = new List<ChapterPerformance>();

                foreach (var chapter in chapters)
                {
                    var progress = await _dbContext.UserProgress
                        .FirstOrDefaultAsync(up => up.UserId == userId && up.ChapterId == chapter.Id);

                    var quizAccuracy = await _dbContext.QuizAttempts
                        .Include(qa => qa.Question)
                        .Where(qa => qa.UserId == userId && qa.Question.ChapterId == chapter.Id)
                        .AverageAsync(qa => qa.IsCorrect ? 100.0 : 0.0);

                    var studyTime = await _dbContext.StudySessions
                        .Where(s => s.UserId == userId && s.ChapterId == chapter.Id)
                        .SumAsync(s => s.DurationMinutes);

                    var flashcardRetention = await _dbContext.FlashcardReviews
                        .Include(fr => fr.Flashcard)
                        .Where(fr => fr.UserId == userId && fr.Flashcard.ChapterId == chapter.Id)
                        .Where(fr => fr.Result == ReviewResult.Good || fr.Result == ReviewResult.Easy)
                        .CountAsync();

                    var totalFlashcardReviews = await _dbContext.FlashcardReviews
                        .Include(fr => fr.Flashcard)
                        .CountAsync(fr => fr.UserId == userId && fr.Flashcard.ChapterId == chapter.Id);

                    var flashcardRetentionRate = totalFlashcardReviews > 0 ? 
                        (double)flashcardRetention / totalFlashcardReviews * 100 : 0;

                    performances.Add(new ChapterPerformance
                    {
                        ChapterId = chapter.Id,
                        ChapterTitle = chapter.Title,
                        SectionTitle = chapter.Section.Title,
                        CompletionPercentage = progress?.CompletionPercentage ?? 0,
                        MasteryScore = progress?.MasteryScore ?? 0,
                        QuizAccuracy = quizAccuracy,
                        StudyTimeMinutes = studyTime,
                        FlashcardRetentionRate = flashcardRetentionRate,
                        Status = progress?.Status ?? ProgressStatus.NotStarted,
                        LastAccessed = progress?.LastAccessedAt
                    });
                }

                return performances.OrderBy(p => p.ChapterId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chapter performance for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<StudyStreak>> GetStudyStreaksAsync(int userId)
        {
            try
            {
                var sessions = await _dbContext.StudySessions
                    .Where(s => s.UserId == userId && s.CompletedSuccessfully)
                    .OrderBy(s => s.StartTime)
                    .Select(s => s.StartTime.Date)
                    .Distinct()
                    .ToListAsync();

                var streaks = new List<StudyStreak>();
                if (!sessions.Any()) return streaks;

                var currentStreak = new StudyStreak { StartDate = sessions[0], EndDate = sessions[0] };

                for (int i = 1; i < sessions.Count; i++)
                {
                    if (sessions[i] == currentStreak.EndDate.AddDays(1))
                    {
                        // Consecutive day, extend current streak
                        currentStreak.EndDate = sessions[i];
                    }
                    else
                    {
                        // Gap found, finalize current streak and start new one
                        currentStreak.DayCount = (currentStreak.EndDate - currentStreak.StartDate).Days + 1;
                        streaks.Add(currentStreak);
                        currentStreak = new StudyStreak { StartDate = sessions[i], EndDate = sessions[i] };
                    }
                }

                // Add the last streak
                currentStreak.DayCount = (currentStreak.EndDate - currentStreak.StartDate).Days + 1;
                streaks.Add(currentStreak);

                return streaks.OrderByDescending(s => s.DayCount).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get study streaks for user {UserId}", userId);
                throw;
            }
        }

        public async Task<ExamReadinessReport> GenerateExamReadinessReportAsync(int userId)
        {
            try
            {
                var analytics = await GetUserAnalyticsAsync(userId);
                var chapterPerformances = await GetChapterPerformanceAsync(userId);
                var weakAreas = await IdentifyWeakAreasAsync(userId);

                // Calculate readiness score using AI if available
                double readinessScore;
                try
                {
                    readinessScore = await _aiService.PredictExamReadinessAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get AI exam readiness prediction, using fallback calculation");
                    readinessScore = CalculateFallbackReadinessScore(analytics, chapterPerformances);
                }

                var recommendations = new List<string>();

                // Generate recommendations based on performance
                if (readinessScore < 70)
                {
                    recommendations.Add("Focus on completing more chapters to improve overall readiness");
                    recommendations.Add("Increase daily study time to at least 60 minutes");
                }

                if (analytics.OverallQuizAccuracy < 80)
                {
                    recommendations.Add("Review incorrect quiz answers and focus on weak concepts");
                    recommendations.Add("Take more practice quizzes to improve accuracy");
                }

                if (weakAreas.Any())
                {
                    recommendations.Add($"Pay special attention to: {string.Join(", ", weakAreas.Take(3).Select(w => w.ChapterTitle))}");
                }

                return new ExamReadinessReport
                {
                    UserId = userId,
                    ReadinessScore = readinessScore,
                    OverallProgress = (double)analytics.ChaptersCompleted / analytics.TotalChapters * 100,
                    QuizAccuracy = analytics.OverallQuizAccuracy,
                    StudyConsistency = analytics.CurrentStudyStreak,
                    WeakAreas = weakAreas.Take(5).ToList(),
                    Recommendations = recommendations,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate exam readiness report for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<WeakArea>> IdentifyWeakAreasAsync(int userId)
        {
            try
            {
                var chapterPerformances = await GetChapterPerformanceAsync(userId);

                var weakAreas = chapterPerformances
                    .Where(cp => cp.Status != ProgressStatus.NotStarted && 
                                (cp.MasteryScore < 70 || cp.QuizAccuracy < 70))
                    .Select(cp => new WeakArea
                    {
                        ChapterId = cp.ChapterId,
                        ChapterTitle = cp.ChapterTitle,
                        SectionTitle = cp.SectionTitle,
                        WeaknessScore = 100 - Math.Max(cp.MasteryScore, cp.QuizAccuracy),
                        IssueType = cp.QuizAccuracy < cp.MasteryScore ? "Quiz Performance" : "Content Mastery"
                    })
                    .OrderByDescending(wa => wa.WeaknessScore)
                    .ToList();

                return weakAreas;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to identify weak areas for user {UserId}", userId);
                throw;
            }
        }

        public async Task<StudyRecommendation> GetPersonalizedRecommendationsAsync(int userId)
        {
            try
            {
                var analytics = await GetUserAnalyticsAsync(userId);
                var weakAreas = await IdentifyWeakAreasAsync(userId);
                var recentSessions = await _dbContext.StudySessions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.StartTime)
                    .Take(10)
                    .ToListAsync();

                var recommendations = new List<string>();
                var priorityChapters = new List<int>();

                // Analyze study patterns
                var averageSessionLength = recentSessions.Any() ? recentSessions.Average(s => s.DurationMinutes) : 0;
                var studyFrequency = recentSessions.Count(s => s.StartTime > DateTime.UtcNow.AddDays(-7));

                // Generate recommendations
                if (averageSessionLength < 30)
                {
                    recommendations.Add("Try to extend your study sessions to at least 30-45 minutes for better retention");
                }

                if (studyFrequency < 4)
                {
                    recommendations.Add("Aim to study at least 4-5 times per week for consistent progress");
                }

                if (weakAreas.Any())
                {
                    recommendations.Add($"Focus on reviewing: {string.Join(", ", weakAreas.Take(3).Select(w => w.ChapterTitle))}");
                    priorityChapters.AddRange(weakAreas.Take(3).Select(w => w.ChapterId));
                }

                // AI-powered recommendations if available
                try
                {
                    var aiRecommendation = await _aiService.GetPersonalizedRecommendationAsync(userId, 
                        $"Analytics: {analytics.OverallQuizAccuracy}% accuracy, {analytics.CurrentStudyStreak} day streak");
                    recommendations.Add(aiRecommendation);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get AI recommendations");
                }

                return new StudyRecommendation
                {
                    UserId = userId,
                    Recommendations = recommendations,
                    PriorityChapterIds = priorityChapters,
                    SuggestedDailyStudyTime = Math.Max(45, (int)(averageSessionLength * 1.2)),
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get personalized recommendations for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Dictionary<DateTime, int>> GetStudyHeatmapDataAsync(int userId, int days = 90)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days).Date;
                var endDate = DateTime.UtcNow.Date;

                var sessions = await _dbContext.StudySessions
                    .Where(s => s.UserId == userId && s.StartTime.Date >= startDate && s.StartTime.Date <= endDate)
                    .GroupBy(s => s.StartTime.Date)
                    .Select(g => new { Date = g.Key, Minutes = g.Sum(s => s.DurationMinutes) })
                    .ToDictionaryAsync(x => x.Date, x => x.Minutes);

                // Fill in missing dates with 0
                var heatmapData = new Dictionary<DateTime, int>();
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    heatmapData[date] = sessions.ContainsKey(date) ? sessions[date] : 0;
                }

                return heatmapData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get study heatmap data for user {UserId}", userId);
                throw;
            }
        }

        private async Task<int> CalculateCurrentStudyStreakAsync(int userId)
        {
            var recentSessions = await _dbContext.StudySessions
                .Where(s => s.UserId == userId && s.CompletedSuccessfully)
                .OrderByDescending(s => s.StartTime)
                .Select(s => s.StartTime.Date)
                .Distinct()
                .Take(30)
                .ToListAsync();

            if (!recentSessions.Any()) return 0;

            var streak = 0;
            var currentDate = DateTime.UtcNow.Date;

            foreach (var sessionDate in recentSessions)
            {
                if (sessionDate == currentDate || sessionDate == currentDate.AddDays(-streak - 1))
                {
                    streak++;
                    currentDate = sessionDate;
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        private double CalculateFallbackReadinessScore(UserAnalytics analytics, List<ChapterPerformance> performances)
        {
            var completionScore = (double)analytics.ChaptersCompleted / analytics.TotalChapters * 100;
            var accuracyScore = analytics.OverallQuizAccuracy;
            var consistencyScore = Math.Min(100, analytics.CurrentStudyStreak * 10);

            return (completionScore * 0.5) + (accuracyScore * 0.3) + (consistencyScore * 0.2);
        }
    }

    // Analytics Data Models
    public class UserAnalytics
    {
        public int UserId { get; set; }
        public int TotalStudyTimeMinutes { get; set; }
        public int TotalQuizAttempts { get; set; }
        public double OverallQuizAccuracy { get; set; }
        public int ChaptersStarted { get; set; }
        public int ChaptersCompleted { get; set; }
        public int TotalChapters { get; set; }
        public int FlashcardsReviewed { get; set; }
        public int CurrentStudyStreak { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class StudyAnalytics
    {
        public int UserId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalSessions { get; set; }
        public int TotalStudyTime { get; set; }
        public double AverageSessionDuration { get; set; }
        public double AverageFocusScore { get; set; }
        public Dictionary<DateTime, int> DailyStudyTime { get; set; } = new();
        public Dictionary<SessionType, int> SessionsByType { get; set; } = new();
    }

    public class ChapterPerformance
    {
        public int ChapterId { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public double CompletionPercentage { get; set; }
        public double MasteryScore { get; set; }
        public double QuizAccuracy { get; set; }
        public int StudyTimeMinutes { get; set; }
        public double FlashcardRetentionRate { get; set; }
        public ProgressStatus Status { get; set; }
        public DateTime? LastAccessed { get; set; }
    }

    public class StudyStreak
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DayCount { get; set; }
    }

    public class ExamReadinessReport
    {
        public int UserId { get; set; }
        public double ReadinessScore { get; set; }
        public double OverallProgress { get; set; }
        public double QuizAccuracy { get; set; }
        public int StudyConsistency { get; set; }
        public List<WeakArea> WeakAreas { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class WeakArea
    {
        public int ChapterId { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public double WeaknessScore { get; set; }
        public string IssueType { get; set; } = string.Empty;
    }

    public class StudyRecommendation
    {
        public int UserId { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public List<int> PriorityChapterIds { get; set; } = new();
        public int SuggestedDailyStudyTime { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
