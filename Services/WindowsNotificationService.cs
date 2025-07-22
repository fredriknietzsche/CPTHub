using Microsoft.Extensions.Logging;
using System.Diagnostics;
// using Microsoft.Toolkit.Win32.UI.Notifications;
// using Windows.UI.Notifications;

namespace CPTHub.Services
{
    public interface INotificationService
    {
        Task ShowStudyReminderAsync(string message, string chapterTitle);
        Task ShowAchievementAsync(string title, string message);
        Task ShowExamReadinessUpdateAsync(double readinessScore);
        Task ShowQuizCompletionAsync(int correctAnswers, int totalQuestions, string chapterTitle);
        Task ScheduleStudyReminderAsync(DateTime reminderTime, string message);
        Task ClearAllNotificationsAsync();
    }

    public class WindowsNotificationService : INotificationService
    {
        private readonly ILogger<WindowsNotificationService> _logger;
        private const string APP_ID = "CPTHub.NASM.ExamPrep";

        public WindowsNotificationService(ILogger<WindowsNotificationService> logger)
        {
            _logger = logger;
            InitializeNotifications();
        }

        private void InitializeNotifications()
        {
            try
            {
                // Register the app for notifications
                ToastNotificationManagerCompat.OnActivated += OnNotificationActivated;
                _logger.LogInformation("Windows notifications initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Windows notifications");
            }
        }

        public async Task ShowStudyReminderAsync(string message, string chapterTitle)
        {
            try
            {
                // Simplified notification for demo - using console output
                _logger.LogInformation("📚 Study Reminder: {Message} - Chapter: {Chapter}", message, chapterTitle);
                Console.WriteLine($"📚 Study Reminder: {message} - Chapter: {chapterTitle}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show study reminder notification");
            }
        }

        public async Task ShowAchievementAsync(string title, string message)
        {
            try
            {
                _logger.LogInformation("🏆 Achievement Unlocked: {Title} - {Message}", title, message);
                Console.WriteLine($"🏆 Achievement Unlocked: {title} - {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show achievement notification");
            }
        }

        public async Task ShowExamReadinessUpdateAsync(double readinessScore)
        {
            try
            {
                var emoji = readinessScore switch
                {
                    >= 90 => "🎯",
                    >= 80 => "📈",
                    >= 70 => "📊",
                    >= 60 => "⚡",
                    _ => "💪"
                };

                var message = readinessScore switch
                {
                    >= 90 => "Excellent! You're ready for the exam!",
                    >= 80 => "Great progress! Almost exam-ready!",
                    >= 70 => "Good work! Keep studying to improve your readiness.",
                    >= 60 => "Making progress! Focus on your weak areas.",
                    _ => "Keep going! Every study session counts."
                };

                var toastContent = new ToastContentBuilder()
                    .AddAppLogoOverride(new Uri("ms-appx:///Assets/progress.png"))
                    .AddText($"{emoji} Exam Readiness Update")
                    .AddText($"Current Score: {readinessScore:F1}%")
                    .AddText(message)
                    .AddButton(new ToastButton()
                        .SetContent("View Details")
                        .AddArgument("action", "viewAnalytics"))
                    .SetToastScenario(ToastScenario.Default);

                var toast = new ToastNotification(toastContent.GetXml());
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);

                _logger.LogDebug("Exam readiness notification sent: {Score}%", readinessScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show exam readiness notification");
            }
        }

        public async Task ShowQuizCompletionAsync(int correctAnswers, int totalQuestions, string chapterTitle)
        {
            try
            {
                var percentage = (double)correctAnswers / totalQuestions * 100;
                var emoji = percentage switch
                {
                    >= 90 => "🌟",
                    >= 80 => "✅",
                    >= 70 => "👍",
                    >= 60 => "📝",
                    _ => "💡"
                };

                var performance = percentage switch
                {
                    >= 90 => "Excellent!",
                    >= 80 => "Great job!",
                    >= 70 => "Good work!",
                    >= 60 => "Keep practicing!",
                    _ => "Review and try again!"
                };

                var toastContent = new ToastContentBuilder()
                    .AddAppLogoOverride(new Uri("ms-appx:///Assets/quiz.png"))
                    .AddText($"{emoji} Quiz Complete - {performance}")
                    .AddText($"Score: {correctAnswers}/{totalQuestions} ({percentage:F0}%)")
                    .AddText($"Chapter: {chapterTitle}")
                    .AddButton(new ToastButton()
                        .SetContent("Review Answers")
                        .AddArgument("action", "reviewQuiz")
                        .AddArgument("chapter", chapterTitle))
                    .AddButton(new ToastButton()
                        .SetContent("Next Chapter")
                        .AddArgument("action", "nextChapter"))
                    .SetToastScenario(ToastScenario.Default);

                var toast = new ToastNotification(toastContent.GetXml());
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);

                _logger.LogDebug("Quiz completion notification sent for chapter: {Chapter}, Score: {Score}%", 
                    chapterTitle, percentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show quiz completion notification");
            }
        }

