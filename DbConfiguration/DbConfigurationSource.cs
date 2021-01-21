using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;

namespace DbConfiguration
{
  public class DbConfigurationSource : IConfigurationSource
  {
    public Action<DbContextOptionsBuilder> OptionsAction { get; set; }

    public bool ReloadOnChange { get; set; }
    public int ReloadDelay { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
      return new DbConfigurationProvider(this);
    }
  }
}