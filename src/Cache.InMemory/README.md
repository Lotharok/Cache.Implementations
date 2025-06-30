# MemoryCacheBackend

[🇪🇸 Español](../../docs/Cache.InMemory.README.es.md) | [🇺🇸 English](README.md)

An in-memory cache implementation for the Component.Cache system that uses `Microsoft.Extensions.Caching.Memory` as the storage backend.

## 📋 Features

- **In-memory caching**: Uses `IMemoryCache` for fast and efficient storage
- **Key management**: Automatic indexing of all stored keys
- **Tagging system**: Full support for tagging and tag-based searches
- **Asynchronous operations**: All operations implement the async/await pattern
- **Flexible expiration**: Support for absolute and sliding expiration
- **Thread-safe**: Safe implementation for concurrent environments
- **Pattern matching**: Ability to filter keys by prefix
- **Cache invalidation**: Multiple strategies for cache invalidation

## 🚀 Installation

### Required Dependencies

```xml
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.6" />
```

### DI Container Registration

```csharp
// Using Microsoft.Extensions.DependencyInjection
services.AddMemoryCache();
services.AddSingleton<ICacheBackend<object>, MemoryCacheBackend>();
```

## 💡 Basic Usage

### Initial Setup

```csharp
// Dependency injection
public class MyService
{
    private readonly ICacheBackend<object> _cache;
    
    public MyService(ICacheBackend<object> cache)
    {
        _cache = cache;
    }
}
```

### Basic Operations

```csharp
// Store a value
await _cache.SetAsync("user:123", userData, expiration, tags: new[] { "users", "active" });

// Get a value
var user = await _cache.GetAsync("user:123");

// Check existence
bool exists = await _cache.ExistsAsync("user:123");

// Remove a value
await _cache.RemoveAsync("user:123");
```

### Expiration Management

```csharp
// Absolute expiration (relative to now)
var expiration = new CacheExpirationOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
};

// Sliding expiration
var expiration = new CacheExpirationOptions
{
    SlidingExpiration = TimeSpan.FromMinutes(15)
};

// Absolute expiration at specific date
var expiration = new CacheExpirationOptions
{
    AbsoluteExpirationAt = DateTimeOffset.Now.AddHours(2)
};

await _cache.SetAsync("key", value, expiration, tags);
```

### Search and Filtering

```csharp
// Get all keys
var allKeys = await _cache.GetKeysAsync();

// Search keys by prefix
var userKeys = await _cache.GetKeysAsync("user:");

// Remove by prefix
await _cache.RemoveByPrefixAsync("temp:");
```

### Tag-based Management

```csharp
// Store with multiple tags
await _cache.SetAsync("product:123", product, expiration, 
    tags: new[] { "products", "category:electronics", "featured" });

// Remove all items with specific tags
await _cache.RemoveByTagsAsync(new[] { "products", "expired" });
```

## 🔧 Advanced Features

### Complete Cache Cleanup

```csharp
// Remove all cache content
await _cache.ClearAsync();
```

### Cache Type Verification

```csharp
// Get the backend type
var cacheType = _cache.CacheType; // Returns CacheType.InMemory
```

## ⚡ Performance and Considerations

### Advantages
- **Speed**: Extremely fast access being in memory
- **No network latency**: Ideal for applications requiring low latency
- **Native integration**: Uses .NET's built-in caching infrastructure

### Limitations
- **Limited memory**: Restricted by available process memory
- **Non-persistent**: Data is lost when the application restarts
- **Single process**: Not shared between multiple application instances

### Usage Recommendations

✅ **Ideal for:**
- Single-server applications
- Frequently changing data
- Short-lived cache
- Applications requiring maximum performance

❌ **Not recommended for:**
- Distributed applications
- Data that must persist between restarts
- Large data volumes
- Shared cache between multiple processes

## 🧪 Complete Example

