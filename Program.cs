using DbConfiguration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DbConfigurationProvider
{
  public class Program
  {
    public static void Main(string[] args)
    {
      CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
          .ConfigureAppConfiguration((hostingContext, builder) =>
          {
            builder.Add(new DbConfigurationSource
            {
              OptionsAction = o => o.UseSqlite("Data Source=demo.db"),
              ReloadOnChange = true,
              ReloadDelay = 200
            });
          })
          .ConfigureWebHostDefaults(webBuilder =>
          {
            webBuilder.UseStartup<Startup>();
          });
  }
}