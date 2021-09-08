using TrpcSharp.Server;
using TrpcSharp.ServerSample.Services;

namespace TrpcSharp.ServerSample
{
    public class Startup
    {
        public void Configure(ITrpcApplicationBuilder app)
        {
            app.AddService<WeatherService>();
        }
    }
}