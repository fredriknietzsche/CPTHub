using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTHub.Models
{
    public class UserProfile
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
        
        public StudyGoal CurrentGoal { get; set; } = StudyGoal.PassExam;
        
        public int TargetStudyMinutesPerDay { get; set; } = 60;
        
        public DateTime? TargetExamDate { get; set; }
        
        public bool EnableAIFeatures { get; set; } = true;
        
        public bool EnableNotifications { get; set; } = true;
        
        public virtual ICollection<UserProgress> Progress { get; set; } = new List<UserProgress>();
        
        public virtual ICollection<StudySession> StudySessions { get; set; } = new List<StudySession>();
        
        public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
        
        public virtual ICollection<FlashcardReview> FlashcardReviews { get; set; } = new List<FlashcardReview>();
    }

    public class UserProgress
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public virtual UserProfile User { get; set; } = null!;
        
        public int ChapterId { get; set; }
        
        [ForeignKey("ChapterId")]
        public virtual StudyChapter Chapter { get; set; } = null!;
        
        public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
        
        public double CompletionPercentage { get; set; } = 0.0;
        
        public int TimeSpentMinutes { get; set; } = 0;
        
        public DateTime? StartedAt { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
        
        public double MasteryScore { get; set; } = 0.0; // 0-100 based on quiz performance
        
        public int ReviewCount { get; set; } = 0;
        
        public DateTime? NextReviewDue { get; set; }
    }

    public class StudySession
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public virtual UserProfile User { get; set; } = null!;
        
        public int? ChapterId { get; set; }
        
        [ForeignKey("ChapterId")]
        public virtual StudyChapter? Chapter { get; set; }
        
        public SessionType Type { get; set; }
        
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        
        public DateTime? EndTime { get; set; }
        
        public int DurationMinutes { get; set; } = 0;
        
        public int QuestionsAnswered { get; set; } = 0;
        
        public int CorrectAnswers { get; set; } = 0;
        
        public double FocusScore { get; set; } = 0.0; // Based on time between interactions
        
        public string Notes { get; set; } = string.Empty;
        
        public bool CompletedSuccessfully { get; set; } = false;
    }

    public class QuizAttempt
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public virtual UserProfile User { get; set; } = null!;
        
        public int QuestionId { get; set; }
        
        [ForeignKey("QuestionId")]
        public virtual QuizQuestion Question { get; set; } = null!;
        
        public string UserAnswer { get; set; } = string.Empty;
        
        public bool IsCorrect { get; set; }
        
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
        
        public int ResponseTimeSeconds { get; set; }
        
        public int? SessionId { get; set; }
        
        [ForeignKey("SessionId")]
        public virtual StudySession? Session { get; set; }
        
        public string? AIExplanation { get; set; } // AI-generated explanation for incorrect answers
    }

    public class FlashcardReview
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public virtual UserProfile User { get; set; } = null!;
        
        public int FlashcardId { get; set; }
        
        [ForeignKey("FlashcardId")]
        public virtual Flashcard Flashcard { get; set; } = null!;
        
        public ReviewResult Result { get; set; }
        
        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime NextReviewDue { get; set; }
        
        public int ResponseTimeSeconds { get; set; }
        
        public double EaseFactor { get; set; } = 2.5; // For spaced repetition algorithm
        
        public int Interval { get; set; } = 1; // Days until next review
        
        public int RepetitionCount { get; set; } = 0;
    }

    public enum StudyGoal
    {
        PassExam,
        MasterContent,
        QuickReview,
        WeakAreaFocus
    }

    public enum ProgressStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Mastered,
        NeedsReview
    }

    public enum SessionType
    {
        Reading,
        Quiz,
        Flashcards,
        Review,
        AIChat,
        Practice
    }

    public enum ReviewResult
    {
        Again,      // 0 - Complete blackout
        Hard,       // 1 - Incorrect response, but correct one remembered
        Good,       // 2 - Correct response, but with hesitation
        Easy        // 3 - Perfect response
    }
}
