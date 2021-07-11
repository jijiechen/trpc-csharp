using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
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
                // .ConfigureLogging((hostingContext, logging) => { logging.SetMinimumLevel(LogLevel.Debug); })
                .ConfigureServices(services => { services.AddTrpcServer(new IPEndPoint(IPAddress.Any, 8009)); })
                .UseKestrel()
                .UseStartup<Startup>();
    }
}
