using System;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Component.Cache.Contract;
using Component.Cache.Models;
using Xunit;

namespace IntegrationTest
{
   public class RedisCacheBackendTests : IAsyncLifetime
   {
      private IContainer container = null!;
      private ICacheBackend<string> backend = null!;

      private CacheExpirationOptions expirationDefault =
         new CacheExpirationOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

      public async Task InitializeAsync()
      {
         var builder = new ContainerBuilder();
         builder.RegisterModule<TestModule>();
         this.container = builder.Build();

         this.backend = this.container.Resolve<ICacheBackend<string>>();
         var exists = await this.backend.ExistsAsync("key2");
         await this.backend.ClearAsync(); // Asegura entorno limpio
      }

      public Task DisposeAsync()
      {
         this.container.Dispose();
         return Task.CompletedTask;
      }

      [Fact]
      public async Task SetAndGet_ShouldReturnExpectedValue()
      {
         await this.backend.SetAsync("key1", "value1", this.expirationDefault, Array.Empty<string>());
         var result = await this.backend.GetAsync("key1");
         Assert.Equal("value1", result);
      }

      [Fact]
      public async Task ExistsAsync_ShouldReturnTrueWhenExists()
      {
         await this.backend.SetAsync("key2", "exists", this.expirationDefault, Array.Empty<string>());

         var exists = await this.backend.ExistsAsync("key2");
         Assert.True(exists);
      }

      [Fact]
      public async Task SetAsync_WithExpiration_ShouldExpire()
      {
         var expiration = new CacheExpirationOptions
         {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(300),
         };

         await this.backend.SetAsync("expiring", "soon", expiration, Array.Empty<string>());
         Assert.True(await this.backend.ExistsAsync("expiring"));
         await Task.Delay(500);
         Assert.False(await this.backend.ExistsAsync("expiring"));
      }

      [Fact]
      public async Task RemoveByTagsAsync_ShouldRemoveTaggedItems()
      {
         await this.backend.SetAsync("tagged-1", "v1", this.expirationDefault, new[] { "my-tag" });
         await this.backend.SetAsync("tagged-2", "v2", this.expirationDefault, new[] { "my-tag" });
         await this.backend.RemoveByTagsAsync(new[] { "my-tag" });
         Assert.False(await this.backend.ExistsAsync("tagged-1"));
         Assert.False(await this.backend.ExistsAsync("tagged-2"));
      }

      [Fact]
      public async Task RemoveByPrefix_ShouldDeleteMatchingKeys()
      {
         await this.backend.SetAsync("prefixed:one", "1", this.expirationDefault, Array.Empty<string>());
         await this.backend.SetAsync("prefixed:two", "2", this.expirationDefault, Array.Empty<string>());
         await this.backend.SetAsync("other", "x", this.expirationDefault, Array.Empty<string>());
         var keys = await this.backend.GetKeysAsync("prefixed:*");
         Assert.Equal(2, keys.Count());
         await this.backend.RemoveByPrefixAsync("prefixed");
         Assert.False(await this.backend.ExistsAsync("prefixed:one"));
         Assert.False(await this.backend.ExistsAsync("prefixed:two"));
         Assert.True(await this.backend.ExistsAsync("other"));
      }

      [Fact]
      public async Task ClearAsync_ShouldRemoveAllKeysInNamespace()
      {
         await this.backend.SetAsync("a", "1", this.expirationDefault, Array.Empty<string>());
         await this.backend.SetAsync("b", "2", this.expirationDefault, Array.Empty<string>());
         await this.backend.ClearAsync();
         Assert.False(await this.backend.ExistsAsync("a"));
         Assert.False(await this.backend.ExistsAsync("b"));
      }
   }
}
