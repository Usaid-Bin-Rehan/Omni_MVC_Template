namespace Omni_MVC_2.Extensions
{
    public static class ConfigureApp
    {
        public static WebApplication UseServices(this WebApplication app)
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
            return app;
            #pragma warning restore ASP0014
        }
    }
}