using System;
using Azure.Identity;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

[assembly: FunctionsStartup(typeof(Relecloud.FunctionApp.Startup))]

namespace Relecloud.FunctionApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            
        }

        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            builder.ConfigurationBuilder.AddAzureAppConfiguration(options =>
            {
                var appConfigUrl = Environment.GetEnvironmentVariable("AppConfigUrl");

                if (string.IsNullOrEmpty(appConfigUrl))
                    appConfigUrl = "https://vslive-setup-appconfig.azconfig.io";

                options.Connect(new Uri(appConfigUrl), new DefaultAzureCredential());
            });
        }
    }
}
