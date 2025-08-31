using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.SkPlugins
{
    public class SkPlugin : ISkPlugin
    {
        [KernelFunction("get_current_time")]
        [Description("Returns the current local time")]
        public Task<string> GetCurrentTimeAsync()
        {
            return Task.FromResult(DateTime.Now.ToString("O"));
        }

        [KernelFunction("get_weather_for_city")]
        [Description("Gets current weather for a given city")]
        public Task<string> GetWeatherForCityAsync(string city)
        {
            // call weather API...
            return Task.FromResult($"Weather for {city}: Sunny");
        }
    }
}
