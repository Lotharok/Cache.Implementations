# Component.Cache - Redis Backend 🚀

[🇪🇸 Español](../../docs/Cache.Redis.README.es.md) | [🇺🇸 English](README.md)

A high-performance Redis cache backend implementation for the Component.Cache library, providing distributed caching capabilities with advanced features like tagging, pattern-based operations, and flexible expiration policies.

## 📋 Features

- **Distributed Caching**: Built on Redis for scalable, distributed cache scenarios
- **Generic Buffer Support**: Supports both `string` and `byte[]` buffer types
- **Namespace Isolation**: Automatic key prefixing to avoid collisions
- **Tag-based Operations**: Group and manage cache entries using tags
- **Pattern Matching**: Advanced key retrieval with pattern support
- **Flexible Expiration**: Multiple expiration strategies (absolute, relative)
- **Batch Operations**: Efficient bulk operations for better performance
- **Async/Await**: Full asynchronous API with cancellation token support
- **High Availability**: Handles Redis replica scenarios and connection issues

## 🚀 Installation

### Required Dependencies

```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.41" />
```

## ⚙️ Configuration

### Basic Setup

```csharp
using StackExchange.Redis;

// Configure Redis connection
var connectionString = "localhost:6379";
var connection = ConnectionMultiplexer.Connect(connectionString);

// Create Redis cache backend
var redisCache = new RedisCacheBackend<string>("myapp", connection);
```

### Advanced Configuration

```csharp
var configurationOptions = new ConfigurationOptions
{
    EndPoints = { "localhost:6379" },
    ConnectTimeout = 5000,
    SyncTimeout = 5000,
    AbortOnConnectFail = false,
    ConnectRetry = 3
};

var connection = ConnectionMultiplexer.Connect(configurationOptions);
var redisCache = new RedisCacheBackend<byte[]>("myapp", connection);
```

## 💡 Basic Usage

### 🔧 Basic Operations

```csharp
// Store a value
await redisCache.SetAsync(
    key: "user:123", 
    buffer: "John Doe",
    expiration: new CacheExpirationOptions 
    { 
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) 
    },
    tags: new[] { "users", "active" }
);

// Retrieve a value
var user = await redisCache.GetAsync("user:123");

// Check if key exists
var exists = await redisCache.ExistsAsync("user:123");

// Remove a value
await redisCache.RemoveAsync("user:123");
```

### 🔍 Advanced Operations

#### 🎯 Pattern-based Key Retrieval

```csharp
// Get all user keys
var userKeys = await redisCache.GetKeysAsync("user:");

// Get all keys (be careful in production)
var allKeys = await redisCache.GetKeysAsync();
```

#### 🏷️ Tag-based Operations

```csharp
// Store multiple entries with tags
await redisCache.SetAsync("user:123", "John", expiration, new[] { "users", "premium" });
await redisCache.SetAsync("user:456", "Jane", expiration, new[] { "users", "basic" });
await redisCache.SetAsync("product:789", "Laptop", expiration, new[] { "products", "electronics" });

// Remove all entries with specific tags
await redisCache.RemoveByTagsAsync(new[] { "users" }); // Removes user:123 and user:456
```

#### 🗂️ Prefix-based Removal

```csharp
// Remove all user entries
await redisCache.RemoveByPrefixAsync("user:");
```

#### 🧹 Cache Clearing

```csharp
// Clear all entries in the namespace
await redisCache.ClearAsync();
```

### ⏰ Expiration Strategies

#### 📅 Absolute Expiration (Specific Date)

```csharp
var expiration = new CacheExpirationOptions
{
    AbsoluteExpirationAt = DateTimeOffset.UtcNow.AddHours(24)
};

await redisCache.SetAsync("session:abc", sessionData, expiration, Array.Empty<string>());
```

#### ⏳ Relative Expiration (Time from Now)

```csharp
var expiration = new CacheExpirationOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
};

await redisCache.SetAsync("temp:xyz", tempData, expiration, Array.Empty<string>());
```

#### ♾️ No Expiration

```csharp
var expiration = new CacheExpirationOptions(); // No expiration set

await redisCache.SetAsync("config:settings", configData, expiration, Array.Empty<string>());
```

## 🗃️ Buffer Type Support

### 📝 String Buffer

```csharp
var stringCache = new RedisCacheBackend<string>("myapp", connection);

await stringCache.SetAsync("key1", "Hello World", expiration, tags);
var value = await stringCache.GetAsync("key1"); // Returns: "Hello World"
```

### 💾 Byte Array Buffer

```csharp
var byteCache = new RedisCacheBackend<byte[]>("myapp", connection);

var data = Encoding.UTF8.GetBytes("Binary data");
await byteCache.SetAsync("key1", data, expiration, tags);
var retrievedData = await byteCache.GetAsync("key1"); // Returns: byte[]
```

