using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Component.Cache.Contract;
using Component.Cache.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Cache.InMemory
{
   /// <summary>
   /// A simple in-memory cache backend that implements the ICacheBackend interface.
   /// </summary>
   public class MemoryCacheBackend : ICacheBackend<object>
   {
      private readonly IMemoryCache memoryCache;
      private readonly ConcurrentDictionary<string, string> keysIndex = new ();
      private readonly ConcurrentDictionary<string, ConcurrentBag<string>> tagIndex = new ();

      /// <summary>
      /// Initializes a new instance of the <see cref="MemoryCacheBackend"/> class with the specified memory cache.
      /// </summary>
      /// <param name="memoryCache">Cache in memory Service.</param>
      public MemoryCacheBackend(IMemoryCache memoryCache)
      {
         this.memoryCache = memoryCache;
      }

      /// <inheritdoc />
      public CacheType CacheType => CacheType.InMemory;

      /// <inheritdoc />
      public Task ClearAsync(CancellationToken cancellationToken = default)
      {
         foreach (var key in this.keysIndex.Keys.ToList())
         {
            this.memoryCache.Remove(key);
            this.keysIndex.TryRemove(key, out _);
         }

         this.tagIndex.Clear();
         return Task.CompletedTask;
      }

      /// <inheritdoc />
      public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
      {
         var exists = this.memoryCache.TryGetValue(key, out _);
         return Task.FromResult(exists);
      }

      /// <inheritdoc />
      public Task<object?> GetAsync(string key, CancellationToken cancellationToken = default)
      {
         this.memoryCache.TryGetValue(key, out var value);
         return Task.FromResult(value);
      }

      /// <inheritdoc />
      public Task<IEnumerable<string>> GetKeysAsync(string? pattern = null, CancellationToken cancellationToken = default)
      {
         var keys = this.keysIndex.Keys;
         if (!string.IsNullOrWhiteSpace(pattern))
         {
            keys = keys
               .Where(k => k.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
               .ToList();
         }

         return Task.FromResult((IEnumerable<string>)keys);
      }

      /// <inheritdoc />
      public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
      {
         this.memoryCache.Remove(key);
         this.keysIndex.TryRemove(key, out _);
         return Task.CompletedTask;
      }

      /// <inheritdoc />
      public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
      {
         var keysToRemove = this.keysIndex.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
         foreach (var key in keysToRemove)
         {
            this.memoryCache.Remove(key);
            this.keysIndex.TryRemove(key, out _);
         }

         return Task.CompletedTask;
      }

      /// <inheritdoc />
      public Task RemoveByTagsAsync(string[] tags, CancellationToken cancellationToken = default)
      {
         var uniqueKeys = new HashSet<string>();
         foreach (var tag in tags.Distinct())
         {
            if (this.tagIndex.TryGetValue(tag, out var bag))
            {
               foreach (var key in bag)
               {
                  uniqueKeys.Add(key);
               }

               this.tagIndex.TryRemove(tag, out _);
            }
         }

         foreach (var key in uniqueKeys)
         {
            this.memoryCache.Remove(key);
            this.keysIndex.TryRemove(key, out _);
         }

         return Task.CompletedTask;
      }

      /// <inheritdoc />
      public Task SetAsync(string key, object buffer, CacheExpirationOptions expiration, string[] tags, CancellationToken cancellationToken = default)
      {
         var options = BuildMemoryCacheEntryOptions(expiration);
         this.memoryCache.Set(key, buffer, options);
         this.keysIndex[key] = key;
         foreach (var tag in tags.Distinct())
         {
            var bag = this.tagIndex.GetOrAdd(tag, _ => new ConcurrentBag<string>());
            bag.Add(key);
         }

         return Task.CompletedTask;
      }

      private static MemoryCacheEntryOptions BuildMemoryCacheEntryOptions(CacheExpirationOptions expiration)
      {
         var options = new MemoryCacheEntryOptions();

         if (expiration.AbsoluteExpirationAt.HasValue)
         {
            options.AbsoluteExpiration = expiration.AbsoluteExpirationAt;
         }
         else if (expiration.AbsoluteExpirationRelativeToNow.HasValue)
         {
            options.AbsoluteExpirationRelativeToNow = expiration.AbsoluteExpirationRelativeToNow;
         }

         if (expiration.SlidingExpiration.HasValue)
         {
            options.SlidingExpiration = expiration.SlidingExpiration;
         }

         return options;
      }
   }
}
