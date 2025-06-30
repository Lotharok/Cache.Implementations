# MemoryCacheBackend

[🇪🇸 Español](Cache.InMemory.README.es.md) | [🇺🇸 English](../src/Cache.InMemory/README.md)

Una implementación de caché en memoria para el sistema Component.Cache que utiliza `Microsoft.Extensions.Caching.Memory` como backend de almacenamiento.

## 📋 Características

- **Caché en memoria**: Utiliza `IMemoryCache` para almacenamiento rápido y eficiente
- **Gestión de claves**: Indexación automática de todas las claves almacenadas
- **Sistema de etiquetas**: Soporte completo para etiquetado y búsquedas basadas en etiquetas
- **Operaciones asíncronas**: Todas las operaciones implementan el patrón async/await
- **Expiración flexible**: Soporte para expiración absoluta y deslizante
- **Thread-safe**: Implementación segura para entornos concurrentes
- **Coincidencia de patrones**: Capacidad de filtrar claves por prefijo
- **Invalidación de caché**: Múltiples estrategias para invalidación de caché

## 🚀 Instalación

### Dependencias Requeridas

```xml
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.6" />
```

### Registro en Contenedor DI

```csharp
// Usando Microsoft.Extensions.DependencyInjection
services.AddMemoryCache();
services.AddSingleton<ICacheBackend<object>, MemoryCacheBackend>();
```

## 💡 Uso Básico

### Configuración Inicial

```csharp
// Inyección de dependencias
public class MyService
{
    private readonly ICacheBackend<object> _cache;
    
    public MyService(ICacheBackend<object> cache)
    {
        _cache = cache;
    }
}
```

### Operaciones Básicas

```csharp
// Almacenar un valor
await _cache.SetAsync("user:123", userData, expiration, tags: new[] { "users", "active" });

// Obtener un valor
var user = await _cache.GetAsync("user:123");

// Verificar existencia
bool exists = await _cache.ExistsAsync("user:123");

// Eliminar un valor
await _cache.RemoveAsync("user:123");
```

### Gestión de Expiración

```csharp
// Expiración absoluta (relativa al momento actual)
var expiration = new CacheExpirationOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
};

// Expiración deslizante
var expiration = new CacheExpirationOptions
{
    SlidingExpiration = TimeSpan.FromMinutes(15)
};

// Expiración absoluta en fecha específica
var expiration = new CacheExpirationOptions
{
    AbsoluteExpirationAt = DateTimeOffset.Now.AddHours(2)
};

await _cache.SetAsync("key", value, expiration, tags);
```

### Búsqueda y Filtrado

```csharp
// Obtener todas las claves
var allKeys = await _cache.GetKeysAsync();

// Buscar claves por prefijo
var userKeys = await _cache.GetKeysAsync("user:");

// Eliminar por prefijo
await _cache.RemoveByPrefixAsync("temp:");
```

### Gestión Basada en Etiquetas

```csharp
// Almacenar con múltiples etiquetas
await _cache.SetAsync("product:123", product, expiration, 
    tags: new[] { "products", "category:electronics", "featured" });

// Eliminar todos los elementos con etiquetas específicas
await _cache.RemoveByTagsAsync(new[] { "products", "expired" });
```

## 🔧 Características Avanzadas

### Limpieza Completa del Caché

```csharp
// Eliminar todo el contenido del caché
await _cache.ClearAsync();
```

### Verificación de Tipo de Caché

```csharp
// Obtener el tipo de backend
var cacheType = _cache.CacheType; // Retorna CacheType.InMemory
```

## ⚡ Rendimiento y Consideraciones

### Ventajas
- **Velocidad**: Acceso extremadamente rápido al estar en memoria
- **Sin latencia de red**: Ideal para aplicaciones que requieren baja latencia
- **Integración nativa**: Utiliza la infraestructura de caché integrada de .NET

### Limitaciones
- **Memoria limitada**: Restringido por la memoria disponible del proceso
- **No persistente**: Los datos se pierden cuando la aplicación se reinicia
- **Un solo proceso**: No se comparte entre múltiples instancias de aplicación

