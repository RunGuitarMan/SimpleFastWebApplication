using System.Net;
using SimpleFastWebApplication;

DateHeader.SyncDateTimer();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHost(builder =>
    {
        builder.UseKestrel(options =>
        {
            options.Listen(new IPEndPoint(IPAddress.Loopback, 8080), listenOptions =>
            {
                listenOptions.Use(_ => new HttpApplication<EmptyApplication>().ExecuteAsync);
            });
        });
        
        builder.Configure(app => { });
        builder.UseSockets(options =>
        {
            options.WaitForDataBeforeAllocatingBuffer = false;
        });
    })
    .Build();

await host.RunAsync();