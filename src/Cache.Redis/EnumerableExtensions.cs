using System.Collections.Generic;

namespace Cache.Redis
{
   /// <summary>
   /// EnumerableExtensions provides extension methods for IEnumerable collections.
   /// </summary>
   internal static class EnumerableExtensions
   {
      /// <summary>
      /// Batch splits an IEnumerable into smaller batches of a specified size.
      /// </summary>
      /// <typeparam name="T">Type data.</typeparam>
      /// <param name="source">Original IEnumerable.</param>
      /// <param name="batchSize">Size from the new IEnumerable.</param>
      /// <returns>New IEnumerable.</returns>
      internal static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
      {
         var batch = new List<T>(batchSize);
         foreach (var item in source)
         {
            batch.Add(item);
            if (batch.Count == batchSize)
            {
               yield return batch;
               batch = new List<T>(batchSize);
            }
         }

         if (batch.Count > 0)
         {
            yield return batch;
         }
      }
   }
}
