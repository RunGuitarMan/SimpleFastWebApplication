using System.Buffers;
using System.Runtime.CompilerServices;

namespace SimpleFastWebApplication;

public ref struct BufferWriter<T> where T : IBufferWriter<byte>
{
    private T _output;
    private Span<byte> _span;
    private int _buffered;

    public BufferWriter(T output, int sizeHint)
    {
        _buffered = 0;
        _output = output;
        _span = output.GetSpan(sizeHint);
    }

    public Span<byte> Span => _span;

    public T Output => _output;

    public int Buffered => _buffered;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Commit()
    {
        var buffered = _buffered;
        if (buffered > 0)
        {
            _buffered = 0;
            _output.Advance(buffered);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        _buffered += count;
        _span = _span.Slice(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ReadOnlySpan<byte> source)
    {
        if (_span.Length >= source.Length)
        {
            source.CopyTo(_span);
            Advance(source.Length);
        }
        else
        {
            WriteMultiBuffer(source);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Ensure(int count = 1)
    {
        if (_span.Length < count)
        {
            EnsureMore(count);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureMore(int count = 0)
    {
        if (_buffered > 0)
        {
            Commit();
        }

        _span = _output.GetSpan(count);
    }

    private void WriteMultiBuffer(scoped ReadOnlySpan<byte> source)
    {
        while (source.Length > 0)
        {
            if (_span.Length == 0)
            {
                EnsureMore();
            }

            var writable = Math.Min(source.Length, _span.Length);
            source.Slice(0, writable).CopyTo(_span);
            source = source.Slice(writable);
            Advance(writable);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteNumeric(uint number)
    {
        const byte asciiDigitStart = (byte)'0';

        var span = Span;

        var advanceBy = 0;
        if (span.Length >= 3)
        {
            if (number < 10)
            {
                span[0] = (byte)(number + asciiDigitStart);
                advanceBy = 1;
            }
            else if (number < 100)
            {
                var tens = (byte)((number * 205u) >> 11); // div10, valid to 1028

                span[0] = (byte)(tens + asciiDigitStart);
                span[1] = (byte)(number - (tens * 10) + asciiDigitStart);
                advanceBy = 2;
            }
            else if (number < 1000)
            {
                var digit0 = (byte)((number * 41u) >> 12); // div100, valid to 1098
                var digits01 = (byte)((number * 205u) >> 11); // div10, valid to 1028

                span[0] = (byte)(digit0 + asciiDigitStart);
                span[1] = (byte)(digits01 - (digit0 * 10) + asciiDigitStart);
                span[2] = (byte)(number - (digits01 * 10) + asciiDigitStart);
                advanceBy = 3;
            }
        }

        if (advanceBy > 0)
        {
            Advance(advanceBy);
        }
        else
        {
            this.WriteNumericMultiWrite(number);
        }
    }
}
