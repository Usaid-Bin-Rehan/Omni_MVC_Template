using Omni_MVC_2.Extensions;

var builder = WebApplication.CreateBuilder(args);           /// handles PowerShell $env:ASPNETCORE_ENVIRONMENT = "Development"
builder.Services.RegisterServices(builder.Configuration);
#region Disabled Kestrel Hosting for IIS-Server Reverse-Proxy
/// Client -> IIS-Server -> Kestrel -> MVC
//builder.WebHost.ConfigureKestrel(options => {
//    options.ListenAnyIP(5000);                                                                      // HTTP
//    options.ListenAnyIP(5001, listenOptions => { listenOptions.UseHttps("cert.pfx","password"); }); // HTTPs
//});
#endregion Disabled Kestrel Hosting for IIS-Server Reverse-Proxy
var app = builder.Build();
await app.UseServices();
app.Run();