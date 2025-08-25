using Microsoft.Extensions.DependencyInjection.Extensions;
using Omni_MVC_2.Areas.Products.Services;
using Omni_MVC_2.DataAccess;
using Omni_MVC_2.Services.HomeService;

namespace Omni_MVC_2.Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBusinessLayer(this IServiceCollection services, IConfiguration configuration)
        {
            services
                    .AddDALayer(configuration)
                    .AddServices();

            Console.WriteLine($"[Info]----->{nameof(AddBusinessLayer)} service added");
            return services;
        }

        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.TryAddScoped<IHomeService, OmniHomeService>();
            services.TryAddScoped<IProductService, OmniProductService>();

            Console.WriteLine($"[Info]----->{nameof(AddServices)} service added");
            return services;
        }
    }
}
