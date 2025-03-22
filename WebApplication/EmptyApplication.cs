using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using HttpMethod = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod;

namespace WebApplication;

public class EmptyApplication : IHttpConnection
{
    private static ReadOnlySpan<byte> DefaultPreamble =>
        "HTTP/1.1 200 OK\r\n"u8 +
        "Server: K\r\n"u8 +
        "Content-Type: text/plain\r\n"u8 +
        "Content-Length: 0"u8;
    
    private static Task Default(PipeWriter pipeWriter)
    {
        var writer = GetWriter(pipeWriter, sizeHint: DefaultPreamble.Length + DateHeader.HeaderBytes.Length);
        Default(ref writer);
        writer.Commit();
        return Task.CompletedTask;
    }
    
    private static void Default(ref BufferWriter<WriterAdapter> writer)
    {
        writer.Write(DefaultPreamble);

        // Date header
        writer.Write(DateHeader.HeaderBytes);
    }
    
    private static ReadOnlySpan<byte> PlainTextBody => "Hello, World!"u8;
    
    private static ReadOnlySpan<byte> PlaintextPreamble =>
        "HTTP/1.1 200 OK\r\n"u8 +
        "Server: K\r\n"u8 +
        "Content-Type: text/plain\r\n"u8 +
        "Content-Length: 13"u8;

    private static void PlainText(ref BufferWriter<WriterAdapter> writer)
    {
        writer.Write(PlaintextPreamble);

        // Date header
        writer.Write(DateHeader.HeaderBytes);

        // Body
        writer.Write(PlainTextBody);
    }

    private static class Paths
    {
        public static ReadOnlySpan<byte> Plaintext => "/plaintext"u8;
    }

    private RequestType _requestType;
    private int _queries;

    public void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
    {
        _requestType = versionAndMethod.Method == HttpMethod.Get
            ? GetRequestType(startLine.Slice(targetPath.Offset, targetPath.Length), ref _queries)
            : RequestType.NotRecognized;
    }

    private static RequestType GetRequestType(ReadOnlySpan<byte> path, ref int queries)
    {
        if (path.Length == 10 && path.SequenceEqual(Paths.Plaintext))
        {
            return RequestType.PlainText;
        }

        return RequestType.NotRecognized;
    }

    private bool ProcessRequest(ref BufferWriter<WriterAdapter> writer)
    {
        if (_requestType == RequestType.PlainText)
        {
            PlainText(ref writer);
        }
        else
        {
            return false;
        }

        return true;
    }

    private Task ProcessRequestAsync() => _requestType switch
    {
        _ => Default(Writer)
    };

    private enum RequestType
    {
        NotRecognized,
        PlainText
    }
    
    private State _state;
    public PipeReader Reader { get; set; }
    public PipeWriter Writer { get; set; }
    
    
    private HttpParser<ParsingAdapter> Parser { get; } = new();

    public async Task ExecuteAsync()
    {
        try
        {
            await ProcessRequestsAsync();
            await Reader.CompleteAsync();
        }
        catch (Exception ex)
        {
            await Reader.CompleteAsync(ex);
        }
        finally
        {
            await Writer.CompleteAsync();
        }
    }

    private async Task ProcessRequestsAsync()
    {
        while (true)
        {
            var readResult = await Reader.ReadAsync();
            var buffer = readResult.Buffer;
            var isCompleted = readResult.IsCompleted;

            if (buffer.IsEmpty && isCompleted)
            {
                return;
            }

            if (!HandleRequests(ref buffer, isCompleted))
            {
                await HandleRequestAsync(buffer);
            }
            
            await Writer.FlushAsync();
        }
    }
    
    private bool HandleRequests(ref ReadOnlySequence<byte> buffer, bool isCompleted)
    {
        var reader = new SequenceReader<byte>(buffer);
        var writer = GetWriter(Writer, sizeHint: 160 * 16);
        
        while (true)
        {
            ParseHttpRequest(ref reader, ref buffer, isCompleted);

            if (_state == State.Body)
            {
                if (!ProcessRequest(ref writer))
                {
                    return false;
                }

                _state = State.StartLine;

                if (!reader.End)
                {
                    continue;
                }
            }
            
            Reader.AdvanceTo(reader.Position, buffer.End);
            break;
        }
        
        writer.Commit();
        return true;
    }
    
    private async Task HandleRequestAsync(ReadOnlySequence<byte> buffer)
    {
        if (_state == State.Body)
        {
            await ProcessRequestAsync();
            
            _state = State.StartLine;
        }
        
        Reader.AdvanceTo(buffer.Start, buffer.End);
    }
    
    private void ParseHttpRequest(ref SequenceReader<byte> reader, ref ReadOnlySequence<byte> buffer, bool isCompleted)
    {
        var state = _state;
        
        if (state == State.StartLine)
        {
            if (Parser.ParseRequestLine(new ParsingAdapter(this), ref reader))
            {
                state = State.Headers;
            }
        }
        
        if (state == State.Headers)
        {
            var success = Parser.ParseHeaders(new ParsingAdapter(this), ref reader);

            if (success)
            {
                state = State.Body;
            }
        }
        
        if (state != State.Body && isCompleted)
        {
            ThrowUnexpectedEndOfData();
        }
        
        _state = state;
    }
    
    public void OnStaticIndexedHeader(int index) { }
    public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value) { }
    public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value) { }
    public void OnHeadersComplete(bool endStream) { }
    private static void ThrowUnexpectedEndOfData() { throw new InvalidOperationException("Unexpected end of data!"); }
    
    private enum State
    {
        StartLine,
        Headers,
        Body
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BufferWriter<WriterAdapter> GetWriter(PipeWriter pipeWriter, int sizeHint)
        => new(new(pipeWriter), sizeHint);
    
    private readonly struct WriterAdapter(PipeWriter writer) : IBufferWriter<byte>
    {
        private readonly PipeWriter Writer = writer;

        public void Advance(int count)
            => Writer.Advance(count);

        public Memory<byte> GetMemory(int sizeHint = 0)
            => Writer.GetMemory(sizeHint);

        public Span<byte> GetSpan(int sizeHint = 0)
            => Writer.GetSpan(sizeHint);
    }
    
    private readonly struct ParsingAdapter(EmptyApplication requestHandler) : IHttpRequestLineHandler, IHttpHeadersHandler
    {
        public void OnStaticIndexedHeader(int index)
            => requestHandler.OnStaticIndexedHeader(index);
        
        public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value)
            => requestHandler.OnStaticIndexedHeader(index, value);
        
        public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
            => requestHandler.OnHeader(name, value);
        
        public void OnHeadersComplete(bool endStream)
            => requestHandler.OnHeadersComplete(endStream);
        
        public void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
            => requestHandler.OnStartLine(versionAndMethod, targetPath, startLine);
    }
}