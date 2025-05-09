using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ArkSharp
{
	public static class ReadOnlyListHelper
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T>(this IReadOnlyList<T> list, T value)
		{
			return list.IndexOf(value) != -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T>(this IReadOnlyList<T> list, T value)
		{
			var comparer = EqualityComparer<T>.Default;
			
			for (int i = 0; i < list.Count; i++)
			{
				if (comparer.Equals(list[i], value))
					return i;
			}
					
			return -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Exists<T>(this IReadOnlyList<T> list, Predicate<T> match)
		{
			return list.FindIndex(match) != -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Find<T>(this IReadOnlyList<T> list, Predicate<T> match)
		{
			for (int i = 0; i < list.Count; i++)
			{
				var item = list[i];
				if (match(item))
					return item;
			}

			return default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FindIndex<T>(this IReadOnlyList<T> list, Predicate<T> match)
		{
			for (int i = 0; i < list.Count; i++)
			{
				var item = list[i];
				if (match(item))
					return i;
			}

			return -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<TOutput> ConvertAll<T, TOutput>(this IReadOnlyList<T> list, Converter<T, TOutput> converter, List<TOutput> output = null)
		{
			if (output == null)
				output = new List<TOutput>(list.Count);

			for (int i = 0; i < list.Count; i++)
			{
				output.Add(converter(list[i]));
			}

			return output;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool SequenceEqual<T>(this IReadOnlyList<T> list, IReadOnlyList<T> other, Func<T, T, bool> comparer)
		{
			if (list.Count != other.Count)
				return false;

			for (int i = 0; i < list.Count; ++i)
			{
				if (!comparer(list[i], other[i]))
					return false;
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool SequenceEqual<T>(this IReadOnlyList<T> list, IReadOnlyList<T> other, IEqualityComparer<T> comparer = null)
		{
			if (list.Count != other.Count)
				return false;

			if (comparer == null)
				comparer = EqualityComparer<T>.Default;

			for (int i = 0; i < list.Count; ++i)
			{
				if (!comparer.Equals(list[i], other[i]))
					return false;
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReadOnlyList<T> EmptyToNull<T>(this IReadOnlyList<T> list)
		{
			if (list == null || list.Count <= 0)
				return null;

			return list;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T GetValueOrDefault<T>(this IReadOnlyList<T> list, int index)
		{
			if (index >= 0 && index < list.Count)
				return list[index];

			return default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T GetValueOrDefault<T>(this IReadOnlyList<T> list, int index, in T defaultValue)
		{
			if (index >= 0 && index < list.Count)
				return list[index];

			return defaultValue;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T GetValueOrFirst<T>(this IReadOnlyList<T> list, int index)
		{
			if (list.Count <= 0)
				return default;

			if (index >= 0 && index < list.Count)
				return list[index];

			return list[0];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T GetValueOrLast<T>(this IReadOnlyList<T> list, int index)
		{
			if (list.Count <= 0)
				return default;

			if (index >= 0 && index < list.Count)
				return list[index];

			return list[list.Count - 1];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrEmpty<T>(this IReadOnlyList<T> list)
		{
			return list == null || list.Count == 0;
		}
	}
}
