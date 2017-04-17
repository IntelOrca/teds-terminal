using System;

namespace tterm.Extensions
{
    internal static class ArraySegmentExtensions
    {
        public static ArraySegment<T> Substring<T>(this ArraySegment<T> segment, int offset)
        {
            return new ArraySegment<T>(segment.Array, segment.Offset + offset, segment.Count - offset);
        }

        public static ArraySegment<T> Substring<T>(this ArraySegment<T> segment, int offset, int count)
        {
            return new ArraySegment<T>(segment.Array, segment.Offset + offset, count);
        }
    }
}
