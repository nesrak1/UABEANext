using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UABEANext4.Util;
public static class ObservableCollectionExtensions
{
    public static int BinarySearch<T>(this ObservableCollection<T> collection, T item, Func<T, T, int>? compare = null)
    {
        if (collection == null || collection.Count == 0)
        {
            return -1;
        }

        compare ??= ((x, y) => Comparer<T>.Default.Compare(x, y));

        int low = 0;
        int high = collection.Count - 1;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            int comparison = compare(collection[mid], item);

            if (comparison == 0)
            {
                return mid;
            }
            else if (comparison < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return ~low;
    }
}