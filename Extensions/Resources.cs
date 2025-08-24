using FluentValidation;
using Omni_MVC_2.Services;
using Omni_MVC_2.Validators;

namespace Omni_MVC_2.Extensions
{
    public static class ConfigDI
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Add MVC
            services.AddControllersWithViews();

            // Add FluentValidations
            services.AddValidatorsFromAssemblyContaining<UserProfileInputValidator>();

            // Add Host Services
            services.AddScoped<IMyScopedService, MyScopedService>();

            // Add Business Layer
            services.AddBusinessLayer(configuration);

            Console.WriteLine($"[Info]----->{nameof(RegisterServices)} service added");
            return services;
        }
    }

    #region Host Services
    // Host Services eg Logging
    public interface IMyScopedService
    {
        string GetMessage();
    }

    public class MyScopedService : IMyScopedService
    {
        public string GetMessage() => "Hello from custom controller non-business service";
    }
    #endregion Host Services
}