```csharp
public class UserService
{
    private readonly ICacheBackend<object> _cache;
    
    public UserService(ICacheBackend<object> cache)
    {
        _cache = cache;
    }
    
    public async Task<User> GetUserAsync(int userId)
    {
        var cacheKey = $"user:{userId}";
        
        // Try to get from cache
        var cachedUser = await _cache.GetAsync(cacheKey);
        if (cachedUser is User user)
        {
            return user;
        }
        
        // If not in cache, get from database
        user = await GetUserFromDatabaseAsync(userId);
        
        // Store in cache with 30-minute expiration
        var expiration = new CacheExpirationOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        };
        
        await _cache.SetAsync(cacheKey, user, expiration, 
            tags: new[] { "users", $"user:{userId}" });
        
        return user;
    }
    
    public async Task InvalidateUserCacheAsync(int userId)
    {
        // Remove specific user cache
        await _cache.RemoveByTagsAsync(new[] { $"user:{userId}" });
    }
    
    public async Task InvalidateAllUsersCacheAsync()
    {
        // Remove all user cache
        await _cache.RemoveByTagsAsync(new[] { "users" });
    }
}
```

## 🔍 Internal Architecture

### Main Components

- **IMemoryCache**: Storage backend provided by Microsoft
- **keysIndex**: Key index for efficient searches (`ConcurrentDictionary<string, string>`)
- **tagIndex**: Tag index for grouping (`ConcurrentDictionary<string, ConcurrentBag<string>>`)

### Thread Safety

The implementation is completely thread-safe using:
- `ConcurrentDictionary` for indexes
- `ConcurrentBag` to store multiple keys per tag
- Atomic operations in `IMemoryCache`

## 📊 Compatibility

- **.NET**: 6.0+
- **Microsoft.Extensions.Caching.Memory**: 9.0.6+
- **Platforms**: Windows, Linux, macOS

## 🔄 Cache Invalidation Strategies

### Individual Key Removal
```csharp
// Remove specific key
await _cache.RemoveAsync("user:123");
```

### Pattern-based Removal
```csharp
// Remove all keys with specific prefix
await _cache.RemoveByPrefixAsync("session:");
```

### Tag-based Removal
```csharp
// Remove all items tagged as "temporary"
await _cache.RemoveByTagsAsync(new[] { "temporary" });

// Remove items with multiple tags
await _cache.RemoveByTagsAsync(new[] { "users", "inactive" });
```

### Complete Cleanup
```csharp
// Clear entire cache
await _cache.ClearAsync();
```

## 🎯 Best Practices

### Effective Key Naming
```csharp
// Use hierarchical naming
"user:profile:123"
"product:inventory:456"
"session:data:789"

// Include version information when needed
"api:v1:user:123"
"config:v2:settings"
```

### Strategic Tagging
```csharp
// Use descriptive and queryable tags
await _cache.SetAsync("user:123", userData, expiration, 
    tags: new[] { 
        "users",           // General category
        "active",          // Status
        "premium",         // User type
        "region:us-east"   // Location-based
    });
```

### Memory Management
```csharp
// Configure memory limits for IMemoryCache
services.Configure<MemoryCacheOptions>(options =>
{
    options.SizeLimit = 1024; // Set size limit
    options.CompactionPercentage = 0.25; // Compact when 25% over limit
});
```

## 🧪 Testing

### Unit Test Example
```csharp
[Test]
public async Task SetAsync_ShouldStoreAndRetrieveValue()
{
    // Arrange
    var memoryCache = new MemoryCache(new MemoryCacheOptions());
    var cacheBackend = new MemoryCacheBackend(memoryCache);
    var expiration = new CacheExpirationOptions();
    var testData = "test value";
    
    // Act
    await cacheBackend.SetAsync("test-key", testData, expiration, Array.Empty<string>());
    var result = await cacheBackend.GetAsync("test-key");
    
    // Assert
    Assert.AreEqual(testData, result);
}
```

For more information about the complete Component.Cache system, check the [main README](../README.md).