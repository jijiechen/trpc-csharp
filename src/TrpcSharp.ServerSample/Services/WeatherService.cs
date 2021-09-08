using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using TrpcSharp.Server;
using TrpcSharp.ServerSample.Proto;

namespace TrpcSharp.ServerSample.Services
{
   
    public class WeatherService :  WeatherForecasts.WeatherForecastsBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
    
        public override async Task<GetWeatherForecastsResponse> GetWeatherForecasts(Empty request, UnaryTrpcContext context)
        {
            var random = new Random();
            var response = new GetWeatherForecastsResponse();
            var forcast = new WeatherForecast
            {
                Date = Timestamp.FromDateTime(DateTime.UtcNow),
                Summary = Summaries[random.Next(0, Summaries.Length - 1)],
                TemperatureC = random.Next(16, 38)
            };
    
            response.Forecasts.Add(new[] {forcast});
            return response;
        }
    }
}