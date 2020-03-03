using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace hblink.libs
{
    public abstract class Download
    {
        protected const int Type3FR = 0x04;
        protected const int TypeJPG = 0x01;

        protected TaskCompletionSource<Download> _source = new TaskCompletionSource<Download>();
        protected Subject<int> _progress = new Subject<int>();

        protected int Index { get; set; }
        public bool Continuation { get; protected set; }
        public Task<Download> Task => _source.Task;
        public IObservable<int> Progress => _progress;

        public abstract bool Handle(byte[] payload);
        public abstract byte[] Request(ushort seq);

        protected byte[] MakeRequest(ushort type, byte[] payload)
        {
            var outBuffer = new byte[2 + 2 + 4 + payload.Length];
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)0xCC55u), 0, outBuffer, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(type), 0, outBuffer, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, outBuffer, 4, 4);
            Buffer.BlockCopy(payload, 0, outBuffer, 8, payload.Length);
            Console.WriteLine($"Send: {outBuffer.Dump(64)}");
            return outBuffer;
        }

        protected byte[] MakeFileRequest(int chunkSize, ushort seq, byte type, string path)
        {
            var payload = new List<byte> { 0x04, 0x00, 0x08, 0x05, 0x34 };
            payload.AddRange(BitConverter.GetBytes(seq));
            Ext.AddRange<byte>(payload, 0x07, 0x0d, 0, 0, 0);
            payload.Add((byte)path.Length);
            payload.AddRange(Encoding.UTF8.GetBytes(path));
            Ext.AddRange<byte>(payload, 0, 0, (byte)((Index & 1) == 1 ? 0x80 : 0), (byte)(Index >> 1), 0, 0, 0, 0);
            payload.AddRange(BitConverter.GetBytes(chunkSize));
            payload.Add(type);
            return MakeRequest(0x574e, payload.ToArray());
        }
    }

    public class DownloadRaw : Download
    {
        const int DefaultChunkSize = 0x800000;

        public string Path { get; set; }
        public int FileSize { get; set; }
        public Stream Output { get; set; }

        private int _receivedSize;

        public override bool Handle(byte[] payload)
        {
            uint total = BitConverter.ToUInt32(payload, 4);
            uint received = BitConverter.ToUInt32(payload, 8);
            uint length = BitConverter.ToUInt32(payload, 12);
            Output.Write(payload, 32, payload.Length - 32);
            _progress.OnNext((int)((_receivedSize + received + length) / (float)FileSize * 100));
            if (received + length == total)
            {
                _receivedSize += DefaultChunkSize;
                Index += 1;
                Continuation = _receivedSize < FileSize;
                if (!Continuation) _source.SetResult(this);
                return true;
            }
            return false;
        }

        public override byte[] Request(ushort seq)
        {
            var chunkSize = DefaultChunkSize * (Index + 1) > FileSize ? FileSize - DefaultChunkSize * Index : DefaultChunkSize;
            return MakeFileRequest(chunkSize, seq, Type3FR, Path);
        }
    }

    public class DownloadJpeg : Download
    {
        const int DefaultChunkSize = 0x300000;

        public string Path { get; set; }
        public Stream Output { get; set; }

        public override bool Handle(byte[] payload)
        {
            uint total = BitConverter.ToUInt32(payload, 4);
            uint received = BitConverter.ToUInt32(payload, 8);
            uint length = BitConverter.ToUInt32(payload, 12);
            Output.Write(payload, 32, payload.Length - 32);
            _progress.OnNext((int)((received + length) / (float)total * 100));
            if (received + length == total)
            {
                _source.SetResult(this);
                return true;
            }
            return false;
        }

        public override byte[] Request(ushort seq)
        {
            return MakeFileRequest(DefaultChunkSize, seq, TypeJPG, Path);
        }
    }

    public class DownloadIndex : Download
    {
        private readonly int _sdIndex;
        byte[] _rest;

        public class Entry
        {
            public string Path { get; set; }
            public bool IsJpeg { get; set; }
            public int Size { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public List<Entry> Table { get; } = new List<Entry>();

        // sdIndex: 0 = SD1, 1 = SD2
        public DownloadIndex(int sdIndex)
        {
            _sdIndex = sdIndex;
        }

        public override bool Handle(byte[] payload)
        {
            var total = BitConverter.ToUInt32(payload, 4);
            var received = BitConverter.ToUInt32(payload, 8);
            var length = BitConverter.ToUInt32(payload, 12);
            var index = received == 0 ? 4 : 0;
            // rebuild payload
            if (_rest != null)
            {
                var updated = new byte[_rest.Length + payload.Length];
                Buffer.BlockCopy(_rest, 0, updated, 32, _rest.Length);
                Buffer.BlockCopy(payload, 32, updated, 32 + _rest.Length, payload.Length - 32);
                payload = updated;
                _rest = null;
            }
            // parse payload
            while (index < payload.Length - 32)
            {
                var offset = index + 32;
                if (offset + 0x1A > payload.Length) break;
                var type = payload[offset + 0x04];
                var fileSize = BitConverter.ToInt32(payload, offset + 0x05);
                var pathLength = BitConverter.ToInt16(payload, offset + 0x13);
                var payloadSize = BitConverter.ToInt16(payload, offset + 0x17);
                if (offset + 0x1A + pathLength + payloadSize > payload.Length) break;
                var path = Encoding.UTF8.GetString(payload, offset + 0x1A, pathLength);
                if (type != 0 && payloadSize > 0)
                {
                    var meta = new byte[payloadSize];
                    Buffer.BlockCopy(payload, offset + 0x1A + pathLength, meta, 0, payloadSize);
                    Table.Add(new Entry
                    {
                        IsJpeg = type != 0x01,
                        Size = fileSize,
                        Path = path,
                        CreatedAt = TinyTiff.GetDateTime(meta),
                    });
                }
                index += 0x1A + pathLength + payloadSize;
            }
            // check rest data
            if (index < payload.Length - 32)
            {
                _rest = new byte[payload.Length - index - 32];
                Buffer.BlockCopy(payload, index + 32, _rest, 0, _rest.Length);
            }
            if (received + length == total)
            {
                _source.SetResult(this);
                return true;
            }
            return false;
        }

        public override byte[] Request(ushort seq)
        {
            var payload = new List<byte> { 0x04, 0x00, 0x08, 0x05, 0x13 };
            payload.AddRange(BitConverter.GetBytes(seq));
            Ext.AddRange<byte>(payload, 0x07, 0x0e, 0, 0, 0, (byte)_sdIndex, 0, 0, 0, 0, 0, 0, 0, 0x0a, (byte)_sdIndex, 0, 0);
            return MakeRequest(0x574e, payload.ToArray());
        }
    }
}
