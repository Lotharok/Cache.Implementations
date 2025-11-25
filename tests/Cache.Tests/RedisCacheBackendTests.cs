using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cache.Redis;
using Component.Cache.Models;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Cache.Tests
{
   public class RedisCacheBackendTests
   {
      private readonly Mock<IDatabase> redisMock;
      private readonly Mock<IConnectionMultiplexer> multiplexerMock;
      private readonly RedisCacheBackend<string> backend;
      private readonly string prefix = "test";

      public RedisCacheBackendTests()
      {
         this.redisMock = new Mock<IDatabase>();
         this.multiplexerMock = new Mock<IConnectionMultiplexer>();
         this.multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(this.redisMock.Object);
         this.redisMock.Setup(r => r.Multiplexer).Returns(this.multiplexerMock.Object);
         this.backend = new RedisCacheBackend<string>(this.prefix, this.multiplexerMock.Object);
      }

      [Theory]
      [InlineData(null)]
      [InlineData("")]
      [InlineData("   ")]
      public void Constructor_ShouldThrowArgumentNullException_WhenNamespaceInvalid(string invalid)
      {
         // Arrange
         var mockConnection = new Mock<IConnectionMultiplexer>();

         // Act & Assert
         var exception = Assert.Throws<ArgumentNullException>(() =>
            new RedisCacheBackend<string>(invalid, mockConnection.Object));

         Assert.Equal("namespacePrefix", exception.ParamName);
      }

      [Fact]
      public async Task ExistsAsync_ReturnsTrue_WhenKeyExists()
      {
         var key = "exists";
         this.redisMock.Setup(r => r.KeyExistsAsync(this.Namespaced(key), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
         var result = await this.backend.ExistsAsync(key);
         Assert.True(result);
         Assert.Equal(CacheType.Distributed, this.backend.CacheType);
      }

      [Fact]
      public async Task GetAsync_ReturnsValue_WhenKeyExists()
      {
         var key = "test-key";
         var expected = "value";
         this.redisMock.Setup(r => r.StringGetAsync(this.Namespaced(key), It.IsAny<CommandFlags>()))
            .ReturnsAsync(expected);
         var result = await this.backend.GetAsync(key);
         Assert.Equal(expected, result);
      }

      [Fact]
      public async Task GetAsync_ShouldReturnByteArray_WhenValueExists()
      {
         var key = "binary-key";
         var expectedBuffer = new byte[] { 10, 20, 30 };
         RedisValue redisValue = expectedBuffer;
         this.redisMock
            .Setup(db => db.StringGetAsync(this.Namespaced(key), It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisValue)
            .Verifiable();
         var cacheBackend = new RedisCacheBackend<byte[]>(this.prefix, this.multiplexerMock.Object);
         var result = await cacheBackend.GetAsync(key);
         Assert.Equal(expectedBuffer, result);
         this.redisMock.Verify();
      }

      [Fact]
      public async Task GetAsync_ReturnsDefault_WhenKeyNotExists()
      {
         var key = "test-key";
         this.redisMock.Setup(r => r.StringGetAsync(this.Namespaced(key), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
         var result = await this.backend.GetAsync(key);
         Assert.Null(result);
      }

      [Fact]
      public async Task SetAsync_SetsValueAndTags()
      {
         var key = "set-key";
         var value = "stored-value";
         var expiration = new CacheExpirationOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
         var tags = new[] { "tag1", "tag2" };

         this.redisMock.Setup(r => r.StringSetAsync(this.Namespaced(key), value, It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

         this.redisMock.Setup(r => r.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), CommandFlags.None))
            .ReturnsAsync(true);

         await this.backend.SetAsync(key, value, expiration, tags);

         this.redisMock.Verify(r => r.StringSetAsync(this.Namespaced(key), value, It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None), Times.Once);
         this.redisMock.Verify(r => r.SetAddAsync(this.Namespaced("tag:tag1"), key, CommandFlags.None), Times.Once);
         this.redisMock.Verify(r => r.SetAddAsync(this.Namespaced("tag:tag2"), key, CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task SetAsync_AbsoluteExpirationAt()
      {
         var key = "set-key";
         var value = "stored-value";
         var expiration = new CacheExpirationOptions { AbsoluteExpirationAt = DateTimeOffset.UtcNow.AddMilliseconds(200) };
         var tags = Array.Empty<string>();

         this.redisMock.Setup(r => r.StringSetAsync(this.Namespaced(key), value, It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

         this.redisMock.Setup(r => r.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), CommandFlags.None))
            .ReturnsAsync(true);

         await this.backend.SetAsync(key, value, expiration, tags);

         this.redisMock.Verify(r => r.StringSetAsync(this.Namespaced(key), value, It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task SetAsync_NotExpiration()
      {
         var key = "set-key";
         var value = "stored-value";
         var expiration = new CacheExpirationOptions();
         var tags = Array.Empty<string>();

         this.redisMock.Setup(r => r.StringSetAsync(this.Namespaced(key), value, It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

         this.redisMock.Setup(r => r.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), CommandFlags.None))
            .ReturnsAsync(true);

         await this.backend.SetAsync(key, value, expiration, tags);

         this.redisMock.Verify(r => r.StringSetAsync(this.Namespaced(key), value, It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task SetAsync_ShouldStoreByteArray()
      {
         var key = "set-key";
         var buffer = new byte[] { 1, 2, 3 };
         var expiration = new CacheExpirationOptions
         {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
         };
         var tags = Array.Empty<string>();

         this.redisMock.Setup(r => r.StringSetAsync(
               this.Namespaced(key),
               It.Is<RedisValue>(v => ((byte[])v).SequenceEqual(buffer)),
               It.IsAny<TimeSpan?>(),
               false,
               When.Always,
               CommandFlags.None))
            .ReturnsAsync(true);
         this.multiplexerMock.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(this.redisMock.Object);

         var cacheBackend = new RedisCacheBackend<byte[]>(this.prefix, this.multiplexerMock.Object);

         // Act
         await cacheBackend.SetAsync(key, buffer, expiration, tags);

         // Assert
         this.redisMock.Verify(r => r.StringSetAsync(this.Namespaced(key), It.Is<RedisValue>(v => ((byte[])v).SequenceEqual(buffer)), It.IsAny<TimeSpan?>(), false, When.Always, CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task RemoveAsync_DeletesKey()
      {
         var key = "remove-key";

         await this.backend.RemoveAsync(key);

         this.redisMock.Verify(r => r.KeyDeleteAsync(this.Namespaced(key), It.IsAny<CommandFlags>()), Times.Once);
      }

      [Fact]
      public async Task RemoveByTagsAsync_DeletesTaggedKeys()
      {
         var tags = new[] { "tag1" };
         var tagKey = this.Namespaced($"tag:{tags[0]}");
         var redisKeys = new RedisValue[] { "key1", "key2" };

         this.redisMock.Setup(r => r.SetMembersAsync(tagKey, It.IsAny<CommandFlags>())).ReturnsAsync(redisKeys);

         await this.backend.RemoveByTagsAsync(tags);

         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Once);
         this.redisMock.Verify(r => r.KeyDeleteAsync(tagKey, CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task RemoveByTagsAsync_DeletesTaggedKeys_Batch()
      {
         var tags = new[] { "tag1" };
         var tagKey = this.Namespaced($"tag:{tags[0]}");
         var redisKeys = new RedisValue[]
         {
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
            "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2", "key1", "key2",
         };

         this.redisMock.Setup(r => r.SetMembersAsync(tagKey, It.IsAny<CommandFlags>())).ReturnsAsync(redisKeys);

         await this.backend.RemoveByTagsAsync(tags);

         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Once);
         this.redisMock.Verify(r => r.KeyDeleteAsync(tagKey, CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task ClearAsync_DeletesKeysFromConnectedWritableServers()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();
         var keys = new RedisKey[] { "test:key1", "test:key2" };

         serverMock.SetupGet(s => s.IsConnected).Returns(true);
         serverMock.SetupGet(s => s.IsReplica).Returns(false);
         serverMock
            .Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), CommandFlags.None))
            .Returns(GetAsyncEnumerable(keys));

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);

         var databaseMock = new Mock<IDatabase>();
         databaseMock.Setup(d => d.Multiplexer).Returns(this.multiplexerMock.Object);
         this.multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(databaseMock.Object);
         var cacheBackend = new RedisCacheBackend<string>("test", this.multiplexerMock.Object);

         // Act
         await cacheBackend.ClearAsync();

         // Assert
         databaseMock.Verify(d => d.KeyDeleteAsync(keys, CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task ClearAsync_SkipsDisconnectedOrReplicaServers()
      {
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();

         serverMock.SetupGet(s => s.IsConnected).Returns(false); // desconectado
         serverMock.SetupGet(s => s.IsReplica).Returns(true);    // réplica
         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);

         var databaseMock = new Mock<IDatabase>();
         databaseMock.Setup(d => d.Multiplexer).Returns(this.multiplexerMock.Object);
         this.multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(databaseMock.Object);
         var cacheBackend = new RedisCacheBackend<string>("test", this.multiplexerMock.Object);

         // Act
         await cacheBackend.ClearAsync();

         // Assert: no se deberían haber intentado borrar claves
         databaseMock.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Never);
      }

      [Fact]
      public async Task GetKeysAsync_ShouldReturnKeys_WhenServerIsConnectedAndPrimary()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var redisKeys = new RedisKey[] { "test:one", "test:two" };
         var asyncKeys = new TestAsyncEnumerable<RedisKey>(redisKeys);
         var serverMock = new Mock<IServer>();

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);
         serverMock.Setup(s => s.IsConnected).Returns(true);
         serverMock.Setup(s => s.IsReplica).Returns(false);
         serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), CommandFlags.None))
                   .Returns(asyncKeys);

         var databaseMock = new Mock<IDatabase>();
         databaseMock.Setup(d => d.Multiplexer).Returns(this.multiplexerMock.Object);
         this.multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(databaseMock.Object);
         var cacheBackend = new RedisCacheBackend<string>("test", this.multiplexerMock.Object);

         // Act
         var result = await cacheBackend.GetKeysAsync();

         // Assert
         Assert.Equal(redisKeys.Select(k => k.ToString()), result);
      }

      [Fact]
      public async Task GetKeysAsync_ShouldUsePattern_WhenProvided()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var pattern = "prefix";
         var redisKeys = new RedisKey[] { "prefix1", "prefix2" };
         var asyncKeys = new TestAsyncEnumerable<RedisKey>(redisKeys);
         var serverMock = new Mock<IServer>();

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);
         serverMock.Setup(s => s.IsConnected).Returns(true);
         serverMock.Setup(s => s.IsReplica).Returns(false);
         serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
                   .Returns(asyncKeys);

         var databaseMock = new Mock<IDatabase>();
         databaseMock.Setup(d => d.Multiplexer).Returns(this.multiplexerMock.Object);
         this.multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(databaseMock.Object);
         var cacheBackend = new RedisCacheBackend<string>(pattern, this.multiplexerMock.Object);

         // Act
         var result = await cacheBackend.GetKeysAsync(pattern);

         // Assert
         Assert.Equal(redisKeys.Select(k => k.ToString()), result);
      }

      [Fact]
      public async Task GetKeysAsync_ShouldReturnEmpty_WhenServerIsDisconnectedOrReplica()
      {
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);

         var databaseMock = new Mock<IDatabase>();
         databaseMock.Setup(d => d.Multiplexer).Returns(this.multiplexerMock.Object);
         this.multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(databaseMock.Object);
         var cacheBackend = new RedisCacheBackend<string>("test", this.multiplexerMock.Object);

         // Arrange
         serverMock.Setup(s => s.IsConnected).Returns(false); // Desconectado
         serverMock.Setup(s => s.IsReplica).Returns(true);    // Es réplica

         // Act
         var result = await cacheBackend.GetKeysAsync();

         // Assert
         Assert.Empty(result);
      }

      [Fact]
      public async Task RemoveByPrefixAsync_ShouldDeleteAllMatchingKeys()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();
         var pattern = "myPrefix";
         var keysFound = new[] { "prefix:k1", "prefix:k2" };
         var redisKeys = keysFound.Select(k => (RedisKey)k).ToArray();
         var asyncKeys = new TestAsyncEnumerable<RedisKey>(redisKeys);

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);
         serverMock.Setup(s => s.IsConnected).Returns(true);
         serverMock.Setup(s => s.IsReplica).Returns(false);
         serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), CommandFlags.None))
            .Returns(asyncKeys);

         this.redisMock.Setup(d => d.Multiplexer).Returns(this.multiplexerMock.Object);
         this.redisMock.Setup(r => r.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(true);
         var cacheBackend = new RedisCacheBackend<string>(pattern, this.multiplexerMock.Object);

         // Act
         await cacheBackend.RemoveByPrefixAsync(pattern);

         // Assert
         this.redisMock.Verify(r => r.KeyDeleteAsync(redisKeys, CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task RemoveByPrefixAsync_ShouldNotDelete_WhenNoKeysFound()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();
         var pattern = "noMatch";
         var redisKeys = Array.Empty<RedisKey>();
         var asyncKeys = new TestAsyncEnumerable<RedisKey>(redisKeys);

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);
         serverMock.Setup(s => s.IsConnected).Returns(true);
         serverMock.Setup(s => s.IsReplica).Returns(false);
         serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), CommandFlags.None))
            .Returns(asyncKeys);

         this.redisMock.Setup(d => d.Multiplexer).Returns(this.multiplexerMock.Object);
         this.redisMock.Setup(r => r.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(true);
         this.multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(this.redisMock.Object);
         var cacheBackend = new RedisCacheBackend<string>(pattern, this.multiplexerMock.Object);

         // Act
         await cacheBackend.RemoveByPrefixAsync(pattern);

         // Assert
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Never);
      }

      [Fact]
      public async Task RemoveByPrefixAsync_ShouldRespectCancellationToken()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();
         var pattern = "cancelledPrefix";
         var token = new CancellationTokenSource().Token;
         var redisKeys = new[] { "key1" }.Select(k => (RedisKey)k).ToArray();
         var asyncKeys = new TestAsyncEnumerable<RedisKey>(redisKeys);

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);
         serverMock.Setup(s => s.IsConnected).Returns(true);
         serverMock.Setup(s => s.IsReplica).Returns(false);
         serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), CommandFlags.None))
            .Returns(asyncKeys);

         this.redisMock.Setup(d => d.Multiplexer).Returns(this.multiplexerMock.Object);
         this.redisMock.Setup(r => r.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(true);
         this.multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(this.redisMock.Object);
         var cacheBackend = new RedisCacheBackend<string>(pattern, this.multiplexerMock.Object);

         // Act
         await cacheBackend.RemoveByPrefixAsync(this.prefix, token);

         // Assert
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Once);
      }

      [Fact]
      public async Task SetAsync_ShouldThrowInvalidOperationException_WhenTypeIsUnsupported()
      {
         var key = "unsupported-key";
         var value = 123; // int is not supported
         var expiration = new CacheExpirationOptions();
         var tags = Array.Empty<string>();

         var cacheBackend = new RedisCacheBackend<int>(this.prefix, this.multiplexerMock.Object);

         await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cacheBackend.SetAsync(key, value, expiration, tags));
      }

      [Fact]
      public async Task GetAsync_ShouldThrowInvalidOperationException_WhenTypeIsUnsupported()
      {
         // Arrange
         var key = "unsupported-type-key";
         var redisValue = (RedisValue)123; // An integer value from Redis

         this.redisMock.Setup(r => r.StringGetAsync(this.Namespaced(key), It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisValue);

         var cacheBackend = new RedisCacheBackend<int>(this.prefix, this.multiplexerMock.Object);

         // Act & Assert
         await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cacheBackend.GetAsync(key));
      }

      [Theory]
      [InlineData(null)]
      [InlineData("")]
      [InlineData("   ")]
      public async Task RemoveByPrefixAsync_ShouldHandleInvalidPrefixes(string invalidPrefix)
      {
         // Act
         await this.backend.RemoveByPrefixAsync(invalidPrefix);

         // Assert
         // Verify that no KeyDeleteAsync operations were attempted
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
      }

      [Theory]
      [InlineData("*")]
      [InlineData("  *")]
      public async Task RemoveByPrefixAsync_ShouldHandlePrefixBecomingInvalidAfterTrim(string prefixWithWildcard)
      {
         // Act
         await this.backend.RemoveByPrefixAsync(prefixWithWildcard);

         // Assert
         // Verify that no KeyDeleteAsync operations were attempted
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
      }

      [Fact]
      public async Task RemoveByPrefixAsync_ShouldSkipDisconnectedOrReplicaServers()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();
         var pattern = "somePrefix";

         // Mock a disconnected or replica server
         serverMock.SetupGet(s => s.IsConnected).Returns(false); // disconnected
         serverMock.SetupGet(s => s.IsReplica).Returns(true);    // replica

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);

         // Act
         await this.backend.RemoveByPrefixAsync(pattern);

         // Assert
         // Verify that no KeyDeleteAsync operations were attempted
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
      }

      [Fact]
      public async Task RemoveByPrefixAsync_ShouldHandleBatching()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();
         var pattern = "batchPrefix";
         var keysToGenerate = 2500; // More than BatchSize (1000) to ensure multiple batches
         var keys = Enumerable.Range(0, keysToGenerate).Select(i => (RedisKey)$"key:{i}").ToArray();
         var asyncKeys = new TestAsyncEnumerable<RedisKey>(keys);

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);
         serverMock.Setup(s => s.IsConnected).Returns(true);
         serverMock.Setup(s => s.IsReplica).Returns(false);
         serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(asyncKeys);

         // Act
         await this.backend.RemoveByPrefixAsync(pattern);

         // Assert
         // Expecting keysToGenerate / BatchSize + 1 calls (2500 / 1000 + 1 = 3 calls)
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Exactly(3));
      }

      [Fact]
      public async Task ClearAsync_ShouldHandleBatching()
      {
         // Arrange
         var endpoint = new DnsEndPoint("localhost", 6379);
         var serverMock = new Mock<IServer>();
         var keysToGenerate = 2500; // More than BatchSize (1000) to ensure multiple batches
         var keys = Enumerable.Range(0, keysToGenerate).Select(i => (RedisKey)$"key:{i}").ToArray();
         var asyncKeys = new TestAsyncEnumerable<RedisKey>(keys);

         this.multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
         this.multiplexerMock.Setup(m => m.GetServer(endpoint, null)).Returns(serverMock.Object);
         serverMock.Setup(s => s.IsConnected).Returns(true);
         serverMock.Setup(s => s.IsReplica).Returns(false);
         serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(asyncKeys);

         // Act
         await this.backend.ClearAsync();

         // Assert
         // Expecting keysToGenerate / BatchSize + 1 calls (2500 / 1000 + 1 = 3 calls)
         this.redisMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Exactly(3));
      }

      private static IAsyncEnumerable<RedisKey> GetAsyncEnumerable(IEnumerable<RedisKey> keys)
      {
         return new TestAsyncEnumerable<RedisKey>(keys);
      }

      private string Namespaced(string key) => $"{{{this.prefix}}}:{key}";
   }
}
