# Cache Backend Implementations

[🇪🇸 Español](docs/README.es.md) | [🇺🇸 English](README.md)

This repository contains specific cache backend implementations that implement the `ICacheBackend<TBuffer>` interface from the main [Component.Cache](https://github.com/Lotharok/Component.Cache) component.

## 🎯 Features

Both implementations provide:

- ✅ **Complete CRUD operations** (Get, Set, Remove, Exists)
- ✅ **Expiration management** with support for absolute and sliding expiration
- ✅ **Tag system** for grouping and batch removal
- ✅ **Pattern-based search** and prefix-based removal
- ✅ **Asynchronous operations** with `CancellationToken` support
- ✅ **Full cache clearing**
- ✅ **Key enumeration** with optional filtering

## 📦 Available Implementations

- **[Cache.InMemory](./src/Cache.InMemory/README.md)** - In-memory cache implementation using Microsoft's `IMemoryCache`.
- **[Cache.Redis](./src/Cache.Redis/README.md)** - Distributed cache implementation using Redis with StackExchange.Redis.

### Implementation Comparison

| Feature | In-Memory | Redis |
|---------|-----------|-------|
| **Cache Type** | Local | Distributed |
| **Persistence** | No | Yes |
| **Cross-process sharing** | No | Yes |
| **Performance** | Very High | High |
| **Memory required** | Local | Redis Server |
| **Setup complexity** | Low | Medium |

## Dependencies

### ChacBolay.Cache.InMemory
```xml
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.6" />
```

### ChacBolay.Cache.Redis
```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.41" />
```

## 🚀 Quick Start

### Installation

```bash
# For in-memory cache
dotnet add package ChacBolay.Cache.InMemory

# For Redis cache
dotnet add package ChacBolay.Cache.Redis
```

### Basic Usage

#### In-Memory Cache

```csharp
using Microsoft.Extensions.Caching.Memory;

var memoryCache = new MemoryCache(new MemoryCacheOptions());
var cacheBackend = new MemoryCacheBackend(memoryCache);

// Store a value
var expiration = new CacheExpirationOptions 
{ 
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) 
};
await cacheBackend.SetAsync("my-key", "my-value", expiration, new[] { "tag1", "tag2" });

// Retrieve a value
var value = await cacheBackend.GetAsync("my-key");
```

#### Redis Cache

```csharp
using StackExchange.Redis;

var connection = ConnectionMultiplexer.Connect("localhost:6379");
var cacheBackend = new RedisCacheBackend<string>("my-app", connection);

// Store a value
var expiration = new CacheExpirationOptions 
{ 
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) 
};
await cacheBackend.SetAsync("my-key", "my-value", expiration, new[] { "tag1", "tag2" });

// Retrieve a value
var value = await cacheBackend.GetAsync("my-key");
```

### Advanced Operations

#### Tag-based Management
```csharp
// Remove all entries with specific tags
await cacheBackend.RemoveByTagsAsync(new[] { "user:123", "session" });
```

#### Prefix-based Search and Removal
```csharp
// Get all keys starting with "user:"
var keys = await cacheBackend.GetKeysAsync("user:");

// Remove all entries with prefix "temp:"
await cacheBackend.RemoveByPrefixAsync("temp:");
```

#### Full Cache Clear
```csharp
// Clear entire cache
await cacheBackend.ClearAsync();
```

## Design Considerations

### In-Memory Cache (MemoryCacheBackend)
- **Advantages**: Maximum performance, no external dependencies
- **Disadvantages**: Limited to single process, data loss on restart
- **Ideal for**: Single-server applications, temporary data, high-frequency access

### Redis Cache (RedisCacheBackend)
- **Advantages**: Shared across processes/servers, persistence, scalability
- **Disadvantages**: Network latency, external dependency
- **Ideal for**: Distributed applications, shared data, high availability

## DI Container Configuration

### ASP.NET Core with In-Memory
```csharp
services.AddMemoryCache();
services.AddSingleton<ICacheBackend<object>, MemoryCacheBackend>();
```

### ASP.NET Core with Redis
```csharp
services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect("localhost:6379"));
services.AddSingleton<ICacheBackend<string>>(provider =>
    new RedisCacheBackend<string>("my-app", provider.GetRequiredService<IConnectionMultiplexer>()));
```

## Supported Buffer Types

### In-Memory
- Any object type (`object`)

### Redis
- `string` - Text strings
- `byte[]` - Binary data

## ⚡ Performance and Best Practices

1. **Use consistent key patterns** to facilitate search and cleanup
2. **Implement appropriate expiration policies** to prevent uncontrolled growth
3. **Group related entries with tags** for efficient removal
4. **Monitor memory usage** especially with in-memory cache
5. **Configure appropriate timeouts** for Redis operations

## 🏗️ Architecture Notes

### Thread Safety
- **MemoryCacheBackend**: Uses `ConcurrentDictionary` and `ConcurrentBag` for thread-safe operations
- **RedisCacheBackend**: Redis operations are inherently thread-safe

### Key Management
- **MemoryCacheBackend**: Maintains internal key and tag indexes for efficient lookups
- **RedisCacheBackend**: Uses namespace prefixes and Redis Sets for tag management

### Error Handling
- Both implementations handle connection issues gracefully
- Redis implementation skips disconnected or replica servers during operations
- Proper exception handling for unsupported buffer types

## Configuration Examples

### Redis Connection Options
```csharp
var configurationOptions = new ConfigurationOptions
{
    EndPoints = { "localhost:6379" },
    AbortOnConnectFail = false,
    ConnectTimeout = 5000,
    SyncTimeout = 5000
};
var connection = ConnectionMultiplexer.Connect(configurationOptions);
```

### Memory Cache Options
```csharp
var memoryCacheOptions = new MemoryCacheOptions
{
    SizeLimit = 1000,
    CompactionPercentage = 0.2
};
var memoryCache = new MemoryCache(memoryCacheOptions);
```

## Monitoring and Diagnostics

### Redis Monitoring
- Monitor Redis memory usage with `INFO memory`
- Track key count with namespace patterns
- Use Redis slow log for performance monitoring

### In-Memory Monitoring
- Monitor application memory usage
- Track cache hit/miss ratios if needed
- Implement custom metrics for cache operations

## Migration Between Implementations

```csharp
// Abstract factory pattern for easy switching
public interface ICacheBackendFactory<TBuffer>
{
    ICacheBackend<TBuffer> CreateCacheBackend();
}

public class RedisCacheBackendFactory : ICacheBackendFactory<string>
{
    private readonly IConnectionMultiplexer connection;
    private readonly string namespacePrefix;

    public RedisCacheBackendFactory(IConnectionMultiplexer connection, string namespacePrefix)
    {
        this.connection = connection;
        this.namespacePrefix = namespacePrefix;
    }

    public ICacheBackend<string> CreateCacheBackend()
    {
        return new RedisCacheBackend<string>(namespacePrefix, connection);
    }
}
```

## Testing

Both implementations support unit testing through dependency injection:

```csharp
// Test with in-memory cache
var memoryCache = new MemoryCache(new MemoryCacheOptions());
var backend = new MemoryCacheBackend(memoryCache);

// Test with Redis (using TestContainers or embedded Redis)
var connection = ConnectionMultiplexer.Connect("localhost:6379");
var backend = new RedisCacheBackend<string>("test", connection);
```
## 📖 Documentation

For detailed documentation of each implementation, see:

- [In-memory cache implementation](./src/Cache.InMemory/README.md)
- [Redis Implementation](./src/Cache.Redis/README.md)

## 🤝 Contributing

Contributions are welcome! Please read our contributing guidelines and submit pull requests for any improvements.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For issues and questions:
- Create an issue on GitHub
- Check the documentation