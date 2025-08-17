namespace Omni_MVC_2.Extensions
{
    public static class ConfigDI
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Add MVC
            services.AddControllersWithViews();

            // Register any custom services
            services.AddScoped<IMyScopedService, MyScopedService>();

            return services;
        }
    }

    // Example service
    public interface IMyScopedService
    {
        string GetMessage();
    }

    public class MyScopedService : IMyScopedService
    {
        public string GetMessage() => "Hello from MyScopedService";
    }
}