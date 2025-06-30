# Implementaciones de Backend de Caché

[🇪🇸 Español](README.es.md) | [🇺🇸 English](../README.md)

Este repositorio contiene implementaciones específicas de backend de caché que implementan la interfaz `ICacheBackend<TBuffer>` del componente principal [Component.Cache](https://github.com/Lotharok/Component.Cache).

## 🎯 Características

Ambas implementaciones proporcionan:

- ✅ **Operaciones CRUD completas** (Get, Set, Remove, Exists)
- ✅ **Gestión de expiración** con soporte para expiración absoluta y deslizante
- ✅ **Sistema de etiquetas** para agrupación y eliminación por lotes
- ✅ **Búsqueda basada en patrones** y eliminación basada en prefijos
- ✅ **Operaciones asíncronas** con soporte para `CancellationToken`
- ✅ **Limpieza completa de caché**
- ✅ **Enumeración de claves** con filtrado opcional

## 📦 Implementaciones Disponibles

- **[Cache.InMemory](./src/Cache.InMemory/README.md)** - Implementación de caché en memoria usando `IMemoryCache` de Microsoft.
- **[Cache.Redis](./src/Cache.Redis/README.md)** - Implementación de caché distribuida usando Redis con StackExchange.Redis.

### Comparación de Implementaciones

| Característica | En Memoria | Redis |
|----------------|------------|-------|
| **Tipo de Caché** | Local | Distribuido |
| **Persistencia** | No | Sí |
| **Compartición entre procesos** | No | Sí |
| **Rendimiento** | Muy Alto | Alto |
| **Memoria requerida** | Local | Servidor Redis |
| **Complejidad de configuración** | Baja | Media |

## Dependencias

### ChacBolay.Cache.InMemory
```xml
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.6" />
```

### ChacBolay.Cache.Redis
```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.41" />
```

## 🚀 Inicio Rápido

### Instalación

```bash
# Para caché en memoria
dotnet add package ChacBolay.Cache.InMemory

# Para caché Redis
dotnet add package ChacBolay.Cache.Redis
```

### Uso Básico

#### Caché en Memoria

```csharp
using Microsoft.Extensions.Caching.Memory;

var memoryCache = new MemoryCache(new MemoryCacheOptions());
var cacheBackend = new MemoryCacheBackend(memoryCache);

// Almacenar un valor
var expiration = new CacheExpirationOptions 
{ 
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) 
};
await cacheBackend.SetAsync("my-key", "my-value", expiration, new[] { "tag1", "tag2" });

// Recuperar un valor
var value = await cacheBackend.GetAsync("my-key");
```

#### Caché Redis

```csharp
using StackExchange.Redis;

var connection = ConnectionMultiplexer.Connect("localhost:6379");
var cacheBackend = new RedisCacheBackend<string>("my-app", connection);

// Almacenar un valor
var expiration = new CacheExpirationOptions 
{ 
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) 
};
await cacheBackend.SetAsync("my-key", "my-value", expiration, new[] { "tag1", "tag2" });

// Recuperar un valor
var value = await cacheBackend.GetAsync("my-key");
```

### Operaciones Avanzadas

#### Gestión Basada en Etiquetas
```csharp
// Eliminar todas las entradas con etiquetas específicas
await cacheBackend.RemoveByTagsAsync(new[] { "user:123", "session" });
```

#### Búsqueda y Eliminación Basada en Prefijos
```csharp
// Obtener todas las claves que comienzan con "user:"
var keys = await cacheBackend.GetKeysAsync("user:");

// Eliminar todas las entradas con prefijo "temp:"
await cacheBackend.RemoveByPrefixAsync("temp:");
```

#### Limpieza Completa de Caché
```csharp
// Limpiar toda la caché
await cacheBackend.ClearAsync();
```

## Consideraciones de Diseño

### Caché en Memoria (MemoryCacheBackend)
- **Ventajas**: Máximo rendimiento, sin dependencias externas
- **Desventajas**: Limitado a un solo proceso, pérdida de datos al reiniciar
- **Ideal para**: Aplicaciones de un solo servidor, datos temporales, acceso de alta frecuencia

### Caché Redis (RedisCacheBackend)
- **Ventajas**: Compartido entre procesos/servidores, persistencia, escalabilidad
- **Desventajas**: Latencia de red, dependencia externa
- **Ideal para**: Aplicaciones distribuidas, datos compartidos, alta disponibilidad

## Configuración del Contenedor DI

### ASP.NET Core con Memoria
```csharp
services.AddMemoryCache();
services.AddSingleton<ICacheBackend<object>, MemoryCacheBackend>();
```

### ASP.NET Core con Redis
```csharp
services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect("localhost:6379"));
services.AddSingleton<ICacheBackend<string>>(provider =>
    new RedisCacheBackend<string>("my-app", provider.GetRequiredService<IConnectionMultiplexer>()));
```

## Tipos de Buffer Soportados

### En Memoria
- Cualquier tipo de objeto (`object`)

### Redis
- `string` - Cadenas de texto
- `byte[]` - Datos binarios

## ⚡ Rendimiento y Mejores Prácticas

1. **Usa patrones de claves consistentes** para facilitar la búsqueda y limpieza
2. **Implementa políticas de expiración apropiadas** para prevenir el crecimiento descontrolado
3. **Agrupa entradas relacionadas con etiquetas** para una eliminación eficiente
4. **Monitorea el uso de memoria** especialmente con caché en memoria
5. **Configura timeouts apropiados** para operaciones Redis

## 🏗️ Notas de Arquitectura

### Seguridad de Hilos
- **MemoryCacheBackend**: Usa `ConcurrentDictionary` y `ConcurrentBag` para operaciones thread-safe
- **RedisCacheBackend**: Las operaciones Redis son inherentemente thread-safe

### Gestión de Claves
- **MemoryCacheBackend**: Mantiene índices internos de claves y etiquetas para búsquedas eficientes
- **RedisCacheBackend**: Usa prefijos de namespace y Redis Sets para gestión de etiquetas

### Manejo de Errores
- Ambas implementaciones manejan problemas de conexión con elegancia
- La implementación Redis omite servidores desconectados o réplicas durante las operaciones
- Manejo apropiado de excepciones para tipos de buffer no soportados

## Ejemplos de Configuración

### Opciones de Conexión Redis
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

### Opciones de Caché en Memoria
```csharp
var memoryCacheOptions = new MemoryCacheOptions
{
    SizeLimit = 1000,
    CompactionPercentage = 0.2
};
var memoryCache = new MemoryCache(memoryCacheOptions);
```

## Monitoreo y Diagnósticos

### Monitoreo Redis
- Monitorea el uso de memoria Redis con `INFO memory`
- Rastrea el conteo de claves con patrones de namespace
- Usa el log lento de Redis para monitoreo de rendimiento

### Monitoreo en Memoria
- Monitorea el uso de memoria de la aplicación
- Rastrea las tasas de acierto/fallo de caché si es necesario
- Implementa métricas personalizadas para operaciones de caché

## Migración Entre Implementaciones

```csharp
// Patrón factory abstracto para cambio fácil
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

## Pruebas

Ambas implementaciones soportan pruebas unitarias a través de inyección de dependencias:

```csharp
// Prueba con caché en memoria
var memoryCache = new MemoryCache(new MemoryCacheOptions());
var backend = new MemoryCacheBackend(memoryCache);

// Prueba con Redis (usando TestContainers o Redis embebido)
var connection = ConnectionMultiplexer.Connect("localhost:6379");
var backend = new RedisCacheBackend<string>("test", connection);
```

## 📖 Documentación

Para documentación detallada de cada implementación, consulta:

- [Implementación de caché en memoria](./src/Cache.InMemory/README.md)
- [Implementación Redis](./src/Cache.Redis/README.md)

## 🤝 Contribuir

¡Las contribuciones son bienvenidas! Por favor lee nuestras pautas de contribución y envía pull requests para cualquier mejora.

## 📄 Licencia

Este proyecto está licenciado bajo la Licencia MIT - consulta el archivo LICENSE para más detalles.

## 🆘 Soporte

Para problemas y preguntas:
- Crea un issue en GitHub
- Revisa la documentación