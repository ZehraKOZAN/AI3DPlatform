using Microsoft.EntityFrameworkCore;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductModel> ProductModels => Set<ProductModel>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasOne(p => p.User)
             .WithMany(u => u.Products)
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductImage>(e =>
        {
            e.HasOne(i => i.Product)
             .WithMany(p => p.Images)
             .HasForeignKey(i => i.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductModel>(e =>
        {
            e.HasOne(m => m.Product)
             .WithMany(p => p.Models)
             .HasForeignKey(m => m.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasIndex(k => k.KeyHash).IsUnique();
            e.HasOne(k => k.User)
             .WithMany(u => u.ApiKeys)
             .HasForeignKey(k => k.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Tenant isolation note: every query against Products/Images/Models in
        // application services MUST filter by the authenticated UserId. See
        // ProductService for the pattern used across this codebase.
    }
}
