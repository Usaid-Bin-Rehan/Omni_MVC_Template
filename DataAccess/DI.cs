namespace Omni_MVC_2.DataAccess
{
    public static class DI
    {
        public static IServiceCollection AddDALayer(this IServiceCollection services, IConfiguration configuration)
        {
            // Add AppDbContext & UoW here

            Console.WriteLine($"[Info]----->{nameof(AddDALayer)} service added");
            return services;
        }
    }
}
