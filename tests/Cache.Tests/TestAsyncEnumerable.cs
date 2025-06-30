using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cache.Tests
{
   public class TestAsyncEnumerable<T> : IAsyncEnumerable<T>
   {
      private readonly IEnumerable<T> items;

      public TestAsyncEnumerable(IEnumerable<T> items) => this.items = items;

      public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
         => new TestAsyncEnumerator<T>(this.items.GetEnumerator());

      private class TestAsyncEnumerator<TItem> : IAsyncEnumerator<TItem>
      {
         private readonly IEnumerator<TItem> inner;

         public TestAsyncEnumerator(IEnumerator<TItem> inner) => this.inner = inner;

         public TItem Current => this.inner.Current;

         public ValueTask DisposeAsync()
         {
            this.inner.Dispose();
            return ValueTask.CompletedTask;
         }

         public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(this.inner.MoveNext());
      }
   }
}
