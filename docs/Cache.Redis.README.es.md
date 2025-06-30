# Component.Cache - Backend Redis 🚀

[🇪🇸 Español](Cache.Redis.README.es.md) | [🇺🇸 English](../src/Cache.Redis/README.md)

Una implementación de backend de caché Redis de alto rendimiento para la librería Component.Cache, proporcionando capacidades de caché distribuido con características avanzadas como etiquetado, operaciones basadas en patrones y políticas de expiración flexibles.

## 📋 Características

- **Caché Distribuido**: Construido sobre Redis para escenarios de caché distribuido y escalable
- **Soporte de Buffer Genérico**: Soporta tipos de buffer tanto `string` como `byte[]`
- **Aislamiento de Namespace**: Prefijado automático de claves para evitar colisiones
- **Operaciones Basadas en Etiquetas**: Agrupa y gestiona entradas de caché usando etiquetas
- **Coincidencia de Patrones**: Recuperación avanzada de claves con soporte de patrones
- **Expiración Flexible**: Múltiples estrategias de expiración (absoluta, relativa)
- **Operaciones por Lotes**: Operaciones masivas eficientes para mejor rendimiento
- **Async/Await**: API completamente asíncrona con soporte de token de cancelación
- **Alta Disponibilidad**: Maneja escenarios de réplicas Redis y problemas de conexión

## 🚀 Instalación

### Dependencias Requeridas

```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.41" />
```

## ⚙️ Configuración

### Configuración Básica

```csharp
using StackExchange.Redis;

// Configurar conexión Redis
var connectionString = "localhost:6379";
var connection = ConnectionMultiplexer.Connect(connectionString);

// Crear backend de caché Redis
var redisCache = new RedisCacheBackend<string>("myapp", connection);
```

### Configuración Avanzada

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

## 💡 Uso Básico

### 🔧 Operaciones Básicas

```csharp
// Almacenar un valor
await redisCache.SetAsync(
    key: "user:123", 
    buffer: "John Doe",
    expiration: new CacheExpirationOptions 
    { 
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) 
    },
    tags: new[] { "users", "active" }
);

// Recuperar un valor
var user = await redisCache.GetAsync("user:123");

// Verificar si la clave existe
var exists = await redisCache.ExistsAsync("user:123");

// Eliminar un valor
await redisCache.RemoveAsync("user:123");
```

### 🔍 Operaciones Avanzadas

#### 🎯 Recuperación de Claves Basada en Patrones

```csharp
// Obtener todas las claves de usuario
var userKeys = await redisCache.GetKeysAsync("user:");

// Obtener todas las claves (ten cuidado en producción)
var allKeys = await redisCache.GetKeysAsync();
```

#### 🏷️ Operaciones Basadas en Etiquetas

```csharp
// Almacenar múltiples entradas con etiquetas
await redisCache.SetAsync("user:123", "John", expiration, new[] { "users", "premium" });
await redisCache.SetAsync("user:456", "Jane", expiration, new[] { "users", "basic" });
await redisCache.SetAsync("product:789", "Laptop", expiration, new[] { "products", "electronics" });

// Eliminar todas las entradas con etiquetas específicas
await redisCache.RemoveByTagsAsync(new[] { "users" }); // Elimina user:123 y user:456
```

#### 🗂️ Eliminación Basada en Prefijos

```csharp
// Eliminar todas las entradas de usuario
await redisCache.RemoveByPrefixAsync("user:");
```

#### 🧹 Limpieza de Caché

```csharp
// Limpiar todas las entradas en el namespace
await redisCache.ClearAsync();
```

### ⏰ Estrategias de Expiración

#### 📅 Expiración Absoluta (Fecha Específica)

```csharp
var expiration = new CacheExpirationOptions
{
    AbsoluteExpirationAt = DateTimeOffset.UtcNow.AddHours(24)
};

await redisCache.SetAsync("session:abc", sessionData, expiration, Array.Empty<string>());
```

#### ⏳ Expiración Relativa (Tiempo desde Ahora)

```csharp
var expiration = new CacheExpirationOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
};

await redisCache.SetAsync("temp:xyz", tempData, expiration, Array.Empty<string>());
```

#### ♾️ Sin Expiración

```csharp
var expiration = new CacheExpirationOptions(); // Sin expiración establecida

await redisCache.SetAsync("config:settings", configData, expiration, Array.Empty<string>());
```

## 🗃️ Soporte de Tipos de Buffer

### 📝 Buffer de String

```csharp
var stringCache = new RedisCacheBackend<string>("myapp", connection);

await stringCache.SetAsync("key1", "Hello World", expiration, tags);
var value = await stringCache.GetAsync("key1"); // Retorna: "Hello World"
```

### 💾 Buffer de Array de Bytes

