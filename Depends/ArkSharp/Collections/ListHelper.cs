using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ArkSharp
{
	public static class ListHelper
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AddUnique<T>(this IList<T> list, T value, Func<T, T, bool> comparer)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (comparer(value, list[i]))
					return;
			}

			list.Add(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AddUnique<T>(this IList<T> list, T value, IEqualityComparer<T> comparer = null)
		{
			if (comparer == null)
				comparer = EqualityComparer<T>.Default;

			for (int i = 0; i < list.Count; i++)
			{
				if (comparer.Equals(value, list[i]))
					return;
			}

			list.Add(value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AddRange<T>(this IList<T> list, IEnumerable<T> collection)
		{
			if (collection == null)
				return;

			var c = collection as IReadOnlyList<T>;
			if (c != null)
			{
				for (int i = 0; i < c.Count; i++)
					list.Add(c[i]);
			}
			else
			{
				using (var e = collection.GetEnumerator())
				{
					while (e.MoveNext())
						list.Add(e.Current);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Enlarge<T>(this IList<T> list, int count, T fillValue = default)
		{
			while (list.Count < count)
				list.Add(fillValue);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Resize<T>(this IList<T> list, int count, T fillValue = default)
		{
			if (count < 0)
				count = 0;

			int oldCount = list.Count;
			if (oldCount > count)
			{
				for (int i = oldCount - 1; i >= count; i--)
					list.RemoveAt(i);
			}
			else
			{
				while (list.Count < count)
					list.Add(fillValue);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Shuffle<T>(this IList<T> list, Random random)
		{
			list.Shuffle(random.Next);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Shuffle<T>(this IList<T> list, Func<int, int> randomNextN)
		{
			int n = list.Count;
			while (n > 1)
			{
				n--;
				int k = randomNextN(n + 1);

				var value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}
	}
}
