namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.SkPlugins
{
    public interface ISkPlugin
    {
        Task<string> GetCurrentTimeAsync();
        Task<string> GetWeatherForCityAsync(string city);
    }
}
