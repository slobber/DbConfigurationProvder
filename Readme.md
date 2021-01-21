程序开发中，有些信息是要根据环境改变的，比如开发环境的数据库可能是本地数据，而生产环境下需要连接生产数据库，我们需要把这些信息放到程序外面，在程序运行时通过读取这些外部信息实现不改变程序代码适应不同环境的需求，这些信息就是“配置”。

配置还有可能有不同来源，比如在 ASP.NET Core 中，框架本身已经为我们提供了以下“配置提供程序”：

* 设置文件，例如 appsettings.json
* 环境变量
* 命令行参数 \
...

以上这三种是常用的配置提供程序，而这几个提供程序有一个小问题：管理起来不太方便，尤其是在有大量配置内容的时候，容易出现问题。另外，如果是基于 Docker 的环境，这些信息修改起来一般来说会比较困难。

如果可以实现一个以数据库为配置提供程序，我们就可以提供专门的页面进行设置，同时动态的更新配置，可以极大的解放维护人员。

在 ASP.NET Core 中，我们一般通过使用“选项模式”读取配置信息。选项模式使用类来提供对相关设置组的强类型访问，具体介绍可参考官方文档。需要指出的是，为了能实现在配置变化后，能够读取到新值，我们需要在依赖注入时使用 `IOptionsSnapshot<T>`。
首先我们先创建一个 ASP.NET Core WebApi 项目，然后使用 VSCode 打开项目文件夹：

``` bash
> cd ~
> mkdir DbConfigurationProvider
> cd DbConfigurationProvider
> dotnet new webapi
> code .
```

为了适配配置提供程序，需要创建四个类：

``` csharp
// DbConfiguration/DbConfigurationSource.cs
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
```
``` csharp
// DbConfiguration/EntityChangeEventArgs.cs
using System;
using System.Threading;

namespace DbConfiguration
{
  public class EntityChangeEventArgs: EventArgs
  {
    public object Entity { get; }
    public EntityChangeEventArgs(object entity)
    {
      Entity = entity;
    }
  }
}
```
``` csharp
// DbConfiguration/EntityChangeObserver.cs
using System;

namespace DbConfiguration
{
  public class EntityChangeObserver
  {
    public event EventHandler<EntityChangeEventArgs> Changed;

    public void OnChanged(EntityChangeEventArgs e)
    {
      ThreadPool.QueueUserWorkItem((_) => Changed?.Invoke(this, e));
    }

    #region singleton

    private static readonly Lazy<EntityChangeObserver> lazy
      = new Lazy<EntityChangeObserver>(() => new EntityChangeObserver());

    private EntityChangeObserver() { }

    public static EntityChangeObserver Instance => lazy.Value;

    #endregion singleton
  }
}
```
``` csharp
// DbConfiguration/DbConfigurationProvider.cs
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
```

在以上代码中，我们分别定义了：
* 实现 `IConfigurationSource` 的 `DbConfigurationSource` 类：`IConfigurationSource` 它表示 `IConfiguration` 中的一个个的配置源，注册到 `IConfigurationBuilder` 中，形成一个 `IEnumerable<IConfigurationSource>` 列表。由于有不同的配置源，比如 JSON 文件，环境变量，INI 文件，XML 文件，Console 命令行参数等等，所以，需要有一个中间的源 Class 参与，这个中间的代理 Data 就是一个 key-value 键值对，能够产生这个键值对的类型是 `IConfigurationProvider`。
* 实现 `IConfigurationProvider` 的 `DbConfigurationProvider` (继承自 `ConfigurationProvider`)：由 `IConfigurationSource` 生成。这个类型中定义了一个可重载的 `Load` 方法，如何加载配置点。 \
在这个项目中，我们定义了一个 `ConfigEntity` 实体类，包含两个属性 `Key` 和 `Value`，我们需要把数据库读出来的实体列表，转换为一个 `Dictionary<string, string>` 的字典，即 `DbConfigurationProvider` 中的 `Data` 属性。\
需要注意的是，此字典的 key 需要从根开始命名，包含相应的 Section (在 `Startup.cs` 中 `AddOptions` 时的 Section)。
* 为了实现数据变化时可以监听到数据的变化，我们需要添加一个事件的观察者，即 `EntityChangeObserver` 类，其中定义了一个 `Changed` 事件，会在数据变化时发出这个事件，同时我们使用**单例模式**生成一个固定的实例。在 `DbConfigurationProvider` 中我们绑定了 `EntityChangeObserver_Changed` 事件处理函数，其中会调用数据库中最新的值，去更新配置内容。
* `EntityChangeEventArgs` 类为事件传递的参数，其中包含了发生改变的实体对象。

接下来，我们还需要注册配置源，注册的位置是在 `Programs.cs` 中：
``` csharp
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
```
在 `Host.CreateDefaultBuilder(args)` 之后添加 `ConfigureAppConfiguration` 函数，`CreateDefaultBuilder` 中会按照默认的设置自动注册 `appsettings.json` 文件配置源，命令行配置源等。

还需要在 `Startup.cs` 的 `ConfigureServices` 函数中注册相应的 Options 和数据库支持。

``` csharp
  services.AddOptions<ConfigOptions>().Bind(Configuration.GetSection("ConfigOptions"));
  services.AddDbContext<DemoContext>((sp, options) =>
  {
    options.UseSqlite("Data Source=demo.db");
  });
```
最后，我们需要添加所需的 `DbContext` 类，重载 `SaveChanges` 和 `SaveChangesAsync` 方法：
``` csharp
  public class DemoContext : DbContext
  {
    ……
    public override int SaveChanges()
    {
      OnEntityChange();
      return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      OnEntityChange();
      return await base.SaveChangesAsync(cancellationToken);
    }

    private void OnEntityChange()
    {
      var entries = ChangeTracker.Entries()
          .Where(i => i.State == EntityState.Modified || i.State == EntityState.Added || i.State == EntityState.Deleted);
      foreach (var entry in entries)
      {
        EntityChangeObserver.Instance.OnChanged(new EntityChangeEventArgs(entry.Entity));
      }
    }
    ……
  } 
```

这样我们就实现了一个这样我们就实现了一个使用数据库作为配置源的功能，还是挺简单的吧。

完整代码请访问：https://github.com/slobber/DbConfigurationProvder

可以通过以下 `curl` 命令测试接口情况：

``` bash
# 添加配置
curl -L -X POST 'http://localhost:5000/api/config' -H 'Content-Type: application/json' --data-raw '{
    "a": "a",
    "b": "b",
    "c": "c"
}'

# 读取配置项
curl -L -X GET 'http://localhost:5000/api/config'
```
