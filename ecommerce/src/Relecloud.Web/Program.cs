using Relecloud.Web;

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

using Azure.Extensions.AspNetCore.Configuration.Secrets;

var builder = WebApplication.CreateBuilder(args);

var appConfigUri = builder.Configuration.GetValue<string>("App:AppConfig:Uri");

builder.Host.ConfigureAppConfiguration(builder =>
{
    //Connect to your App Config Store using the connection string    
    builder.AddAzureAppConfiguration(options => options.Connect(new Uri(appConfigUri), new DefaultAzureCredential())
        .ConfigureKeyVault(kv => kv.SetCredential(new DefaultAzureCredential())));
});

var startup = new Startup(builder.Configuration);

// Add services to the container
startup.ConfigureServices(builder.Services);

var app = builder.Build();

startup.Configure(app);

app.Run();