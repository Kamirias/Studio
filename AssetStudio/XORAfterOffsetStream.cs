using System;
using System.IO;

namespace AssetStudio
{
    public class XORAfterOffsetStream : Stream
    {
        private readonly Stream _base;
        private readonly long _xorStartOffset;
        private readonly byte[] _pad;

        public XORAfterOffsetStream(Stream baseStream, long xorStartOffset, byte[] pad)
        {
            _base = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _xorStartOffset = xorStartOffset;
            _pad = pad ?? throw new ArgumentNullException(nameof(pad));
            if (_pad.Length == 0) throw new ArgumentException("Pad must not be empty", nameof(pad));
        }

        public override bool CanRead => _base.CanRead;
        public override bool CanSeek => _base.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _base.Length;
        public override long Position { get => _base.Position; set => _base.Position = value; }

        public override void Flush() => _base.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var startPos = _base.Position;
            var read = _base.Read(buffer, offset, count);
            if (read <= 0)
                return read;

            for (int i = 0; i < read; i++)
            {
                long absPos = startPos + i;
                if (absPos >= _xorStartOffset)
                {
                    var keyIndex = (int)(absPos % _pad.Length);
                    buffer[offset + i] ^= _pad[keyIndex];
                }
            }
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _base.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _base?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
