using FluentValidation;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Omni_MVC_2.Extensions.Filters;
using Omni_MVC_2.Services;
using Omni_MVC_2.Utilities.ValidatorUtilities;

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

            // Add MVC with Global Filters
            services.AddControllersWithViews(options =>
            {
                options.Filters.Add<LogExecutionTimeFilter>();

            });

            // Add FluentValidations
            services.AddValidatorsFromAssembly(typeof(ModelValidator<>).Assembly);

            // Add Business Layer
            services.AddBusinessLayer(configuration);

            Console.WriteLine($"[Info]----->{nameof(RegisterServices)} service added");
            return services;
        }
    }
}