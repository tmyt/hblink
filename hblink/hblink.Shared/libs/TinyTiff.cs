using System;
using System.Collections.Generic;
using System.Text;

namespace hblink.libs
{
    public class TinyTiff
    {
        const int TagDateTime = 0x0132;

        class IDF
        {
            public int Tag { get; set; }
            public int Type { get; set; }
            public int Length { get; set; }
            public byte[] Data { get; set; }
        }

        private static IEnumerable<IDF> Parse(byte[] tiff)
        {
            var idfOffset = BitConverter.ToInt32(tiff, 4);
            var num = BitConverter.ToInt16(tiff, idfOffset);
            for (var n = 0; n < num; ++n)
            {
                var offset = idfOffset + 2 + n * 12;
                var tag = BitConverter.ToInt16(tiff, offset + 0x00);
                var type = BitConverter.ToInt16(tiff, offset + 0x02);
                var count = BitConverter.ToInt32(tiff, offset + 0x04);
                var data = count <= 4
                    ? tiff.Copy(offset + 0x08, count)
                    : tiff.Copy(BitConverter.ToInt32(tiff, offset + 0x08), count);
                yield return new IDF
                {
                    Tag = tag,
                    Type = type,
                    Length = count,
                    Data = data,
                };
            }
        }

        public static DateTime GetDateTime(byte[] tiff)
        {
            foreach (var idf in Parse(tiff))
            {
                if (idf.Tag != TagDateTime) continue;
                var text = Encoding.UTF8.GetString(idf.Data, 0, idf.Length - 1).Trim();
                if(text == "Date/Time not set") return DateTime.MinValue;
                var format = "yyyy:MM:dd HH:mm:ss";
                return DateTime.ParseExact(text, format, null);
            }
            return DateTime.MinValue;
        }
    }
}
