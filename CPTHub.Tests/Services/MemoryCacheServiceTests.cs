using CPTHub.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace CPTHub.Tests.Services
{
    public class MemoryCacheServiceTests : IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<ILogger<MemoryCacheService>> _loggerMock;
        private readonly MemoryCacheService _cacheService;

        public MemoryCacheServiceTests()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _loggerMock = new Mock<ILogger<MemoryCacheService>>();
            _cacheService = new MemoryCacheService(_memoryCache, _loggerMock.Object);
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }

        [Fact]
        public async Task GetAsync_WhenKeyExists_ReturnsDeserializedValue()
        {
            // Arrange
            var key = "test-key";
            var testObject = new TestData { Id = 1, Name = "Test" };
            var serializedValue = JsonConvert.SerializeObject(testObject);
            _memoryCache.Set(key, serializedValue);

            // Act
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(testObject.Id);
            result.Name.Should().Be(testObject.Name);
        }

        [Fact]
        public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNull()
        {
            // Arrange
            var key = "non-existent-key";

            // Act
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_WhenCachedValueIsNotString_ReturnsDirectCast()
        {
            // Arrange
            var key = "test-key";
            var testObject = new TestData { Id = 1, Name = "Test" };
            _memoryCache.Set(key, testObject);

            // Act
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(testObject.Id);
            result.Name.Should().Be(testObject.Name);
        }

        [Fact]
        public async Task GetAsync_WhenDeserializationFails_LogsWarningAndReturnsNull()
        {
            // Arrange
            var key = "test-key";
            var invalidJson = "invalid-json-string";
            _memoryCache.Set(key, invalidJson);

            // Act
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to retrieve cached item with key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SetAsync_WithExplicitExpiration_CachesValueWithCorrectExpiration()
        {
            // Arrange
            var key = "test-key";
            var testObject = new TestData { Id = 1, Name = "Test" };
            var expiration = TimeSpan.FromMinutes(30);

            // Act
            await _cacheService.SetAsync(key, testObject, expiration);

            // Assert
            var cachedValue = _memoryCache.Get(key);
            cachedValue.Should().NotBeNull();
            
            var deserializedValue = JsonConvert.DeserializeObject<TestData>(cachedValue.ToString()!);
            deserializedValue!.Id.Should().Be(testObject.Id);
            deserializedValue.Name.Should().Be(testObject.Name);
        }

        [Fact]
        public async Task SetAsync_WithoutExpiration_CachesValueWithDefaultSlidingExpiration()
        {
            // Arrange
            var key = "test-key";
            var testObject = new TestData { Id = 1, Name = "Test" };

            // Act
            await _cacheService.SetAsync(key, testObject);

            // Assert
            var cachedValue = _memoryCache.Get(key);
            cachedValue.Should().NotBeNull();
            
            var deserializedValue = JsonConvert.DeserializeObject<TestData>(cachedValue.ToString()!);
            deserializedValue!.Id.Should().Be(testObject.Id);
            deserializedValue.Name.Should().Be(testObject.Name);
        }

        [Fact]
        public async Task SetAsync_LogsDebugMessage()
        {
            // Arrange
            var key = "test-key";
            var testObject = new TestData { Id = 1, Name = "Test" };
            var expiration = TimeSpan.FromMinutes(30);

            // Act
            await _cacheService.SetAsync(key, testObject, expiration);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cached item with key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_WhenKeyExists_RemovesValueFromCache()
        {
            // Arrange
            var key = "test-key";
            var testObject = new TestData { Id = 1, Name = "Test" };
            _memoryCache.Set(key, testObject);

            // Act
            await _cacheService.RemoveAsync(key);

            // Assert
            var cachedValue = _memoryCache.Get(key);
            cachedValue.Should().BeNull();
        }

        [Fact]
        public async Task RemoveAsync_LogsDebugMessage()
        {
            // Arrange
            var key = "test-key";
            var testObject = new TestData { Id = 1, Name = "Test" };
            _memoryCache.Set(key, testObject);

            // Act
            await _cacheService.RemoveAsync(key);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Removed cached item with key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ClearAsync_CompactsMemoryCache()
        {
            // Arrange
            var key1 = "test-key-1";
            var key2 = "test-key-2";
            var testObject1 = new TestData { Id = 1, Name = "Test1" };
            var testObject2 = new TestData { Id = 2, Name = "Test2" };
            
            _memoryCache.Set(key1, testObject1);
            _memoryCache.Set(key2, testObject2);

            // Act
            await _cacheService.ClearAsync();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache cleared")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExistsAsync_WhenKeyExists_ReturnsTrue()
        {
            // Arrange
            var key = "test-key";
            var testObject = new TestData { Id = 1, Name = "Test" };
            _memoryCache.Set(key, testObject);

            // Act
            var result = await _cacheService.ExistsAsync(key);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_WhenKeyDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var key = "non-existent-key";

            // Act
            var result = await _cacheService.ExistsAsync(key);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetAsync_SetAsync_RemoveAsync_IntegrationTest()
        {
            // Arrange
            var key = "integration-test-key";
            var testObject = new TestData { Id = 42, Name = "Integration Test" };

            // Act & Assert - Set
            await _cacheService.SetAsync(key, testObject);
            var exists = await _cacheService.ExistsAsync(key);
            exists.Should().BeTrue();

            // Act & Assert - Get
            var retrievedObject = await _cacheService.GetAsync<TestData>(key);
            retrievedObject.Should().NotBeNull();
            retrievedObject!.Id.Should().Be(testObject.Id);
            retrievedObject.Name.Should().Be(testObject.Name);

            // Act & Assert - Remove
            await _cacheService.RemoveAsync(key);
            var existsAfterRemoval = await _cacheService.ExistsAsync(key);
            existsAfterRemoval.Should().BeFalse();

            var retrievedAfterRemoval = await _cacheService.GetAsync<TestData>(key);
            retrievedAfterRemoval.Should().BeNull();
        }

        // Test data class for testing
        public class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