### Recomendaciones de Uso

✅ **Ideal para:**
- Aplicaciones de un solo servidor
- Datos que cambian frecuentemente
- Caché de corta duración
- Aplicaciones que requieren máximo rendimiento

❌ **No recomendado para:**
- Aplicaciones distribuidas
- Datos que deben persistir entre reinicios
- Grandes volúmenes de datos
- Caché compartido entre múltiples procesos

## 🧪 Ejemplo Completo

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
        
        // Intentar obtener del caché
        var cachedUser = await _cache.GetAsync(cacheKey);
        if (cachedUser is User user)
        {
            return user;
        }
        
        // Si no está en caché, obtener de la base de datos
        user = await GetUserFromDatabaseAsync(userId);
        
        // Almacenar en caché con expiración de 30 minutos
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
        // Eliminar caché de usuario específico
        await _cache.RemoveByTagsAsync(new[] { $"user:{userId}" });
    }
    
    public async Task InvalidateAllUsersCacheAsync()
    {
        // Eliminar todo el caché de usuarios
        await _cache.RemoveByTagsAsync(new[] { "users" });
    }
}
```

## 🔍 Arquitectura Interna

### Componentes Principales

- **IMemoryCache**: Backend de almacenamiento proporcionado por Microsoft
- **keysIndex**: Índice de claves para búsquedas eficientes (`ConcurrentDictionary<string, string>`)
- **tagIndex**: Índice de etiquetas para agrupación (`ConcurrentDictionary<string, ConcurrentBag<string>>`)

### Seguridad de Hilos

La implementación es completamente thread-safe usando:
- `ConcurrentDictionary` para los índices
- `ConcurrentBag` para almacenar múltiples claves por etiqueta
- Operaciones atómicas en `IMemoryCache`

## 📊 Compatibilidad

- **.NET**: 6.0+
- **Microsoft.Extensions.Caching.Memory**: 9.0.6+
- **Plataformas**: Windows, Linux, macOS

## 🔄 Estrategias de Invalidación de Caché

### Eliminación de Clave Individual
```csharp
// Eliminar clave específica
await _cache.RemoveAsync("user:123");
```

### Eliminación Basada en Patrones
```csharp
// Eliminar todas las claves con prefijo específico
await _cache.RemoveByPrefixAsync("session:");
```

### Eliminación Basada en Etiquetas
```csharp
// Eliminar todos los elementos etiquetados como "temporary"
await _cache.RemoveByTagsAsync(new[] { "temporary" });

// Eliminar elementos con múltiples etiquetas
await _cache.RemoveByTagsAsync(new[] { "users", "inactive" });
```

### Limpieza Completa
```csharp
// Limpiar todo el caché
await _cache.ClearAsync();
```

## 🎯 Mejores Prácticas

### Nomenclatura Efectiva de Claves
```csharp
// Usar nomenclatura jerárquica
"user:profile:123"
"product:inventory:456"
"session:data:789"

// Incluir información de versión cuando sea necesario
"api:v1:user:123"
"config:v2:settings"
```

### Etiquetado Estratégico
```csharp
// Usar etiquetas descriptivas y consultables
await _cache.SetAsync("user:123", userData, expiration, 
    tags: new[] { 
        "users",           // Categoría general
        "active",          // Estado
        "premium",         // Tipo de usuario
        "region:us-east"   // Basado en ubicación
    });
```

### Gestión de Memoria
```csharp
// Configurar límites de memoria para IMemoryCache
services.Configure<MemoryCacheOptions>(options =>
{
    options.SizeLimit = 1024; // Establecer límite de tamaño
    options.CompactionPercentage = 0.25; // Compactar cuando esté 25% sobre el límite
});
```

## 🧪 Pruebas

### Ejemplo de Prueba Unitaria
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

Para más información sobre el sistema completo Component.Cache, consulta el [README principal](../README.md).