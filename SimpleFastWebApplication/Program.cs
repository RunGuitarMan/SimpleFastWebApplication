using System.Net;
using SimpleFastWebApplication;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHost(web =>
    {
        web.UseKestrel(options =>
        {
            options.Listen(new IPEndPoint(IPAddress.Loopback, 8080), builder =>
            {
                builder.Use(_ => new HttpApplication<EmptyApplication>().ExecuteAsync);
            });
        });
        
        web.Configure(app => { });
    })
    .Build();

await host.RunAsync();