using System.Buffers.Text;
using System.Diagnostics;

namespace WebApplication;

internal static class DateHeader
{
    private const int PrefixLength = 8;
    private const int DateTimeRLength = 29;
    private const int SuffixLength = 2;
    private const int SuffixIndex = DateTimeRLength + PrefixLength;
    
    private static readonly Timer STimer = new((s) => {
        SetDateValues(DateTimeOffset.UtcNow);
    }, null, 1000, 1000);
    
    private static byte[] _sHeaderBytesMaster = new byte[PrefixLength + DateTimeRLength + 2 * SuffixLength];
    private static byte[] _sHeaderBytesScratch = new byte[PrefixLength + DateTimeRLength + 2 * SuffixLength];
    
    static DateHeader()
    {
        var utf8 = "\r\nDate: "u8;
        
        utf8.CopyTo(_sHeaderBytesMaster);
        utf8.CopyTo(_sHeaderBytesScratch);
        _sHeaderBytesMaster[SuffixIndex] = (byte)'\r';
        _sHeaderBytesMaster[SuffixIndex + 1] = (byte)'\n';
        _sHeaderBytesMaster[SuffixIndex + 2] = (byte)'\r';
        _sHeaderBytesMaster[SuffixIndex + 3] = (byte)'\n';
        _sHeaderBytesScratch[SuffixIndex] = (byte)'\r';
        _sHeaderBytesScratch[SuffixIndex + 1] = (byte)'\n';
        _sHeaderBytesScratch[SuffixIndex + 2] = (byte)'\r';
        _sHeaderBytesScratch[SuffixIndex + 3] = (byte)'\n';
        
        SetDateValues(DateTimeOffset.UtcNow);
        SyncDateTimer();
    }

    private static void SyncDateTimer()
    {
        STimer.Change(1000, 1000);
    }

    public static ReadOnlySpan<byte> HeaderBytes => _sHeaderBytesMaster;

    private static void SetDateValues(DateTimeOffset value)
    {
        lock (_sHeaderBytesScratch)
        {
            if (!Utf8Formatter.TryFormat(value, _sHeaderBytesScratch.AsSpan(PrefixLength), out var written, 'R'))
            {
                throw new Exception("date time format failed");
            }
            Debug.Assert(written == DateTimeRLength);
            (_sHeaderBytesScratch, _sHeaderBytesMaster) = (_sHeaderBytesMaster, _sHeaderBytesScratch);
        }
    }
}