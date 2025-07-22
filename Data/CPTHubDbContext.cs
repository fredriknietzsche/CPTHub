using Microsoft.EntityFrameworkCore;
using CPTHub.Models;

namespace CPTHub.Data
{
    public class CPTHubDbContext : DbContext
    {
        public CPTHubDbContext(DbContextOptions<CPTHubDbContext> options) : base(options)
        {
        }

        // Study Content
        public DbSet<StudySection> StudySections { get; set; }
        public DbSet<StudyChapter> StudyChapters { get; set; }
        public DbSet<QuizQuestion> QuizQuestions { get; set; }
        public DbSet<Flashcard> Flashcards { get; set; }

        // User Data
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserProgress> UserProgress { get; set; }
        public DbSet<StudySession> StudySessions { get; set; }
        public DbSet<QuizAttempt> QuizAttempts { get; set; }
        public DbSet<FlashcardReview> FlashcardReviews { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<StudyChapter>()
                .HasOne(c => c.Section)
                .WithMany(s => s.Chapters)
                .HasForeignKey(c => c.SectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizQuestion>()
                .HasOne(q => q.Chapter)
                .WithMany(c => c.Questions)
                .HasForeignKey(q => q.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Flashcard>()
                .HasOne(f => f.Chapter)
                .WithMany(c => c.Flashcards)
                .HasForeignKey(f => f.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserProgress>()
                .HasOne(up => up.User)
                .WithMany(u => u.Progress)
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserProgress>()
                .HasOne(up => up.Chapter)
                .WithMany(c => c.UserProgress)
                .HasForeignKey(up => up.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudySession>()
                .HasOne(ss => ss.User)
                .WithMany(u => u.StudySessions)
                .HasForeignKey(ss => ss.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizAttempt>()
                .HasOne(qa => qa.User)
                .WithMany(u => u.QuizAttempts)
                .HasForeignKey(qa => qa.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizAttempt>()
                .HasOne(qa => qa.Question)
                .WithMany(q => q.Attempts)
                .HasForeignKey(qa => qa.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FlashcardReview>()
                .HasOne(fr => fr.User)
                .WithMany(u => u.FlashcardReviews)
                .HasForeignKey(fr => fr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FlashcardReview>()
                .HasOne(fr => fr.Flashcard)
                .WithMany(f => f.Reviews)
                .HasForeignKey(fr => fr.FlashcardId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure indexes for performance
            modelBuilder.Entity<UserProgress>()
                .HasIndex(up => new { up.UserId, up.ChapterId })
                .IsUnique();

            modelBuilder.Entity<QuizAttempt>()
                .HasIndex(qa => new { qa.UserId, qa.QuestionId, qa.AttemptedAt });

            modelBuilder.Entity<FlashcardReview>()
                .HasIndex(fr => new { fr.UserId, fr.NextReviewDue });

            modelBuilder.Entity<StudySession>()
                .HasIndex(ss => new { ss.UserId, ss.StartTime });

            // Seed initial data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed NASM Study Sections
            var sections = new[]
            {
                new StudySection { Id = 1, Title = "Professional Development and Responsibility", Description = "Modern state of health and fitness, and the personal training profession", OrderIndex = 1 },
                new StudySection { Id = 2, Title = "Client Relations and Behavioral Coaching", Description = "Psychology of exercise and behavioral coaching techniques", OrderIndex = 2 },
                new StudySection { Id = 3, Title = "Basic and Applied Sciences and Nutritional Concepts", Description = "Human anatomy, physiology, movement science, metabolism, and nutrition", OrderIndex = 3 },
                new StudySection { Id = 4, Title = "Assessment", Description = "Health, wellness, fitness, posture, movement, and performance assessments", OrderIndex = 4 },
                new StudySection { Id = 5, Title = "Exercise Technique and Training Instruction", Description = "Integrated training concepts and the OPT model implementation", OrderIndex = 5 },
                new StudySection { Id = 6, Title = "Program Design", Description = "OPT model application, exercise modalities, and special populations", OrderIndex = 6 }
            };

            modelBuilder.Entity<StudySection>().HasData(sections);

            // Seed NASM Study Chapters
            var chapters = new[]
            {
                // Section 1: Professional Development and Responsibility
                new StudyChapter { Id = 1, Title = "Modern State of Health and Fitness", ChapterNumber = 1, SectionId = 1, EstimatedReadingMinutes = 45, Description = "Overview of current health and fitness landscape", KeyConcepts = "Health statistics, fitness trends, chronic diseases", LearningObjectives = "Understand current health challenges and fitness industry role" },
                new StudyChapter { Id = 2, Title = "The Personal Training Profession", ChapterNumber = 2, SectionId = 1, EstimatedReadingMinutes = 60, Description = "Professional standards, ethics, and career development", KeyConcepts = "Professional standards, ethics, scope of practice", LearningObjectives = "Define professional responsibilities and ethical guidelines" },

                // Section 2: Client Relations and Behavioral Coaching
                new StudyChapter { Id = 3, Title = "Psychology of Exercise", ChapterNumber = 3, SectionId = 2, EstimatedReadingMinutes = 50, Description = "Psychological factors affecting exercise behavior", KeyConcepts = "Motivation, adherence, behavior change", LearningObjectives = "Apply psychological principles to enhance client motivation" },
                new StudyChapter { Id = 4, Title = "Behavioral Coaching", ChapterNumber = 4, SectionId = 2, EstimatedReadingMinutes = 55, Description = "Coaching techniques and communication strategies", KeyConcepts = "Coaching models, communication, goal setting", LearningObjectives = "Implement effective coaching strategies" },

                // Section 3: Basic and Applied Sciences and Nutritional Concepts
                new StudyChapter { Id = 5, Title = "The Nervous, Skeletal, and Muscular Systems", ChapterNumber = 5, SectionId = 3, EstimatedReadingMinutes = 75, Description = "Anatomy and physiology of movement systems", KeyConcepts = "Nervous system, bones, muscles, movement", LearningObjectives = "Understand anatomical systems supporting movement" },
                new StudyChapter { Id = 6, Title = "The Cardiorespiratory, Endocrine, and Digestive Systems", ChapterNumber = 6, SectionId = 3, EstimatedReadingMinutes = 70, Description = "Physiological systems supporting exercise", KeyConcepts = "Heart, lungs, hormones, digestion", LearningObjectives = "Explain physiological responses to exercise" },
                new StudyChapter { Id = 7, Title = "Human Movement Science", ChapterNumber = 7, SectionId = 3, EstimatedReadingMinutes = 65, Description = "Biomechanics and movement analysis", KeyConcepts = "Biomechanics, movement patterns, kinetic chain", LearningObjectives = "Analyze human movement patterns" },
                new StudyChapter { Id = 8, Title = "Exercise Metabolism and Bioenergetics", ChapterNumber = 8, SectionId = 3, EstimatedReadingMinutes = 60, Description = "Energy systems and metabolic responses", KeyConcepts = "ATP, energy systems, metabolism", LearningObjectives = "Understand energy production during exercise" },
                new StudyChapter { Id = 9, Title = "Nutrition", ChapterNumber = 9, SectionId = 3, EstimatedReadingMinutes = 80, Description = "Nutritional principles for health and performance", KeyConcepts = "Macronutrients, micronutrients, hydration", LearningObjectives = "Apply basic nutritional principles" },
                new StudyChapter { Id = 10, Title = "Supplementation", ChapterNumber = 10, SectionId = 3, EstimatedReadingMinutes = 40, Description = "Evidence-based supplementation guidelines", KeyConcepts = "Supplements, safety, efficacy", LearningObjectives = "Evaluate supplement use and safety" },

                // Section 4: Assessment
                new StudyChapter { Id = 11, Title = "Health, Wellness, and Fitness Assessments", ChapterNumber = 11, SectionId = 4, EstimatedReadingMinutes = 90, Description = "Comprehensive client assessment protocols", KeyConcepts = "Health screening, fitness testing, risk stratification", LearningObjectives = "Conduct comprehensive client assessments" },
                new StudyChapter { Id = 12, Title = "Posture, Movement, and Performance Assessments", ChapterNumber = 12, SectionId = 4, EstimatedReadingMinutes = 85, Description = "Movement analysis and performance testing", KeyConcepts = "Posture analysis, movement screens, performance tests", LearningObjectives = "Assess movement quality and performance" },

                // Section 5: Exercise Technique and Training Instruction
                new StudyChapter { Id = 13, Title = "Integrated Training and the OPT Model", ChapterNumber = 13, SectionId = 5, EstimatedReadingMinutes = 70, Description = "Overview of NASM's OPT training model", KeyConcepts = "OPT model, integrated training, periodization", LearningObjectives = "Understand the OPT model framework" },
                new StudyChapter { Id = 14, Title = "Flexibility Training Concepts", ChapterNumber = 14, SectionId = 5, EstimatedReadingMinutes = 55, Description = "Flexibility training methods and applications", KeyConcepts = "Flexibility, stretching techniques, corrective exercise", LearningObjectives = "Design flexibility training programs" },
                new StudyChapter { Id = 15, Title = "Cardiorespiratory Training", ChapterNumber = 15, SectionId = 5, EstimatedReadingMinutes = 65, Description = "Cardiovascular exercise programming", KeyConcepts = "Cardio training, heart rate zones, programming", LearningObjectives = "Design cardiorespiratory training programs" },
                new StudyChapter { Id = 16, Title = "Core Training Concepts", ChapterNumber = 16, SectionId = 5, EstimatedReadingMinutes = 50, Description = "Core stability and strengthening principles", KeyConcepts = "Core stability, core strength, functional movement", LearningObjectives = "Implement core training strategies" },
                new StudyChapter { Id = 17, Title = "Balance Training Concepts", ChapterNumber = 17, SectionId = 5, EstimatedReadingMinutes = 45, Description = "Balance and proprioceptive training", KeyConcepts = "Balance, proprioception, stability training", LearningObjectives = "Design balance training programs" },
                new StudyChapter { Id = 18, Title = "Plyometric (Reactive) Training Concepts", ChapterNumber = 18, SectionId = 5, EstimatedReadingMinutes = 55, Description = "Power development through plyometric training", KeyConcepts = "Plyometrics, power, reactive training", LearningObjectives = "Implement plyometric training safely" },
                new StudyChapter { Id = 19, Title = "Speed, Agility, and Quickness Training Concepts", ChapterNumber = 19, SectionId = 5, EstimatedReadingMinutes = 50, Description = "Athletic performance enhancement training", KeyConcepts = "Speed, agility, quickness, athletic performance", LearningObjectives = "Design SAQ training programs" },
                new StudyChapter { Id = 20, Title = "Resistance Training Concepts", ChapterNumber = 20, SectionId = 5, EstimatedReadingMinutes = 75, Description = "Strength training principles and methods", KeyConcepts = "Resistance training, strength, hypertrophy, endurance", LearningObjectives = "Design effective resistance training programs" },

                // Section 6: Program Design
                new StudyChapter { Id = 21, Title = "The Optimum Performance Training Model", ChapterNumber = 21, SectionId = 6, EstimatedReadingMinutes = 80, Description = "Comprehensive OPT model application", KeyConcepts = "OPT phases, program design, periodization", LearningObjectives = "Apply the complete OPT model" },
                new StudyChapter { Id = 22, Title = "Introduction to Exercise Modalities", ChapterNumber = 22, SectionId = 6, EstimatedReadingMinutes = 60, Description = "Various exercise equipment and methods", KeyConcepts = "Exercise equipment, modalities, program variation", LearningObjectives = "Select appropriate exercise modalities" },
                new StudyChapter { Id = 23, Title = "Chronic Health Conditions and Special Populations", ChapterNumber = 23, SectionId = 6, EstimatedReadingMinutes = 90, Description = "Training considerations for special populations", KeyConcepts = "Special populations, chronic conditions, modifications", LearningObjectives = "Modify programs for special populations" }
            };

            modelBuilder.Entity<StudyChapter>().HasData(chapters);
        }
    }
}
