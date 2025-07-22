using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CPTHub.Data;
using CPTHub.Models;

namespace CPTHub.Services
{
    public interface IFlashcardService
    {
        Task<List<Flashcard>> GetFlashcardsAsync(int chapterId, int count = 10);
        Task<Flashcard?> GetFlashcardAsync(int flashcardId);
        Task<FlashcardReview> ReviewFlashcardAsync(int userId, int flashcardId, ReviewResult result, int responseTimeSeconds);
        Task<List<Flashcard>> GetDueFlashcardsAsync(int userId, int maxCount = 20);
        Task<List<FlashcardReview>> GetReviewHistoryAsync(int userId, int flashcardId);
        Task<double> CalculateRetentionRateAsync(int userId, int chapterId);
        Task<List<Flashcard>> GenerateFlashcardsAsync(int chapterId, int count);
        Task<Dictionary<int, int>> GetDueCountsByChapterAsync(int userId);
        Task ResetFlashcardProgressAsync(int userId, int flashcardId);
    }

    public class FlashcardService : IFlashcardService
    {
        private readonly CPTHubDbContext _dbContext;
        private readonly IAIService _aiService;
        private readonly ILogger<FlashcardService> _logger;

        public FlashcardService(CPTHubDbContext dbContext, IAIService aiService, ILogger<FlashcardService> logger)
        {
            _dbContext = dbContext;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<List<Flashcard>> GetFlashcardsAsync(int chapterId, int count = 10)
        {
            try
            {
                var flashcards = await _dbContext.Flashcards
                    .Where(f => f.ChapterId == chapterId)
                    .OrderBy(f => Guid.NewGuid())
                    .Take(count)
                    .ToListAsync();

                // If we don't have enough flashcards, generate more with AI
                if (flashcards.Count < count)
                {
                    var needed = count - flashcards.Count;
                    var aiFlashcards = await GenerateFlashcardsAsync(chapterId, needed);
                    flashcards.AddRange(aiFlashcards);
                }

                _logger.LogDebug("Retrieved {Count} flashcards for chapter {ChapterId}", flashcards.Count, chapterId);
                return flashcards;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get flashcards for chapter {ChapterId}", chapterId);
                throw;
            }
        }

        public async Task<Flashcard?> GetFlashcardAsync(int flashcardId)
        {
            return await _dbContext.Flashcards
                .Include(f => f.Chapter)
                .FirstOrDefaultAsync(f => f.Id == flashcardId);
        }

        public async Task<FlashcardReview> ReviewFlashcardAsync(int userId, int flashcardId, ReviewResult result, int responseTimeSeconds)
        {
            try
            {
                var flashcard = await GetFlashcardAsync(flashcardId);
                if (flashcard == null)
                {
                    throw new ArgumentException($"Flashcard {flashcardId} not found");
                }

                // Get the last review to calculate new spaced repetition values
                var lastReview = await _dbContext.FlashcardReviews
                    .Where(fr => fr.UserId == userId && fr.FlashcardId == flashcardId)
                    .OrderByDescending(fr => fr.ReviewedAt)
                    .FirstOrDefaultAsync();

                var (newInterval, newEaseFactor, newRepetitionCount) = CalculateSpacedRepetition(
                    result, 
                    lastReview?.Interval ?? 1,
                    lastReview?.EaseFactor ?? 2.5,
                    lastReview?.RepetitionCount ?? 0);

                var review = new FlashcardReview
                {
                    UserId = userId,
                    FlashcardId = flashcardId,
                    Result = result,
                    ReviewedAt = DateTime.UtcNow,
                    NextReviewDue = DateTime.UtcNow.AddDays(newInterval),
                    ResponseTimeSeconds = responseTimeSeconds,
                    EaseFactor = newEaseFactor,
                    Interval = newInterval,
                    RepetitionCount = newRepetitionCount
                };

                _dbContext.FlashcardReviews.Add(review);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Flashcard review submitted: User {UserId}, Flashcard {FlashcardId}, Result: {Result}, Next due: {NextDue}", 
                    userId, flashcardId, result, review.NextReviewDue);

                return review;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to review flashcard");
                throw;
            }
        }

        public async Task<List<Flashcard>> GetDueFlashcardsAsync(int userId, int maxCount = 20)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Get flashcards that are due for review
                var dueFlashcardIds = await _dbContext.FlashcardReviews
                    .Where(fr => fr.UserId == userId && fr.NextReviewDue <= now)
                    .GroupBy(fr => fr.FlashcardId)
                    .Select(g => g.Key)
                    .ToListAsync();

                // Get flashcards that have never been reviewed
                var allFlashcardIds = await _dbContext.Flashcards.Select(f => f.Id).ToListAsync();
                var reviewedFlashcardIds = await _dbContext.FlashcardReviews
                    .Where(fr => fr.UserId == userId)
                    .Select(fr => fr.FlashcardId)
                    .Distinct()
                    .ToListAsync();

                var neverReviewedIds = allFlashcardIds.Except(reviewedFlashcardIds).ToList();

                // Combine due and never-reviewed flashcards
                var targetIds = dueFlashcardIds.Concat(neverReviewedIds).Distinct().Take(maxCount).ToList();

                var dueFlashcards = await _dbContext.Flashcards
                    .Include(f => f.Chapter)
                    .Where(f => targetIds.Contains(f.Id))
                    .OrderBy(f => Guid.NewGuid()) // Randomize order
                    .ToListAsync();

