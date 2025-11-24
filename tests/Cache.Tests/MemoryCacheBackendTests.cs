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
      public async Task SetAsync_WithAbsoluteExpirationAt_ShouldExpireAfterTime()
      {
         var key = "absolute-at-key";
         var value = "value";
         var expiration = new CacheExpirationOptions
         {
            AbsoluteExpirationAt = DateTimeOffset.UtcNow.AddMilliseconds(200),
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

         // Acceso antes de que expire
         await Task.Delay(200);
         _ = await this.backend.GetAsync(key);
         await Task.Delay(200);
         _ = await this.backend.GetAsync(key);

         // Debe existir aún
         Assert.True(await this.backend.ExistsAsync(key));

         // Esperar más allá del tiempo de sliding sin acceder
         await Task.Delay(400);
         Assert.False(await this.backend.ExistsAsync(key));
      }

      [Fact]
      public async Task SetAsync_WithMultipleTags_ShouldIndexAllTags()
      {
         var key = "multi-tag-key";
         var value = "value";
         var tags = new[] { "tag1", "tag2", "tag3" };
         var expiration = new CacheExpirationOptions();

         await this.backend.SetAsync(key, value, expiration, tags);

         // Verificar que se removió por cada tag
         await this.backend.RemoveByTagsAsync(new[] { "tag2" });
         Assert.Null(await this.backend.GetAsync(key));
      }

      [Fact]
      public async Task SetAsync_WithDuplicateTags_ShouldHandleDistinctTags()
      {
         var key = "duplicate-tags-key";
         var value = "value";
         var tags = new[] { "tag1", "tag1", "tag2" };
         var expiration = new CacheExpirationOptions();

         await this.backend.SetAsync(key, value, expiration, tags);

         var result = await this.backend.GetAsync(key);
         Assert.Equal(value, result);
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
      public async Task RemoveAsync_ShouldCleanupTagIndex()
      {
         var key = "key-with-tags";
         var tags = new[] { "tag1", "tag2" };
         await this.backend.SetAsync(key, "value", new CacheExpirationOptions(), tags);

         await this.backend.RemoveAsync(key);

         var result = await this.backend.GetAsync(key);
         Assert.Null(result);

         // Verificar que tags se removieron cuando no hay más keys
         await this.backend.RemoveByTagsAsync(tags);
      }

      [Fact]
      public async Task RemoveAsync_WithNonExistentKey_ShouldNotThrow()
      {
         // No debe lanzar excepción
         await this.backend.RemoveAsync("non-existent-key");
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
      public async Task RemoveByPrefixAsync_ShouldRemoveKeysWithTags()
      {
         await this.backend.SetAsync("prefix-key1", "a", new CacheExpirationOptions(), new[] { "tag1" });
         await this.backend.SetAsync("prefix-key2", "b", new CacheExpirationOptions(), new[] { "tag2" });

         await this.backend.RemoveByPrefixAsync("prefix");

         Assert.Null(await this.backend.GetAsync("prefix-key1"));
         Assert.Null(await this.backend.GetAsync("prefix-key2"));
      }

      [Fact]
      public async Task RemoveByPrefixAsync_NoMatchingKeys_ShouldNotThrow()
      {
         // No debe lanzar excepción
         await this.backend.RemoveByPrefixAsync("nonexistent");
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
      public async Task RemoveByTagsAsync_WithMultipleTags_ShouldRemoveUnionOfKeys()
      {
         await this.backend.SetAsync("key1", "a", new CacheExpirationOptions(), new[] { "tag1" });
         await this.backend.SetAsync("key2", "b", new CacheExpirationOptions(), new[] { "tag2" });
         await this.backend.SetAsync("key3", "c", new CacheExpirationOptions(), new[] { "tag3" });

         await this.backend.RemoveByTagsAsync(new[] { "tag1", "tag2" });

         Assert.Null(await this.backend.GetAsync("key1"));
         Assert.Null(await this.backend.GetAsync("key2"));
         Assert.NotNull(await this.backend.GetAsync("key3"));
      }

      [Fact]
      public async Task RemoveByTagsAsync_WithDuplicateTags_ShouldHandleDistinct()
      {
         await this.backend.SetAsync("key1", "a", new CacheExpirationOptions(), new[] { "tag1" });

         await this.backend.RemoveByTagsAsync(new[] { "tag1", "tag1" });

         Assert.Null(await this.backend.GetAsync("key1"));
      }

      [Fact]
      public async Task RemoveByTagsAsync_NonExistentTag_ShouldNotThrow()
      {
         // No debe lanzar excepción
         await this.backend.RemoveByTagsAsync(new[] { "nonexistent-tag" });
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
      public async Task ClearAsync_WithTags_ShouldClearEverything()
      {
         await this.backend.SetAsync("k1", "v1", new CacheExpirationOptions(), new[] { "tag1" });
         await this.backend.SetAsync("k2", "v2", new CacheExpirationOptions(), new[] { "tag2" });

         await this.backend.ClearAsync();

         Assert.Null(await this.backend.GetAsync("k1"));
         Assert.Null(await this.backend.GetAsync("k2"));
      }

      [Fact]
      public async Task GetKeysAsync_ShouldReturnAllKeys()
      {
         await this.backend.SetAsync("key1", "v1", new CacheExpirationOptions(), new string[0]);
         await this.backend.SetAsync("key2", "v2", new CacheExpirationOptions(), new string[0]);

         var keys = await this.backend.GetKeysAsync();

         Assert.Contains("key1", keys);
         Assert.Contains("key2", keys);
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
      public async Task GetKeysAsync_WithEmptyPattern_ShouldReturnAllKeys()
      {
         await this.backend.SetAsync("key1", "v1", new CacheExpirationOptions(), new string[0]);
         await this.backend.SetAsync("key2", "v2", new CacheExpirationOptions(), new string[0]);

         var keys = await this.backend.GetKeysAsync(null);

         Assert.Contains("key1", keys);
         Assert.Contains("key2", keys);
      }

      [Fact]
      public async Task GetKeysAsync_ShouldNotReturnExpiredKeys()
      {
         var key = "expired-key";
         var expiration = new CacheExpirationOptions
         {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(100),
         };

         await this.backend.SetAsync(key, "value", expiration, new string[0]);

         var keysInitial = await this.backend.GetKeysAsync();
         Assert.Contains(key, keysInitial);

         await Task.Delay(200);
         this.memoryCache.Compact(1.0);
         await Task.Delay(50);

         var keysAfter = await this.backend.GetKeysAsync();
         Assert.DoesNotContain(key, keysAfter);
      }

      [Fact]
      public async Task EvictionCallback_ShouldCleanupIndicesWhenKeyExpires()
      {
         var key = "expiring-key";
         var tags = new[] { "tag1", "tag2" };
         var expiration = new CacheExpirationOptions
         {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(100),
         };

         await this.backend.SetAsync(key, "value", expiration, tags);

         await Task.Delay(200);
         this.memoryCache.Compact(1.0);
         await Task.Delay(50);

         // Verificar que las indices se limpiaron
         var keys = await this.backend.GetKeysAsync();
         Assert.DoesNotContain(key, keys);
      }

      [Fact]
      public async Task SetAsync_ShouldHandleLargeObjects()
      {
         var key = "large-object-key";
         var largeValue = new string('x', 10000);
         var expiration = new CacheExpirationOptions();

         await this.backend.SetAsync(key, largeValue, expiration, new[] { "tag1" });

         var result = await this.backend.GetAsync(key);
         Assert.Equal(largeValue, result);
      }

      [Fact]
      public async Task SetAsync_OverwriteExistingKey_ShouldUpdateValue()
      {
         var key = "overwrite-key";
         var value1 = "value1";
         var value2 = "value2";
         var expiration = new CacheExpirationOptions();

         await this.backend.SetAsync(key, value1, expiration, new[] { "tag1" });
         var result1 = await this.backend.GetAsync(key);
         Assert.Equal(value1, result1);

         await this.backend.SetAsync(key, value2, expiration, new[] { "tag2" });
         var result2 = await this.backend.GetAsync(key);
         Assert.Equal(value2, result2);
      }
   }
}