using Omni_MVC_2.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterServices(builder.Configuration);
var app = builder.Build();
app.ConfigurePipeline();
app.Run();