using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ArkSharp
{
	public static class DictionaryHelper
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dict, Func<KeyValuePair<TKey, TValue>, bool> predicate, IList<TKey> tempKeyList = null)
		{
			if (dict.Count <= 0)
				return;

			if (tempKeyList == null)
				tempKeyList = new List<TKey>(dict.Count);

			foreach (var kv in dict)
			{
				if (predicate(kv))
					tempKeyList.Add(kv.Key);
			}

			for (int i = 0; i < tempKeyList.Count; i++)
			{
				dict.Remove(tempKeyList[i]);
			}

			tempKeyList.Clear();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrEmpty<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict)
		{
			return dict == null || dict.Count == 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TValue GetValueOrDefault<TKey1, TKey2, TValue>(this IReadOnlyDictionary<TKey1, IReadOnlyDictionary<TKey2, TValue>> dict, TKey1 key1, TKey2 key2)
		{
			var dict2 = dict.GetValueOrDefault(key1);
			if (dict2 == null)
				return default;

			return dict2.GetValueOrDefault(key2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TValue GetValueOrDefault<TKey1, TKey2, TValue>(this IReadOnlyDictionary<TKey1, IReadOnlyDictionary<TKey2, TValue>> dict, TKey1 key1, TKey2 key2, TValue defaultValue)
		{
			var dict2 = dict.GetValueOrDefault(key1);
			if (dict2 == null)
				return default;

			return dict2.GetValueOrDefault(key2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TValue GetValueOrDefault<TKey1, TKey2, TKey3, TValue>(this IReadOnlyDictionary<TKey1, IReadOnlyDictionary<TKey2, IReadOnlyDictionary<TKey3, TValue>>> dict, TKey1 key1, TKey2 key2, TKey3 key3)
		{
			var dict2 = dict.GetValueOrDefault(key1);
			if (dict2 == null)
				return default;

			var dict3 = dict2.GetValueOrDefault(key2);
			if (dict3 == null)
				return default;

			return dict3.GetValueOrDefault(key3);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TValue GetValueOrDefault<TKey1, TKey2, TKey3, TValue>(this IReadOnlyDictionary<TKey1, IReadOnlyDictionary<TKey2, IReadOnlyDictionary<TKey3, TValue>>> dict, TKey1 key1, TKey2 key2, TKey3 key3, TValue defaultValue)
		{
			var dict2 = dict.GetValueOrDefault(key1);
			if (dict2 == null)
				return defaultValue;

			var dict3 = dict2.GetValueOrDefault(key2);
			if (dict3 == null)
				return defaultValue;

			return dict3.GetValueOrDefault(key3);
		}
	}
}
