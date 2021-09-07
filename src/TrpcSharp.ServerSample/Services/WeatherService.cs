using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using TrpcSharp.Server;

namespace TrpcSharp.ServerSample.Services
{
   
    public class WeatherService : WeatherForecasts.WeatherForecastsBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public override async Task<GetWeatherForecastsResponse> GetWeatherForecasts(Empty request, UnaryTrpcContext context)
        {
            context. 
            return response;
        }
    }
}