                _logger.LogDebug("Found {Count} due flashcards for user {UserId}", dueFlashcards.Count, userId);
                return dueFlashcards;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get due flashcards for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<FlashcardReview>> GetReviewHistoryAsync(int userId, int flashcardId)
        {
            return await _dbContext.FlashcardReviews
                .Where(fr => fr.UserId == userId && fr.FlashcardId == flashcardId)
                .OrderByDescending(fr => fr.ReviewedAt)
                .ToListAsync();
        }

        public async Task<double> CalculateRetentionRateAsync(int userId, int chapterId)
        {
            var reviews = await _dbContext.FlashcardReviews
                .Include(fr => fr.Flashcard)
                .Where(fr => fr.UserId == userId && fr.Flashcard.ChapterId == chapterId)
                .ToListAsync();

            if (!reviews.Any()) return 0.0;

            var successfulReviews = reviews.Count(r => r.Result == ReviewResult.Good || r.Result == ReviewResult.Easy);
            return (double)successfulReviews / reviews.Count * 100.0;
        }

        public async Task<List<Flashcard>> GenerateFlashcardsAsync(int chapterId, int count)
        {
            try
            {
                var aiFlashcards = await _aiService.GenerateFlashcardsAsync(chapterId, count);
                
                if (aiFlashcards.Any())
                {
                    _dbContext.Flashcards.AddRange(aiFlashcards);
                    await _dbContext.SaveChangesAsync();
                    
                    _logger.LogDebug("Generated {Count} AI flashcards for chapter {ChapterId}", aiFlashcards.Count, chapterId);
                }

                return aiFlashcards;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate AI flashcards for chapter {ChapterId}", chapterId);
                return new List<Flashcard>();
            }
        }

        public async Task<Dictionary<int, int>> GetDueCountsByChapterAsync(int userId)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Get due flashcards by chapter
                var dueByChapter = await _dbContext.FlashcardReviews
                    .Include(fr => fr.Flashcard)
                    .Where(fr => fr.UserId == userId && fr.NextReviewDue <= now)
                    .GroupBy(fr => fr.Flashcard.ChapterId)
                    .Select(g => new { ChapterId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ChapterId, x => x.Count);

                // Get never-reviewed flashcards by chapter
                var reviewedFlashcardIds = await _dbContext.FlashcardReviews
                    .Where(fr => fr.UserId == userId)
                    .Select(fr => fr.FlashcardId)
                    .Distinct()
                    .ToListAsync();

                var neverReviewedByChapter = await _dbContext.Flashcards
                    .Where(f => !reviewedFlashcardIds.Contains(f.Id))
                    .GroupBy(f => f.ChapterId)
                    .Select(g => new { ChapterId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ChapterId, x => x.Count);

                // Combine the counts
                var result = new Dictionary<int, int>();
                foreach (var kvp in dueByChapter)
                {
                    result[kvp.Key] = kvp.Value;
                }

                foreach (var kvp in neverReviewedByChapter)
                {
                    if (result.ContainsKey(kvp.Key))
                    {
                        result[kvp.Key] += kvp.Value;
                    }
                    else
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get due counts by chapter for user {UserId}", userId);
                return new Dictionary<int, int>();
            }
        }

        public async Task ResetFlashcardProgressAsync(int userId, int flashcardId)
        {
            try
            {
                var reviews = await _dbContext.FlashcardReviews
                    .Where(fr => fr.UserId == userId && fr.FlashcardId == flashcardId)
                    .ToListAsync();

                _dbContext.FlashcardReviews.RemoveRange(reviews);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Reset flashcard progress for user {UserId}, flashcard {FlashcardId}", userId, flashcardId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset flashcard progress");
                throw;
            }
        }

        private (int newInterval, double newEaseFactor, int newRepetitionCount) CalculateSpacedRepetition(
            ReviewResult result, int currentInterval, double currentEaseFactor, int currentRepetitionCount)
        {
            // Implementation of the SM-2 spaced repetition algorithm
            var newEaseFactor = currentEaseFactor;
            var newRepetitionCount = currentRepetitionCount;
            int newInterval;

            switch (result)
            {
                case ReviewResult.Again: // Complete failure
                    newRepetitionCount = 0;
                    newInterval = 1;
                    break;

                case ReviewResult.Hard: // Incorrect but remembered
                    newEaseFactor = Math.Max(1.3, currentEaseFactor - 0.15);
                    newRepetitionCount = 0;
                    newInterval = 1;
                    break;

                case ReviewResult.Good: // Correct with some hesitation
                    newRepetitionCount++;
                    newEaseFactor = Math.Max(1.3, currentEaseFactor - 0.02);
                    
                    if (newRepetitionCount == 1)
                        newInterval = 1;
                    else if (newRepetitionCount == 2)
                        newInterval = 6;
                    else
                        newInterval = (int)Math.Round(currentInterval * newEaseFactor);
                    break;

                case ReviewResult.Easy: // Perfect recall
                    newRepetitionCount++;
                    newEaseFactor = currentEaseFactor + 0.1;
                    
                    if (newRepetitionCount == 1)
                        newInterval = 4;
                    else if (newRepetitionCount == 2)
                        newInterval = 6;
                    else
                        newInterval = (int)Math.Round(currentInterval * newEaseFactor);
                    break;

                default:
                    newInterval = currentInterval;
                    break;
            }

            // Ensure minimum and maximum intervals
            newInterval = Math.Max(1, Math.Min(365, newInterval));
            newEaseFactor = Math.Max(1.3, Math.Min(2.5, newEaseFactor));

            return (newInterval, newEaseFactor, newRepetitionCount);
        }
    }
}
