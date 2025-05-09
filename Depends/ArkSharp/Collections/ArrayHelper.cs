using System;
using System.Runtime.CompilerServices;

namespace ArkSharp
{
	public static class ArrayHelper
	{
		public static int MaxLength => 0x7FFFFFC7;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Clear(this Array array, int index, int length)
		{
			Array.Clear(array, index, length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Clear(this Array array)
		{
			Array.Clear(array, 0, array.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool EnsureCapacity<T>(ref T[] array, int count, int minCapacity = 16)
		{
			int capacity = array.Length;
			if (count <= capacity)
				return true;

			while (count > capacity)
			{
				if (capacity <= 0)
					capacity = minCapacity;
				else
					capacity *= 2;

				if ((uint)capacity > MaxLength)
				{
					capacity = MaxLength;
					break;
				}
			}

			if (capacity <= array.Length)
				return false;

			Array.Resize(ref array, capacity);
			return true;
		}
	}
}
