using System.IO.Pipelines;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;

namespace SimpleFastWebApplication;

public sealed class HttpApplication<TConnection> where TConnection : IHttpConnection, new()
{
    public Task ExecuteAsync(ConnectionContext connection)
    {
        var httpConnection = new TConnection
        {
            Reader = connection.Transport.Input,
            Writer = connection.Transport.Output
        };
        return httpConnection.ExecuteAsync();
    }
}

public interface IHttpConnection : IHttpHeadersHandler, IHttpRequestLineHandler
{
    PipeReader Reader { get; set; }
    PipeWriter Writer { get; set; }

    Task ExecuteAsync();
}
