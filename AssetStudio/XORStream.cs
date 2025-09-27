using System.IO;

namespace AssetStudio
{
    public class XORStream : OffsetStream
    {
        private readonly byte[] _xorpad;
        private readonly long _offset;

        public XORStream(Stream stream, long offset, byte[] xorpad) : base(stream, offset)
        {
            _xorpad = xorpad;
            _offset = offset;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var startPos = AbsolutePosition;
            var read = base.Read(buffer, offset, count);
            if (read <= 0)
            {
                return read;
            }

            for (int i = 0; i < read; i++)
            {
                var absPos = startPos + i;
                if (absPos >= _offset)
                {
                    var keyIndex = (int)(absPos % _xorpad.Length);
                    buffer[offset + i] ^= _xorpad[keyIndex];
                }
            }
            return read;
        }
    }
}
