using FluentValidation;
using Omni_MVC_2.Services;
using Omni_MVC_2.Validators;

namespace Omni_MVC_2.Extensions
{
    public static class ConfigDI
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {

            #region Session Management

            //services.AddDistributedMemoryCache();

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = "localhost:6379";
                options.InstanceName = "MyApp:";
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            #endregion Session Management

            // Add MVC
            services.AddControllersWithViews();

            // Add FluentValidations
            services.AddValidatorsFromAssemblyContaining<UserProfileInputValidator>();

            // Add Business Layer
            services.AddBusinessLayer(configuration);

            Console.WriteLine($"[Info]----->{nameof(RegisterServices)} service added");
            return services;
        }
    }
}