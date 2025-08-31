using Microsoft.EntityFrameworkCore;
using Omni_MVC_2.DataAccess.Connections;

namespace Omni_MVC_2.Extensions
{
    public static class ConfigureApp
    {
        public static async Task UseServices(this WebApplication app)
        {
            if (!app.Environment.IsDevelopment()) {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseRequestLogging();
            app.UseAuthorization();
            #pragma warning disable ASP0014
            app.UseEndpoints(endpoints => {
                //endpoints.MapAreaControllerRoute(name: "ProductsArea", areaName: "Products", pattern: "Products/{controller=Home}/{action=Index}/{id?}");
                endpoints.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
                endpoints.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
            });
            await app.UpdateDB();
            #pragma warning restore ASP0014
        }

        #region UpdateDB
        private static async Task UpdateDB(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
            if (!await dbContext.Database.CanConnectAsync())
            {
                if (!app.Environment.IsDevelopment()) throw new Exception("[Error] Database connection failed in non-development environment.");
                Console.WriteLine("[Warning] Cannot connect to the database. Skipping migration check.");
                return;
            }
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                Console.WriteLine("[Info] Applying pending migrations...");
                await dbContext.Database.MigrateAsync();
                Console.WriteLine("[Info] Applied pending migrations.");
            }
            else Console.WriteLine("[Info] No pending migrations found.");
        }
        #endregion UpdateDB
    }
}