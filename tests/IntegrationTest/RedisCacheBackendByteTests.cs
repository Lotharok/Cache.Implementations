using System;
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
         new CacheExpirationOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60) };

      public async Task InitializeAsync()
      {
         var builder = new ContainerBuilder();
         builder.RegisterModule<TestModule>();
         this.container = builder.Build();

         this.backend = this.container.Resolve<ICacheBackend<byte[]>>();
         var exists = await this.backend.ExistsAsync("key2");
         ////await this.backend.ClearAsync();
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

      ////[Fact]
      ////public async Task Set_WithExpiration_ShouldExpire()
      ////{
      ////   var key = "pb-expiring";
      ////   var person = new Person { Id = 1, Name = "Exp" };
      ////   var expiration = new CacheExpirationOptions
      ////   {
      ////      AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(300)
      ////   };

      ////   await backend.SetAsync(key, ProtobufHelper.Serialize(person), expiration, Array.Empty<string>());

      ////   (await backend.ExistsAsync(key)).Should().BeTrue();
      ////   await Task.Delay(500);
      ////   (await backend.ExistsAsync(key)).Should().BeFalse();
      ////}
   }
}
