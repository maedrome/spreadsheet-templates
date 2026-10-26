using Api.Catalogo;
using Api.Compras;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<Preview> Previews => Set<Preview>();
    public DbSet<Bundle> Bundles => Set<Bundle>();
    public DbSet<BundleTemplate> BundleTemplates => Set<BundleTemplate>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderTemplate> OrderTemplates => Set<OrderTemplate>();
    public DbSet<PurchaseLink> PurchaseLinks => Set<PurchaseLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
