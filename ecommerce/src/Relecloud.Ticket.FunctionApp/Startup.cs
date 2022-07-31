using Azure.Identity;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Relecloud.Ticket.FunctionApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


[assembly: FunctionsStartup(typeof(Startup))]
namespace Relecloud.Ticket.FunctionApp
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

                options.Connect(new Uri(appConfigUrl), new DefaultAzureCredential())
                    .ConfigureKeyVault(options => options.SetCredential(new DefaultAzureCredential()));
            });
        }
    }
}
