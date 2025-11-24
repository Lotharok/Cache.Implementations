using System;
using System.Threading.Tasks;
using Cache.InMemory;
using Component.Cache.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cache.Tests
{
   public class MemoryCacheBackendTests
   {
      private readonly MemoryCache memoryCache;
      private readonly MemoryCacheBackend backend;

      public MemoryCacheBackendTests()
      {
         var options = Options.Create(new MemoryCacheOptions());
         this.memoryCache = new MemoryCache(options);
         this.backend = new MemoryCacheBackend(this.memoryCache);
      }

      [Fact]
      public async Task SetAsync_ShouldStoreValue_AndTrackKeyAndTags()
      {
         var key = "test-key";
         var value = "test-value";
         var tags = new[] { "tag1", "tag2" };
         var expiration = new CacheExpirationOptions();

         await this.backend.SetAsync(key, value, expiration, tags);

         var result = await this.backend.GetAsync(key);
         Assert.Equal(value, result);

         var keys = await this.backend.GetKeysAsync();
         Assert.Contains(key, keys);
      }

      [Fact]
      public async Task SetAsync_WithAbsoluteExpiration_ShouldExpireAfterTime()
      {
         var key = "absolute-key";
         var value = "value";
         var expiration = new CacheExpirationOptions
         {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(300),
         };

         await this.backend.SetAsync(key, value, expiration, new string[0]);
         Assert.True(await this.backend.ExistsAsync(key));
         await Task.Delay(500);
         Assert.False(await this.backend.ExistsAsync(key));
      }

      [Fact]
      public async Task SetAsync_WithAbsoluteExpirationRelativeToNow_ShouldExpireAfterTime()
      {
         var key = "relative-key";
         var value = "value";
         var expiration = new CacheExpirationOptions
         {
            AbsoluteExpirationAt = DateTimeOffset.UtcNow.AddMilliseconds(200),
            SlidingExpiration = null,
         };

         await this.backend.SetAsync(key, value, expiration, new string[0]);
         Assert.True(await this.backend.ExistsAsync(key));
         await Task.Delay(400);
         Assert.False(await this.backend.ExistsAsync(key));
      }

      [Fact]
      public async Task SetAsync_WithSlidingExpiration_ShouldExtendOnAccess()
      {
         var key = "sliding-key";
         var value = "value";
         var expiration = new CacheExpirationOptions
         {
            SlidingExpiration = TimeSpan.FromMilliseconds(300),
         };

         await this.backend.SetAsync(key, value, expiration, new string[0]);
         Assert.True(await this.backend.ExistsAsync(key));

         // Act: acceso antes de que expire
         await Task.Delay(200);
         _ = await this.backend.GetAsync(key); // mantenerlo vivo
         await Task.Delay(200);
         _ = await this.backend.GetAsync(key); // volver a mantenerlo vivo

         // Assert: aún debe existir
         Assert.True(await this.backend.ExistsAsync(key));

         // Esperar más allá del tiempo de sliding sin acceder
         await Task.Delay(400);
         Assert.False(await this.backend.ExistsAsync(key));
      }

      [Fact]
      public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
      {
         var result = await this.backend.GetAsync("non-existent-key");
         Assert.Null(result);
      }

      [Fact]
      public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
      {
         var key = "exists-key";
         await this.backend.SetAsync(key, "some-value", new CacheExpirationOptions(), new string[0]);

         var exists = await this.backend.ExistsAsync(key);
         Assert.True(exists);
         Assert.Equal(CacheType.InMemory, this.backend.CacheType);
      }

      [Fact]
      public async Task ExistsAsync_ShouldReturnFalse_WhenKeyMissing()
      {
         var exists = await this.backend.ExistsAsync("missing-key");
         Assert.False(exists);
      }

      [Fact]
      public async Task RemoveAsync_ShouldDeleteKey()
      {
         var key = "key-to-remove";
         await this.backend.SetAsync(key, "value", new CacheExpirationOptions(), new string[0]);
         await this.backend.RemoveAsync(key);

         var result = await this.backend.GetAsync(key);
         Assert.Null(result);
      }

      [Fact]
      public async Task RemoveByPrefixAsync_ShouldRemoveMatchingKeys()
      {
         await this.backend.SetAsync("prefix-one", "a", new CacheExpirationOptions(), new string[0]);
         await this.backend.SetAsync("prefix-two", "b", new CacheExpirationOptions(), new string[0]);
         await this.backend.SetAsync("other", "c", new CacheExpirationOptions(), new string[0]);

         await this.backend.RemoveByPrefixAsync("prefix");

         Assert.Null(await this.backend.GetAsync("prefix-one"));
         Assert.Null(await this.backend.GetAsync("prefix-two"));
         Assert.NotNull(await this.backend.GetAsync("other"));
      }

      [Fact]
      public async Task RemoveByTagsAsync_ShouldRemoveAllKeysWithMatchingTags()
      {
         await this.backend.SetAsync("key1", "a", new CacheExpirationOptions(), new[] { "tagX" });
         await this.backend.SetAsync("key2", "b", new CacheExpirationOptions(), new[] { "tagX", "tagY" });
         await this.backend.SetAsync("key3", "c", new CacheExpirationOptions(), new[] { "tagZ" });

         await this.backend.RemoveByTagsAsync(new[] { "tagX" });

         Assert.Null(await this.backend.GetAsync("key1"));
         Assert.Null(await this.backend.GetAsync("key2"));
         Assert.NotNull(await this.backend.GetAsync("key3"));
      }

      [Fact]
      public async Task ClearAsync_ShouldRemoveAllCachedItems()
      {
         await this.backend.SetAsync("k1", "v1", new CacheExpirationOptions(), new string[0]);
         await this.backend.SetAsync("k2", "v2", new CacheExpirationOptions(), new string[0]);
         await this.backend.SetAsync("k3", "v3", new CacheExpirationOptions(), new string[0]);

         await this.backend.ClearAsync();

         Assert.Null(await this.backend.GetAsync("k1"));
         Assert.Null(await this.backend.GetAsync("k2"));
         Assert.Null(await this.backend.GetAsync("k3"));
      }

      [Fact]
      public async Task GetKeysAsync_ShouldReturnKeysFilteredByPattern()
      {
         await this.backend.SetAsync("abc-1", "v1", new CacheExpirationOptions(), new string[0]);
         await this.backend.SetAsync("abc-2", "v2", new CacheExpirationOptions(), new string[0]);
         await this.backend.SetAsync("def-1", "v3", new CacheExpirationOptions(), new string[0]);

         var keys = await this.backend.GetKeysAsync("abc");

         Assert.Contains("abc-1", keys);
         Assert.Contains("abc-2", keys);
         Assert.DoesNotContain("def-1", keys);
      }

      [Fact]
      public async Task GetKeysAsync_ShouldNotReturnExpiredKeys()
      {
         var key = "expired-key";
         var value = "value";
         var expiration = new CacheExpirationOptions
         {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(100),
         };

         await this.backend.SetAsync(key, value, expiration, new string[0]);

         // Verify key exists initially
         var keysInitial = await this.backend.GetKeysAsync();
         Assert.Contains(key, keysInitial);

         // Wait for expiration
         await Task.Delay(200);

         // Force eviction
         this.memoryCache.Compact(1.0);

         // Give a small window for the callback to execute on the threadpool
         await Task.Delay(50);

         // Verify key is gone from index
         var keysAfter = await this.backend.GetKeysAsync();
         Assert.DoesNotContain(key, keysAfter);
      }
   }
}
