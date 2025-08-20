using finanzas.api.Domain;
using Microsoft.EntityFrameworkCore;

namespace finanzas.api.Infra;

public class FinanzasDbContext : DbContext
{
    public FinanzasDbContext(DbContextOptions<FinanzasDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Movement> Movements => Set<Movement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        });

        b.Entity<Movement>(e =>
        {
            e.ToTable("movements");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Category).WithMany(c => c.Movements).HasForeignKey(x => x.CategoryId);
            e.HasIndex(x => new { x.UserId, x.Date });
        });
    }
}
