using Microsoft.Extensions.Logging;

namespace CPTHub.Services
{
    public class SimpleNotificationService : INotificationService
    {
        private readonly ILogger<SimpleNotificationService> _logger;

        public SimpleNotificationService(ILogger<SimpleNotificationService> logger)
        {
            _logger = logger;
        }

        public async Task ShowStudyReminderAsync(string message, string chapterTitle)
        {
            _logger.LogInformation("📚 Study Reminder: {Message} - Chapter: {Chapter}", message, chapterTitle);
            Console.WriteLine($"📚 Study Reminder: {message} - Chapter: {chapterTitle}");
        }

        public async Task ShowAchievementAsync(string title, string message)
        {
            _logger.LogInformation("🏆 Achievement: {Title} - {Message}", title, message);
            Console.WriteLine($"🏆 Achievement: {title} - {message}");
        }

        public async Task ShowExamReadinessUpdateAsync(double readinessScore)
        {
            _logger.LogInformation("📊 Exam Readiness: {Score:F1}%", readinessScore);
            Console.WriteLine($"📊 Exam Readiness: {readinessScore:F1}%");
        }

        public async Task ShowQuizCompletionAsync(int correctAnswers, int totalQuestions, string chapterTitle)
        {
            var percentage = (double)correctAnswers / totalQuestions * 100;
            _logger.LogInformation("✅ Quiz Complete: {Score}/{Total} ({Percentage:F0}%) - {Chapter}", 
                correctAnswers, totalQuestions, percentage, chapterTitle);
            Console.WriteLine($"✅ Quiz Complete: {correctAnswers}/{totalQuestions} ({percentage:F0}%) - {chapterTitle}");
        }

        public async Task ScheduleStudyReminderAsync(DateTime reminderTime, string message)
        {
            _logger.LogInformation("⏰ Scheduled Reminder for {Time}: {Message}", reminderTime, message);
            Console.WriteLine($"⏰ Scheduled Reminder for {reminderTime}: {message}");
        }

        public async Task ClearAllNotificationsAsync()
        {
            _logger.LogInformation("🧹 Cleared all notifications");
            Console.WriteLine("🧹 Cleared all notifications");
        }
    }
}
