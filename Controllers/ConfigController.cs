using DbConfigurationProvider.Enitities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DbConfigurationProvider.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class ConfigController : ControllerBase
  {
    private DemoContext context;
    private ConfigOptions options;

    public ConfigController(DemoContext context, IOptionsSnapshot<ConfigOptions> optionsAccessor)
    {
      this.context = context;
      this.options = optionsAccessor.Value;
    }

    [HttpGet]
    public Dictionary<string, string> Index()
    {
      return options;
    }

    [HttpPost]
    public async Task<bool> PostAsync([FromBody] Dictionary<string, string> data)
    {
      var keys = data.Keys;
      var entities = await context.Configs.Where(x => keys.Contains(x.Key)).ToListAsync();
      context.RemoveRange(entities);

      foreach (var kv in data)
      {
        var entity = new ConfigEntity
        {
          Key = kv.Key,
          Value = kv.Value
        };
        context.Add(entity);
      }
      var result = await context.SaveChangesAsync();

      return result > 0;
    }
  }
}