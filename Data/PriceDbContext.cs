using BtcPriceService.Domain;
using Microsoft.EntityFrameworkCore;

namespace BtcPriceService.Data;

public sealed class PriceDbContext : DbContext
{
    public PriceDbContext(DbContextOptions<PriceDbContext> options) : base(options) { }

    public DbSet<PricePoint> PricePoints => Set<PricePoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PricePoint>()
            .HasIndex(p => new { p.Instrument, p.TimestampUtc })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}