using CPTHub.Models;

namespace CPTHub.Services
{
    public interface IAIService
    {
        Task<string> GetStudyAssistanceAsync(string question, string context, int chapterId);
        Task<List<QuizQuestion>> GenerateQuizQuestionsAsync(int chapterId, int count, DifficultyLevel difficulty);
        Task<List<Flashcard>> GenerateFlashcardsAsync(int chapterId, int count);
        Task<string> ExplainIncorrectAnswerAsync(QuizQuestion question, string userAnswer);
        Task<string> GetPersonalizedRecommendationAsync(int userId, string currentContext);
        Task<double> PredictExamReadinessAsync(int userId);
        Task<bool> ValidateAIResponseAsync(string response, string topic);
        Task<bool> IsServiceHealthyAsync();
    }

    public interface ILearnLMService
    {
        Task<string> SendPromptAsync(string prompt, string context = "");
        Task<bool> ValidateApiKeyAsync();
        Task<bool> CheckServiceAvailabilityAsync();
        Task<string> GenerateContentAsync(string prompt, ContentType contentType);
    }

    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task ClearAsync();
        Task<bool> ExistsAsync(string key);
    }

    public interface IErrorHandlingService
    {
        Task<T> ExecuteWithFallbackAsync<T>(
            Func<Task<T>> primaryAction,
            Func<Task<T>> fallbackAction,
            string operationName);
        
        Task LogErrorAsync(Exception exception, string context, Dictionary<string, object>? additionalData = null);
        Task<bool> ShouldRetryAsync(Exception exception, int attemptCount);
        TimeSpan GetRetryDelay(int attemptCount);
    }

    public enum ContentType
    {
        StudyExplanation,
        QuizQuestion,
        Flashcard,
        Recommendation,
        ExamPrediction
    }

    public class AIServiceException : Exception
    {
        public string OperationType { get; }
        public bool IsRetryable { get; }

        public AIServiceException(string message, string operationType, bool isRetryable = true) 
            : base(message)
        {
            OperationType = operationType;
            IsRetryable = isRetryable;
        }

        public AIServiceException(string message, Exception innerException, string operationType, bool isRetryable = true) 
            : base(message, innerException)
        {
            OperationType = operationType;
            IsRetryable = isRetryable;
        }
    }
}
