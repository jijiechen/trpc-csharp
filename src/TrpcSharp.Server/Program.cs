using System;
using TrpcSharp.Protocol.Standard;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using TrpcSharp.Server.Trpc;


namespace TrpcSharp.Server
{
    class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    // This shows how a custom framework could plug in an experience without using Kestrel APIs directly
                    services.AddTrpcServer(new IPEndPoint(IPAddress.Any, 8009));
                })
                .UseKestrel(options =>
                {

                    // HTTP 5000
                    options.ListenLocalhost(5000);

                    // HTTPS 5001
                    options.ListenLocalhost(5001, builder =>
                    {
                        builder.UseHttps();
                    });
                }).UseStartup<Startup>();
    }
}
