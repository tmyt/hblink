using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace hblink.libs
{
    static class Ext
    {
        public static void Write(this Stream stream, params byte[] buffer)
        {
            stream.Write(buffer, 0, buffer.Length);
        }

        public static int Read(this Stream stream, byte[] buffer)
        {
            return stream.Read(buffer, 0, buffer.Length);
        }

        public static string Join(this IEnumerable<string> values, string separator)
        {
            return string.Join(separator, values);
        }

        public static void AddRange<T>(List<T> list, params T[] values)
        {
            list.AddRange(values);
        }

        public static string Dump(this IEnumerable<byte> buffer, int length = 16)
        {
            return buffer.Take(length).Select(b => b.ToString("X2")).Join(" ");
        }

        public static byte[] Copy(this byte[] array, int offset, int size)
        {
            var dst = new byte[size];
            Buffer.BlockCopy(array, offset, dst, 0, size);
            return dst;
        }
    }
}