## 🌟 Best Practices

### 1️⃣ Namespace Organization

```csharp
// Use descriptive namespaces
var userCache = new RedisCacheBackend<string>("app:users", connection);
var sessionCache = new RedisCacheBackend<string>("app:sessions", connection);
var configCache = new RedisCacheBackend<string>("app:config", connection);
```

### 2️⃣ Tag Strategy

```csharp
// Use hierarchical tags for flexible management
var tags = new[] { "user", "user:active", "user:premium", "region:us-east" };
await cache.SetAsync("user:123", userData, expiration, tags);

// Remove by specific tag combinations
await cache.RemoveByTagsAsync(new[] { "user:premium" }); // Remove premium users only
await cache.RemoveByTagsAsync(new[] { "region:us-east" }); // Remove by region
```

### 3️⃣ Error Handling

```csharp
try
{
    await redisCache.SetAsync("key", "value", expiration, tags);
}
catch (RedisConnectionException ex)
{
    // Handle Redis connection issues
    logger.LogError(ex, "Redis connection failed");
    // Implement fallback strategy
}
catch (RedisTimeoutException ex)
{
    // Handle timeout scenarios
    logger.LogWarning(ex, "Redis operation timed out");
}
```

### 4️⃣ Cancellation Token Usage

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var result = await redisCache.GetAsync("key", cts.Token);
}
catch (OperationCanceledException)
{
    // Handle cancellation
}
```

## ⚡ Performance Considerations

### 📦 Batch Operations

The Redis backend automatically batches operations where possible:

```csharp
// Tag removal uses batching (100 keys per batch)
await redisCache.RemoveByTagsAsync(new[] { "bulk-import" });
```

### 🔌 Connection Pooling

Reuse `ConnectionMultiplexer` instances:

```csharp
// ✅ Good - Singleton pattern
public class CacheService
{
    private static readonly ConnectionMultiplexer Connection = 
        ConnectionMultiplexer.Connect("localhost:6379");
    
    private readonly RedisCacheBackend<string> cache = 
        new("myapp", Connection);
}

// ❌ Bad - Creating multiple connections
var cache1 = new RedisCacheBackend<string>("app", ConnectionMultiplexer.Connect("..."));
var cache2 = new RedisCacheBackend<string>("app", ConnectionMultiplexer.Connect("..."));
```

### 🗝️ Key Naming Conventions

```csharp
// Use consistent, hierarchical key naming
await cache.SetAsync("user:profile:123", userProfile, expiration, tags);
await cache.SetAsync("user:preferences:123", userPrefs, expiration, tags);
await cache.SetAsync("product:details:456", product, expiration, tags);
```

## 📊 Monitoring and Debugging

### 🔍 Key Pattern Analysis

```csharp
// Monitor key usage patterns
var allKeys = await redisCache.GetKeysAsync();
var keysByPrefix = allKeys.GroupBy(key => key.Split(':')[0]);

foreach (var group in keysByPrefix)
{
    Console.WriteLine($"Prefix: {group.Key}, Count: {group.Count()}");
}
```

### 📈 Cache Hit Rate Monitoring

```csharp
public class MonitoredCacheBackend<T> : ICacheBackend<T>
{
    private readonly ICacheBackend<T> innerCache;
    private long hits = 0;
    private long misses = 0;

    public async Task<T?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await innerCache.GetAsync(key, cancellationToken);
        if (result != null)
            Interlocked.Increment(ref hits);
        else
            Interlocked.Increment(ref misses);
        
        return result;
    }

    public double HitRate => hits + misses > 0 ? (double)hits / (hits + misses) : 0;
}
```

## 🔧 Troubleshooting

### ⚠️ Common Issues

1. **Connection Timeouts**
   ```csharp
   // Increase timeout values
   var config = new ConfigurationOptions
   {
       ConnectTimeout = 10000,
       SyncTimeout = 10000
   };
   ```

2. **Memory Usage**
   ```csharp
   // Monitor Redis memory usage
   var server = connection.GetServer("localhost:6379");
   var info = await server.InfoAsync("memory");
   ```

3. **Key Expiration Issues**
   ```csharp
   // Always check expiration logic
   var expiry = expiration.AbsoluteExpirationAt?.Subtract(DateTimeOffset.UtcNow);
   if (expiry <= TimeSpan.Zero)
   {
       // Handle already expired entries
   }
   ```

## 🛡️ Thread Safety

The Redis cache backend is thread-safe and can be used concurrently from multiple threads. The underlying `StackExchange.Redis` library handles connection pooling and thread safety internally.

## 📋 Requirements

- **.NET 6.0** or later
- **Redis 3.0** or later
- **StackExchange.Redis 2.8.41** or later

For more information about the complete Component.Cache system, check the [main README](../README.md).