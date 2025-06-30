using Autofac;
using Cache.Redis;
using Component.Cache.Contract;
using StackExchange.Redis;

namespace IntegrationTest
{
   internal class TestModule : Module
   {
      protected override void Load(ContainerBuilder builder)
      {
         var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
         builder.RegisterInstance(multiplexer).As<IConnectionMultiplexer>().SingleInstance();

         builder.Register(c =>
         {
            var prefix = $"test";
            return new RedisCacheBackend<string>(prefix, multiplexer);
         }).As<ICacheBackend<string>>().InstancePerLifetimeScope();

         builder.Register(c =>
         {
            var prefix = $"test-bytes";
            return new RedisCacheBackend<byte[]>(prefix, multiplexer);
         }).As<ICacheBackend<byte[]>>().InstancePerLifetimeScope();
      }
   }
}
