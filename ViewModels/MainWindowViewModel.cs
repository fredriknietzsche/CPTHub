using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using CPTHub.Services;
using CPTHub.Models;
using System.Collections.ObjectModel;

namespace CPTHub.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IStudyService _studyService;
        private readonly IAIService _aiService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<MainWindowViewModel> _logger;

        [ObservableProperty]
        private string _welcomeMessage = "Welcome to CPTHub - Your NASM CPT Exam Preparation Assistant";

        [ObservableProperty]
        private double _overallProgress = 0.0;

        [ObservableProperty]
        private double _examReadiness = 0.0;

        [ObservableProperty]
        private bool _isAIServiceHealthy = false;

        [ObservableProperty]
        private string _currentUserName = "Student";

        [ObservableProperty]
        private ObservableCollection<StudySection> _studySections = new();

        [ObservableProperty]
        private ObservableCollection<StudyChapter> _recommendedChapters = new();

        [ObservableProperty]
        private StudySession? _currentSession;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public MainWindowViewModel(
            IStudyService studyService,
            IAIService aiService,
            INotificationService notificationService,
            ILogger<MainWindowViewModel> logger)
        {
            _studyService = studyService;
            _aiService = aiService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Initializing application...";

                // Initialize study content
                await _studyService.InitializeContentAsync();

                // Load study sections
                await LoadStudySectionsAsync();

                // Load user progress and recommendations
                await LoadUserDataAsync();

                // Check AI service health
                await CheckAIServiceHealthAsync();

                StatusMessage = "Ready";
                _logger.LogInformation("Main window initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize main window");
                StatusMessage = "Initialization failed. Please restart the application.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task StartStudyModeAsync()
        {
            try
            {
                StatusMessage = "Starting study mode...";
                // TODO: Navigate to study mode view
                _logger.LogInformation("Study mode started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start study mode");
                StatusMessage = "Failed to start study mode";
            }
        }

        [RelayCommand]
        private async Task StartQuizModeAsync()
        {
            try
            {
                StatusMessage = "Starting quiz mode...";
                // TODO: Navigate to quiz mode view
                _logger.LogInformation("Quiz mode started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start quiz mode");
                StatusMessage = "Failed to start quiz mode";
            }
        }

        [RelayCommand]
        private async Task StartFlashcardModeAsync()
        {
            try
            {
                StatusMessage = "Starting flashcard mode...";
                // TODO: Navigate to flashcard mode view
                _logger.LogInformation("Flashcard mode started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start flashcard mode");
                StatusMessage = "Failed to start flashcard mode";
            }
        }

        [RelayCommand]
        private async Task ViewAnalyticsAsync()
        {
            try
            {
                StatusMessage = "Loading analytics...";
                // TODO: Navigate to analytics view
                _logger.LogInformation("Analytics view opened");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open analytics");
                StatusMessage = "Failed to load analytics";
            }
        }

        [RelayCommand]
        private async Task OpenSettingsAsync()
        {
            try
            {
                StatusMessage = "Opening settings...";
                // TODO: Navigate to settings view
                _logger.LogInformation("Settings opened");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open settings");
                StatusMessage = "Failed to open settings";
            }
        }

        [RelayCommand]
        private async Task StartRecommendedChapterAsync(StudyChapter chapter)
        {
            if (chapter == null) return;

            try
            {
                StatusMessage = $"Starting chapter: {chapter.Title}";
                
                // Start a new study session
                CurrentSession = await _studyService.StartStudySessionAsync(1, chapter.Id, SessionType.Reading);
                
                // TODO: Navigate to chapter study view
                _logger.LogInformation("Started studying chapter: {ChapterTitle}", chapter.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start chapter study");
                StatusMessage = "Failed to start chapter study";
            }
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Refreshing data...";

                await LoadUserDataAsync();
                await CheckAIServiceHealthAsync();

                StatusMessage = "Data refreshed";
                _logger.LogInformation("Data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh data");
                StatusMessage = "Failed to refresh data";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ShowAIAssistantAsync()
        {
            try
            {
                if (!IsAIServiceHealthy)
                {
                    StatusMessage = "AI service is currently unavailable";
                    return;
                }

                StatusMessage = "Opening AI assistant...";
                // TODO: Open AI chat interface
                _logger.LogInformation("AI assistant opened");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open AI assistant");
                StatusMessage = "Failed to open AI assistant";
            }
        }

        private async Task LoadStudySectionsAsync()
        {
            try
            {
                var sections = await _studyService.GetAllSectionsAsync();
                StudySections.Clear();
                
                foreach (var section in sections)
                {
                    StudySections.Add(section);
                }

                _logger.LogDebug("Loaded {Count} study sections", sections.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load study sections");
                throw;
            }
        }

        private async Task LoadUserDataAsync()
        {
            try
            {
                const int userId = 1; // TODO: Get from user authentication

                // Load overall progress
                OverallProgress = await _studyService.CalculateOverallProgressAsync(userId);

                // Load recommended chapters
                var recommendations = await _studyService.GetRecommendedChaptersAsync(userId);
                RecommendedChapters.Clear();
                
                foreach (var chapter in recommendations)
                {
                    RecommendedChapters.Add(chapter);
                }

                // Get exam readiness prediction from AI
                if (IsAIServiceHealthy)
                {
                    try
                    {
                        ExamReadiness = await _aiService.PredictExamReadinessAsync(userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get AI exam readiness prediction");
                        ExamReadiness = OverallProgress * 0.8; // Fallback calculation
                    }
                }
                else
                {
                    ExamReadiness = OverallProgress * 0.8; // Fallback calculation
                }

                _logger.LogDebug("Loaded user data: {Progress}% progress, {Readiness}% readiness", 
                    OverallProgress, ExamReadiness);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user data");
                throw;
            }
        }

        private async Task CheckAIServiceHealthAsync()
        {
            try
            {
                IsAIServiceHealthy = await _aiService.IsServiceHealthyAsync();
                
                if (IsAIServiceHealthy)
                {
                    _logger.LogDebug("AI service is healthy");
                }
                else
                {
                    _logger.LogWarning("AI service is not available");
                    await _notificationService.ShowAchievementAsync(
                        "AI Service Unavailable", 
                        "AI features are currently offline. Core functionality remains available.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check AI service health");
                IsAIServiceHealthy = false;
            }
        }

        public async Task OnWindowClosingAsync()
        {
            try
            {
                // End current session if active
                if (CurrentSession != null && CurrentSession.EndTime == null)
                {
                    await _studyService.EndStudySessionAsync(CurrentSession.Id, true, "Session ended on application close");
                }

                _logger.LogInformation("Application closing gracefully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during application shutdown");
            }
        }
    }
}