        public async Task ScheduleStudyReminderAsync(DateTime reminderTime, string message)
        {
            try
            {
                var toastContent = new ToastContentBuilder()
                    .AddText("Scheduled Study Reminder")
                    .AddText(message)
                    .AddButton(new ToastButton()
                        .SetContent("Start Now")
                        .AddArgument("action", "startStudy"))
                    .SetToastScenario(ToastScenario.Reminder);

                var toast = new ScheduledToastNotification(toastContent.GetXml(), reminderTime);
                ToastNotificationManagerCompat.CreateToastNotifier().AddToSchedule(toast);

                _logger.LogDebug("Scheduled study reminder for: {ReminderTime}", reminderTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule study reminder");
            }
        }

        public async Task ClearAllNotificationsAsync()
        {
            try
            {
                var notifier = ToastNotificationManagerCompat.CreateToastNotifier();
                
                // Clear all scheduled notifications
                var scheduledNotifications = notifier.GetScheduledToastNotifications();
                foreach (var notification in scheduledNotifications)
                {
                    notifier.RemoveFromSchedule(notification);
                }

                _logger.LogInformation("Cleared all scheduled notifications");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear notifications");
            }
        }

        private void OnNotificationActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                var args = ToastArguments.Parse(e.Argument);
                var action = args.Get("action");

                _logger.LogDebug("Notification activated with action: {Action}", action);

                // Handle different notification actions
                switch (action)
                {
                    case "study":
                        HandleStudyAction(args);
                        break;
                    case "snooze":
                        HandleSnoozeAction(args);
                        break;
                    case "viewProgress":
                        HandleViewProgressAction();
                        break;
                    case "viewAnalytics":
                        HandleViewAnalyticsAction();
                        break;
                    case "reviewQuiz":
                        HandleReviewQuizAction(args);
                        break;
                    case "nextChapter":
                        HandleNextChapterAction();
                        break;
                    case "startStudy":
                        HandleStartStudyAction();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification activation");
            }
        }

        private void HandleStudyAction(ToastArguments args)
        {
            var chapter = args.Get("chapter");
            _logger.LogInformation("User wants to study chapter: {Chapter}", chapter);
            // TODO: Navigate to study mode for specific chapter
        }

        private void HandleSnoozeAction(ToastArguments args)
        {
            if (int.TryParse(args.Get("minutes"), out int minutes))
            {
                var snoozeTime = DateTime.Now.AddMinutes(minutes);
                _ = ScheduleStudyReminderAsync(snoozeTime, "Time to continue studying!");
                _logger.LogInformation("Snoozed study reminder for {Minutes} minutes", minutes);
            }
        }

        private void HandleViewProgressAction()
        {
            _logger.LogInformation("User wants to view progress");
            // TODO: Navigate to progress/analytics view
        }

        private void HandleViewAnalyticsAction()
        {
            _logger.LogInformation("User wants to view analytics");
            // TODO: Navigate to detailed analytics view
        }

        private void HandleReviewQuizAction(ToastArguments args)
        {
            var chapter = args.Get("chapter");
            _logger.LogInformation("User wants to review quiz for chapter: {Chapter}", chapter);
            // TODO: Navigate to quiz review for specific chapter
        }

        private void HandleNextChapterAction()
        {
            _logger.LogInformation("User wants to go to next chapter");
            // TODO: Navigate to next chapter in sequence
        }

        private void HandleStartStudyAction()
        {
            _logger.LogInformation("User wants to start studying");
            // TODO: Navigate to main study interface
        }
    }
}
