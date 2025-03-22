using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace SimpleFastWebApplication;

public static class BufferExtensions
{
    private const int MaxULongByteLength = 20;

    [ThreadStatic]
    private static byte[]? _numericBytesScratch;

    internal static void WriteUtf8String<T>(ref this BufferWriter<T> buffer, string text)
        where T : struct, IBufferWriter<byte>
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);
        buffer.Ensure(byteCount);
        byteCount = Encoding.UTF8.GetBytes(text.AsSpan(), buffer.Span);
        buffer.Advance(byteCount);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void WriteNumericMultiWrite<T>(ref this BufferWriter<T> buffer, uint number)
        where T : IBufferWriter<byte>
    {
        const byte asciiDigitStart = (byte)'0';

        var value = number;
        var position = MaxULongByteLength;
        var byteBuffer = NumericBytesScratch;
        do
        {
            var quotient = value / 10;
            byteBuffer[--position] = (byte)(asciiDigitStart + (value - quotient * 10)); // 0x30 = '0'
            value = quotient;
        }
        while (value != 0);

        var length = MaxULongByteLength - position;
        buffer.Write(new ReadOnlySpan<byte>(byteBuffer, position, length));
    }

    private static byte[] NumericBytesScratch => _numericBytesScratch ?? CreateNumericBytesScratch();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte[] CreateNumericBytesScratch()
    {
        var bytes = new byte[MaxULongByteLength];
        _numericBytesScratch = bytes;
        return bytes;
    }
}