```csharp
var byteCache = new RedisCacheBackend<byte[]>("myapp", connection);

var data = Encoding.UTF8.GetBytes("Binary data");
await byteCache.SetAsync("key1", data, expiration, tags);
var retrievedData = await byteCache.GetAsync("key1"); // Retorna: byte[]
```

## 🌟 Mejores Prácticas

### 1️⃣ Organización de Namespaces

```csharp
// Usar namespaces descriptivos
var userCache = new RedisCacheBackend<string>("app:users", connection);
var sessionCache = new RedisCacheBackend<string>("app:sessions", connection);
var configCache = new RedisCacheBackend<string>("app:config", connection);
```

### 2️⃣ Estrategia de Etiquetas

```csharp
// Usar etiquetas jerárquicas para gestión flexible
var tags = new[] { "user", "user:active", "user:premium", "region:us-east" };
await cache.SetAsync("user:123", userData, expiration, tags);

// Eliminar por combinaciones específicas de etiquetas
await cache.RemoveByTagsAsync(new[] { "user:premium" }); // Eliminar solo usuarios premium
await cache.RemoveByTagsAsync(new[] { "region:us-east" }); // Eliminar por región
```

### 3️⃣ Manejo de Errores

```csharp
try
{
    await redisCache.SetAsync("key", "value", expiration, tags);
}
catch (RedisConnectionException ex)
{
    // Manejar problemas de conexión Redis
    logger.LogError(ex, "Redis connection failed");
    // Implementar estrategia de respaldo
}
catch (RedisTimeoutException ex)
{
    // Manejar escenarios de timeout
    logger.LogWarning(ex, "Redis operation timed out");
}
```

### 4️⃣ Uso de Token de Cancelación

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var result = await redisCache.GetAsync("key", cts.Token);
}
catch (OperationCanceledException)
{
    // Manejar cancelación
}
```

## ⚡ Consideraciones de Rendimiento

### 📦 Operaciones por Lotes

El backend Redis agrupa operaciones automáticamente donde es posible:

```csharp
// La eliminación por etiquetas usa agrupación (100 claves por lote)
await redisCache.RemoveByTagsAsync(new[] { "bulk-import" });
```

### 🔌 Pool de Conexiones

Reutilizar instancias de `ConnectionMultiplexer`:

```csharp
// ✅ Bueno - Patrón Singleton
public class CacheService
{
    private static readonly ConnectionMultiplexer Connection = 
        ConnectionMultiplexer.Connect("localhost:6379");
    
    private readonly RedisCacheBackend<string> cache = 
        new("myapp", Connection);
}

// ❌ Malo - Crear múltiples conexiones
var cache1 = new RedisCacheBackend<string>("app", ConnectionMultiplexer.Connect("..."));
var cache2 = new RedisCacheBackend<string>("app", ConnectionMultiplexer.Connect("..."));
```

### 🗝️ Convenciones de Nomenclatura de Claves

```csharp
// Usar nomenclatura consistente y jerárquica
await cache.SetAsync("user:profile:123", userProfile, expiration, tags);
await cache.SetAsync("user:preferences:123", userPrefs, expiration, tags);
await cache.SetAsync("product:details:456", product, expiration, tags);
```

## 📊 Monitoreo y Depuración

### 🔍 Análisis de Patrones de Claves

```csharp
// Monitorear patrones de uso de claves
var allKeys = await redisCache.GetKeysAsync();
var keysByPrefix = allKeys.GroupBy(key => key.Split(':')[0]);

foreach (var group in keysByPrefix)
{
    Console.WriteLine($"Prefix: {group.Key}, Count: {group.Count()}");
}
```

### 📈 Monitoreo de Tasa de Aciertos de Caché

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

## 🔧 Solución de Problemas

### ⚠️ Problemas Comunes

1. **Timeouts de Conexión**
   ```csharp
   // Aumentar valores de timeout
   var config = new ConfigurationOptions
   {
       ConnectTimeout = 10000,
       SyncTimeout = 10000
   };
   ```

2. **Uso de Memoria**
   ```csharp
   // Monitorear uso de memoria Redis
   var server = connection.GetServer("localhost:6379");
   var info = await server.InfoAsync("memory");
   ```

3. **Problemas de Expiración de Claves**
   ```csharp
   // Siempre verificar lógica de expiración
   var expiry = expiration.AbsoluteExpirationAt?.Subtract(DateTimeOffset.UtcNow);
   if (expiry <= TimeSpan.Zero)
   {
       // Manejar entradas ya expiradas
   }
   ```

## 🛡️ Seguridad de Hilos

El backend de caché Redis es thread-safe y puede ser usado concurrentemente desde múltiples hilos. La librería subyacente `StackExchange.Redis` maneja el pool de conexiones y la seguridad de hilos internamente.

## 📋 Requisitos

- **.NET 6.0** o posterior
- **Redis 3.0** o posterior
- **StackExchange.Redis 2.8.41** o posterior

Para más información sobre el sistema completo Component.Cache, consulta el [README principal](../README.md).