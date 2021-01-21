using DbConfigurationProvider;
using DbConfigurationProvider.Enitities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DbConfiguration
{
  public class DbConfigurationProvider : ConfigurationProvider
  {
    private readonly DbConfigurationSource _source;

    public DbConfigurationProvider(DbConfigurationSource source)
    {
      _source = source;
      if (source.ReloadOnChange)
      {
        EntityChangeObserver.Instance.Changed += EntityChangeObserver_Changed;
      }
    }

    public override void Load()
    {
      var builder = new DbContextOptionsBuilder<DemoContext>();
      _source.OptionsAction(builder);

      Data = new Dictionary<string, string>();

      using var context = new DemoContext(builder.Options);
      var items = context.Configs.AsNoTracking().ToList();

      foreach (var item in items)
      {
        var key = $"ConfigOptions:{item.Key}";
        Data[key] = item.Value;
      }
    }

    private void EntityChangeObserver_Changed(object sender, EntityChangeEventArgs e)
    {
      if (e.Entity.GetType() != typeof(ConfigEntity))
        return;

      Thread.Sleep(_source.ReloadDelay);
      Load();
    }
  }
}