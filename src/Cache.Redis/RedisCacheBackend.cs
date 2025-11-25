using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Component.Cache.Contract;
using Component.Cache.Models;
using StackExchange.Redis;

namespace Cache.Redis
{
   /// <summary>
   /// RedisCacheBackend is a class that implements the ICacheBackend interface for Redis.
   /// </summary>
   /// <typeparam name="TBuffer">Data type.</typeparam>
   public class RedisCacheBackend<TBuffer> : ICacheBackend<TBuffer>
   {
      private const int BatchSize = 1000;
      private readonly IDatabase redis;
      private readonly string namespacePrefix;

      /// <summary>
      /// Initializes a new instance of the <see cref="RedisCacheBackend{TBuffer}"/> class with the specified Redis connection.
      /// </summary>
      /// <param name="namespacePrefix">Redis prefix namespace from keys.</param>
      /// <param name="connection">Redis connection.</param>
      public RedisCacheBackend(string namespacePrefix, IConnectionMultiplexer connection)
      {
         if (string.IsNullOrWhiteSpace(namespacePrefix))
         {
            throw new ArgumentNullException(nameof(namespacePrefix), "Namespace prefix cannot be null or empty.");
         }

         this.redis = connection.GetDatabase();
         this.namespacePrefix = namespacePrefix;
      }

      /// <inheritdoc />
      public CacheType CacheType => CacheType.Distributed;

      /// <inheritdoc />
      public async Task ClearAsync(CancellationToken cancellationToken = default)
      {
         var endpoints = this.redis.Multiplexer.GetEndPoints();
         foreach (var endpoint in endpoints)
         {
            var server = this.redis.Multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
               continue;
            }

            var batch = new List<RedisKey>(BatchSize);
            await foreach (var key in server.KeysAsync(pattern: $"{{{this.namespacePrefix}}}:*").WithCancellation(cancellationToken))
            {
               batch.Add(key);
               if (batch.Count >= BatchSize)
               {
                  await this.redis.KeyDeleteAsync(batch.ToArray());
                  batch.Clear();
               }
            }

            if (batch.Count > 0)
            {
               await this.redis.KeyDeleteAsync(batch.ToArray());
            }
         }
      }

      /// <inheritdoc />
      public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
      {
         return await this.redis.KeyExistsAsync(this.Namespaced(key));
      }

      /// <inheritdoc />
      public async Task<TBuffer?> GetAsync(string key, CancellationToken cancellationToken = default)
      {
         var value = await this.redis.StringGetAsync(this.Namespaced(key));
         if (!value.HasValue)
         {
            return default;
         }

         return this.ConvertFromRedisValue(value);
      }

      /// <inheritdoc />
      public async Task<IEnumerable<string>> GetKeysAsync(string? pattern = null, CancellationToken cancellationToken = default)
      {
         var keys = new List<string>();
         var redisPattern = string.IsNullOrWhiteSpace(pattern) ? "*" : $"{pattern}*";
         var endpoints = this.redis.Multiplexer.GetEndPoints();
         foreach (var endpoint in endpoints)
         {
            var server = this.redis.Multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
               continue;
            }

            await foreach (var key in server.KeysAsync(pattern: this.Namespaced(redisPattern)).WithCancellation(cancellationToken))
            {
               keys.Add(key.ToString());
            }
         }

         return keys;
      }

      /// <inheritdoc />
      public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
      {
         await this.redis.KeyDeleteAsync(this.Namespaced(key));
      }

      /// <inheritdoc />
      public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
      {
         if (string.IsNullOrWhiteSpace(prefix))
         {
            return;
         }

         var cleanPrefix = prefix.TrimEnd('*');
         if (string.IsNullOrWhiteSpace(cleanPrefix))
         {
            return;
         }

         var pattern = $"{this.Namespaced(cleanPrefix)}*";
         var batch = new List<RedisKey>(BatchSize);
         var endpoints = this.redis.Multiplexer.GetEndPoints();
         foreach (var endpoint in endpoints)
         {
            var server = this.redis.Multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
               continue;
            }

            await foreach (var key in server.KeysAsync(pattern: $"{pattern}*")
                              .WithCancellation(cancellationToken))
            {
               batch.Add(key);
               if (batch.Count >= BatchSize)
               {
                  await this.redis.KeyDeleteAsync(batch.ToArray());
                  batch.Clear();
               }
            }
         }

         if (batch.Count > 0)
         {
            await this.redis.KeyDeleteAsync(batch.ToArray());
         }
      }

      /// <inheritdoc />
      public async Task RemoveByTagsAsync(string[] tags, CancellationToken cancellationToken = default)
      {
         foreach (var tag in tags.Distinct())
         {
            var tagKey = this.GetTagKey(tag);
            var keys = await this.redis.SetMembersAsync(tagKey);

            if (keys.Length > 0)
            {
               var namespacedKeys = keys
                  .Select(key => (RedisKey)this.Namespaced(key.ToString()))
                  .ToArray();

               await this.redis.KeyDeleteAsync(namespacedKeys);
            }

            await this.redis.KeyDeleteAsync(tagKey);
         }
      }

      /// <inheritdoc />
      public async Task SetAsync(string key, TBuffer buffer, CacheExpirationOptions expiration, string[] tags, CancellationToken cancellationToken = default)
      {
         var expiry = this.GetRedisExpiry(expiration);
         RedisValue value = this.ConvertToRedisValue(buffer);
         var tasks = new List<Task>(1 + tags.Length);
         tasks.Add(this.redis.StringSetAsync(this.Namespaced(key), value, expiry));
         foreach (var tag in tags.Distinct())
         {
            tasks.Add(this.redis.SetAddAsync(this.GetTagKey(tag), key));
         }

         await Task.WhenAll(tasks);
      }

      /// <summary>
      /// Genera una key con hash tag para garantizar que todas las keys del mismo namespace
      /// estén en el mismo slot en Redis Cluster. En Standalone, los hash tags son ignorados sin impacto.
      /// </summary>
      private string Namespaced(string key) => $"{{{this.namespacePrefix}}}:{key}";

      /// <summary>
      /// Genera la key que almacena el SET de keys asociadas a un tag.
      /// </summary>
      private string GetTagKey(string tag) => this.Namespaced($"tag:{tag}");

      private TimeSpan? GetRedisExpiry(CacheExpirationOptions expiration)
      {
         if (expiration.AbsoluteExpirationAt.HasValue)
         {
            return expiration.AbsoluteExpirationAt.Value - DateTimeOffset.UtcNow;
         }

         if (expiration.AbsoluteExpirationRelativeToNow.HasValue)
         {
            return expiration.AbsoluteExpirationRelativeToNow;
         }

         return null;
      }

      private RedisValue ConvertToRedisValue(TBuffer buffer)
      {
         return buffer switch
         {
            string s => s,
            byte[] b => b,
            _ => throw new InvalidOperationException($"Unsupported buffer type {typeof(TBuffer).Name} for Redis")
         };
      }

      private TBuffer ConvertFromRedisValue(RedisValue value)
      {
         if (typeof(TBuffer) == typeof(string))
         {
            return (TBuffer)(object)value.ToString();
         }

         if (typeof(TBuffer) == typeof(byte[]))
         {
            byte[] bytes = value!;
            return (TBuffer)(object)bytes;
         }

         throw new InvalidOperationException($"Unsupported buffer type {typeof(TBuffer).Name} for Redis");
      }
   }
}
