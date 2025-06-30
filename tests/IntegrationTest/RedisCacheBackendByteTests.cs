using System;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Component.Cache.Contract;
using Component.Cache.Models;
using IntegrationTest.Models;
using PriceTravel.Vendor.ProtobufSerialization;
using Xunit;

namespace IntegrationTest
{
   public class RedisCacheBackendByteTests : IAsyncLifetime
   {
      private IContainer container = null!;
      private ICacheBackend<byte[]> backend = null!;
      private CacheExpirationOptions expirationDefault =
         new CacheExpirationOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

      public async Task InitializeAsync()
      {
         var builder = new ContainerBuilder();
         builder.RegisterModule<TestModule>();
         this.container = builder.Build();

         this.backend = this.container.Resolve<ICacheBackend<byte[]>>();
         await this.backend.ClearAsync();
      }

      public Task DisposeAsync()
      {
         this.container.Dispose();
         return Task.CompletedTask;
      }

      [Fact]
      public async Task SetAndGet_ShouldReturnExpectedObject()
      {
         var key = "protobuf-key";
         var person = new Person { Id = 123, Name = "Ana" };
         var serializator = new ProtobufSerializer();
         var bytes = serializator.Serialize(person);

         await this.backend.SetAsync(key, bytes, this.expirationDefault, Array.Empty<string>());

         var storedBytes = await this.backend.GetAsync(key);
         Assert.NotNull(storedBytes);

         var recovered = serializator.Deserialize<Person>(storedBytes!);
         Assert.Equal(123, recovered.Id);
         Assert.Equal("Ana", recovered.Name);
      }

      [Fact]
      public async Task ExistsAsync_ShouldReturnTrueWhenExists()
      {
         var person = new Person { Id = 123, Name = "Ana" };
         var serializator = new ProtobufSerializer();
         var bytes = serializator.Serialize(person);
         await this.backend.SetAsync("key2", bytes, this.expirationDefault, Array.Empty<string>());
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
         var person = new Person { Id = 123, Name = "Ana" };
         var serializator = new ProtobufSerializer();
         var bytes = serializator.Serialize(person);
         await this.backend.SetAsync("expiring", bytes, expiration, Array.Empty<string>());
         Assert.True(await this.backend.ExistsAsync("expiring"));
         await Task.Delay(500);
         Assert.False(await this.backend.ExistsAsync("expiring"));
      }

      [Fact]
      public async Task RemoveByTagsAsync_ShouldRemoveTaggedItems()
      {
         var person = new Person { Id = 123, Name = "Ana" };
         var serializator = new ProtobufSerializer();
         var bytes = serializator.Serialize(person);
         await this.backend.SetAsync("tagged-1", bytes, this.expirationDefault, new[] { "my-tag" });
         person.Name = "Bob";
         bytes = serializator.Serialize(person);
         await this.backend.SetAsync("tagged-2", bytes, this.expirationDefault, new[] { "my-tag" });
         await this.backend.RemoveByTagsAsync(new[] { "my-tag" });
         Assert.False(await this.backend.ExistsAsync("tagged-1"));
         Assert.False(await this.backend.ExistsAsync("tagged-2"));
      }

      [Fact]
      public async Task RemoveByPrefix_ShouldDeleteMatchingKeys()
      {
         var person = new Person { Id = 123, Name = "Ana" };
         var serializator = new ProtobufSerializer();
         var bytes = serializator.Serialize(person);
         await this.backend.SetAsync("prefixed:one", bytes, this.expirationDefault, Array.Empty<string>());
         person.Name = "Bob";
         bytes = serializator.Serialize(person);
         await this.backend.SetAsync("prefixed:two", bytes, this.expirationDefault, Array.Empty<string>());
         person.Name = "X";
         bytes = serializator.Serialize(person);
         await this.backend.SetAsync("other", bytes, this.expirationDefault, Array.Empty<string>());
         var keys = await this.backend.GetKeysAsync("prefixed:*");
         Assert.Equal(2, keys.Count());
         await this.backend.RemoveByPrefixAsync("prefixed");
         Assert.False(await this.backend.ExistsAsync("prefixed:one"));
         Assert.False(await this.backend.ExistsAsync("prefixed:two"));
         var x = await this.backend.GetAsync("other");
         var personX = serializator.Deserialize<Person>(x!);
         Assert.NotNull(x);
         Assert.Equal("X", personX.Name);
      }

      [Fact]
      public async Task ClearAsync_ShouldRemoveAllKeysInNamespace()
      {
         var person = new Person { Id = 123, Name = "Ana" };
         var serializator = new ProtobufSerializer();
         var bytes = serializator.Serialize(person);
         await this.backend.SetAsync("a", bytes, this.expirationDefault, Array.Empty<string>());
         person.Name = "Bob";
         bytes = serializator.Serialize(person);
         await this.backend.SetAsync("b", bytes, this.expirationDefault, Array.Empty<string>());
         await this.backend.ClearAsync();
         Assert.False(await this.backend.ExistsAsync("a"));
         Assert.False(await this.backend.ExistsAsync("b"));
      }
   }
}
