using DbConfiguration;
using DbConfigurationProvider.Enitities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DbConfigurationProvider
{
  public class DemoContext : DbContext
  {
    public DbSet<ConfigEntity> Configs { get; set; }

    public DemoContext(DbContextOptions<DemoContext> options) : base(options)
    {
      Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
      base.OnModelCreating(builder);
      builder.Entity<ConfigEntity>(entity =>
      {
        entity.HasKey(e => e.Key);
      });
    }

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
  }
}