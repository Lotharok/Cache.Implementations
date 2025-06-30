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

            await foreach (var key in server.KeysAsync(pattern: $"{this.namespacePrefix}:*").WithCancellation(cancellationToken))
            {
               await this.redis.KeyDeleteAsync(key);
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
         var keys = await this.GetKeysAsync(prefix, cancellationToken);
         foreach (var key in keys)
         {
            await this.redis.KeyDeleteAsync(key);
         }
      }

      /// <inheritdoc />
      public async Task RemoveByTagsAsync(string[] tags, CancellationToken cancellationToken = default)
      {
         foreach (var tag in tags.Distinct())
         {
            var tagKey = this.GetTagKey(tag);
            var keys = await this.redis.SetMembersAsync(tagKey);
            const int batchSize = 100;
            var keyBatches = keys
               .Select(key => (RedisKey)this.Namespaced(key.ToString()))
               .Batch(batchSize);

            foreach (var batch in keyBatches)
            {
               await this.redis.KeyDeleteAsync(batch.ToArray());
            }

            await this.redis.KeyDeleteAsync(tagKey);
         }
      }

      /// <inheritdoc />
      public async Task SetAsync(string key, TBuffer buffer, CacheExpirationOptions expiration, string[] tags, CancellationToken cancellationToken = default)
      {
         var expiry = this.GetRedisExpiry(expiration);
         RedisValue value = this.ConvertToRedisValue(buffer);
         await this.redis.StringSetAsync(this.Namespaced(key), value, expiry);
         foreach (var tag in tags.Distinct())
         {
            await this.redis.SetAddAsync(this.GetTagKey(tag), key);
         }
      }

      private string Namespaced(string key) => $"{this.namespacePrefix}:{key}";

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
