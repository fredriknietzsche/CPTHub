using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTHub.Models
{
    public class StudySection
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        
        public int OrderIndex { get; set; }
        
        public virtual ICollection<StudyChapter> Chapters { get; set; } = new List<StudyChapter>();
    }

    public class StudyChapter
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        public int ChapterNumber { get; set; }
        
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;
        
        public string Content { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string KeyConcepts { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string LearningObjectives { get; set; } = string.Empty;
        
        public int EstimatedReadingMinutes { get; set; }
        
        public int SectionId { get; set; }
        
        [ForeignKey("SectionId")]
        public virtual StudySection Section { get; set; } = null!;
        
        public virtual ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
        
        public virtual ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
        
        public virtual ICollection<UserProgress> UserProgress { get; set; } = new List<UserProgress>();
    }

    public class QuizQuestion
    {
        public int Id { get; set; }
        
        [Required]
        public string Question { get; set; } = string.Empty;
        
        [Required]
        public string CorrectAnswer { get; set; } = string.Empty;
        
        public string IncorrectAnswer1 { get; set; } = string.Empty;
        
        public string IncorrectAnswer2 { get; set; } = string.Empty;
        
        public string IncorrectAnswer3 { get; set; } = string.Empty;
        
        public string Explanation { get; set; } = string.Empty;
        
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;
        
        public QuestionType Type { get; set; } = QuestionType.MultipleChoice;
        
        public bool IsAIGenerated { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public int ChapterId { get; set; }
        
        [ForeignKey("ChapterId")]
        public virtual StudyChapter Chapter { get; set; } = null!;
        
        public virtual ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
    }

    public class Flashcard
    {
        public int Id { get; set; }
        
        [Required]
        public string Front { get; set; } = string.Empty;
        
        [Required]
        public string Back { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string Hint { get; set; } = string.Empty;
        
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;
        
        public bool IsAIGenerated { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public int ChapterId { get; set; }
        
        [ForeignKey("ChapterId")]
        public virtual StudyChapter Chapter { get; set; } = null!;
        
        public virtual ICollection<FlashcardReview> Reviews { get; set; } = new List<FlashcardReview>();
    }

    public enum DifficultyLevel
    {
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Expert = 4
    }

    public enum QuestionType
    {
        MultipleChoice,
        TrueFalse,
        FillInTheBlank,
        Scenario
    }
}
