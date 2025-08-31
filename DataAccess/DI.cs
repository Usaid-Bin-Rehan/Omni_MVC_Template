using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Omni_MVC_2.DataAccess.Connections;
using Omni_MVC_2.DataAccess.DataValidations;
using Omni_MVC_2.DataAccess.UnitOfWork;

namespace Omni_MVC_2.DataAccess
{
    public static class DI
    {
        public static IServiceCollection AddDALayer(this IServiceCollection services, IConfiguration configuration)
        {
            services
                    .AddDbContext(configuration)
                    .AddUnitOfWork()
                    .AddDataValidations()
                    ;

            Console.WriteLine($"[Info]----->{nameof(AddDALayer)} service added");
            return services;
        }

        #region Services
        public static IServiceCollection AddDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var DBhost = Environment.GetEnvironmentVariable("DBHost") ?? "Host=localhost;Port=5432;Database=Omni;Username=postgres;Password=postgres";
            ArgumentNullException.ThrowIfNullOrEmpty(DBhost, "[Error] Please add env:DBhost value.");
            services.AddDbContext<PgDbContext>(options => options.UseNpgsql(DBhost));
            return services;
        }

        public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
        {
            services.TryAddScoped<IUnitOfWork, PgUnitOfWork>();
            return services;
        }

        public static IServiceCollection AddDataValidations(this IServiceCollection services)
        {
            services.TryAddScoped<IDataValidations, PgDataValidations>();
            return services;
        }
        #endregion Services
    }
}