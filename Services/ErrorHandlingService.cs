using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CPTHub.Services
{
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly Dictionary<string, int> _operationAttempts = new();
        private readonly object _lockObject = new();

        public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteWithFallbackAsync<T>(
            Func<Task<T>> primaryAction,
            Func<Task<T>> fallbackAction,
            string operationName)
        {
            var operationKey = $"{operationName}_{Thread.CurrentThread.ManagedThreadId}_{DateTime.UtcNow.Ticks}";
            
            try
            {
                _logger.LogDebug("Executing primary action for operation: {OperationName}", operationName);
                return await primaryAction();
            }
            catch (Exception primaryException)
            {
                await LogErrorAsync(primaryException, $"Primary action failed for {operationName}");
                
                try
                {
                    _logger.LogWarning("Primary action failed for {OperationName}, attempting fallback", operationName);
                    return await fallbackAction();
                }
                catch (Exception fallbackException)
                {
                    await LogErrorAsync(fallbackException, $"Fallback action failed for {operationName}");
                    
                    // If both primary and fallback fail, throw a comprehensive exception
                    throw new AIServiceException(
                        $"Both primary and fallback actions failed for {operationName}. " +
                        $"Primary: {primaryException.Message}, Fallback: {fallbackException.Message}",
                        primaryException,
                        operationName,
                        false);
                }
            }
        }

        public async Task LogErrorAsync(Exception exception, string context, Dictionary<string, object>? additionalData = null)
        {
            var logData = new Dictionary<string, object>
            {
                ["Context"] = context,
                ["ExceptionType"] = exception.GetType().Name,
                ["Message"] = exception.Message,
                ["StackTrace"] = exception.StackTrace ?? "",
                ["Timestamp"] = DateTime.UtcNow
            };

            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    logData[kvp.Key] = kvp.Value;
                }
            }

            if (exception.InnerException != null)
            {
                logData["InnerException"] = exception.InnerException.Message;
            }

            // Log based on exception severity
            if (IsRetryableException(exception))
            {
                _logger.LogWarning(exception, "Retryable error in {Context}: {Message}", context, exception.Message);
            }
            else if (IsCriticalException(exception))
            {
                _logger.LogCritical(exception, "Critical error in {Context}: {Message}", context, exception.Message);
            }
            else
            {
                _logger.LogError(exception, "Error in {Context}: {Message}", context, exception.Message);
            }

            // Store error for pattern analysis
            await StoreErrorPatternAsync(exception, context);
        }

        public async Task<bool> ShouldRetryAsync(Exception exception, int attemptCount)
        {
            const int maxRetries = 3;
            
            if (attemptCount >= maxRetries)
            {
                _logger.LogWarning("Maximum retry attempts ({MaxRetries}) reached", maxRetries);
                return false;
            }

            if (!IsRetryableException(exception))
            {
                _logger.LogDebug("Exception is not retryable: {ExceptionType}", exception.GetType().Name);
                return false;
            }

            // Check if we're hitting rate limits
            if (IsRateLimitException(exception))
            {
                var delay = GetRateLimitDelay(exception);
                if (delay > TimeSpan.FromMinutes(5)) // Don't wait more than 5 minutes
                {
                    _logger.LogWarning("Rate limit delay too long: {Delay}", delay);
                    return false;
                }
            }

            return true;
        }

        public TimeSpan GetRetryDelay(int attemptCount)
        {
            // Exponential backoff with jitter
            var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attemptCount));
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
            
            var totalDelay = baseDelay + jitter;
            
            // Cap at 30 seconds
            return totalDelay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : totalDelay;
        }

        private bool IsRetryableException(Exception exception)
        {
            return exception switch
            {
                HttpRequestException => true,
                TaskCanceledException => true,
                SocketException => true,
                AIServiceException aiEx => aiEx.IsRetryable,
                _ when IsNetworkException(exception) => true,
                _ when IsTimeoutException(exception) => true,
                _ when IsRateLimitException(exception) => true,
                _ => false
            };
        }

        private bool IsCriticalException(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => true,
                StackOverflowException => true,
                AccessViolationException => true,
                _ => false
            };
        }

        private bool IsNetworkException(Exception exception)
        {
            return exception.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                   exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                   exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTimeoutException(Exception exception)
        {
            return exception is TaskCanceledException ||
                   exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRateLimitException(Exception exception)
        {
            if (exception is HttpRequestException httpEx)
            {
                return httpEx.Message.Contains("429") || 
                       httpEx.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                       httpEx.Message.Contains("quota", StringComparison.OrdinalIgnoreCase);
            }

            return exception.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                   exception.Message.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase);
        }

        private TimeSpan GetRateLimitDelay(Exception exception)
        {
            // Try to extract retry-after header value or use default
            // This is a simplified implementation
            return TimeSpan.FromSeconds(60); // Default 1 minute for rate limits
        }

        private async Task StoreErrorPatternAsync(Exception exception, string context)
        {
            lock (_lockObject)
            {
                var key = $"{exception.GetType().Name}_{context}";
                if (_operationAttempts.ContainsKey(key))
                {
                    _operationAttempts[key]++;
                }
                else
                {
                    _operationAttempts[key] = 1;
                }

                // Log patterns that occur frequently
                if (_operationAttempts[key] > 5)
                {
                    _logger.LogWarning("Error pattern detected: {Pattern} occurred {Count} times", 
                        key, _operationAttempts[key]);
                }
            }
        }
    }
}
