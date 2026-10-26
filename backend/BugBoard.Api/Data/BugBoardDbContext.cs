using BugBoard.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BugBoard.Api.Data;

public class BugBoardDbContext(DbContextOptions<BugBoardDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Bug> Bugs => Set<Bug>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).IsRequired().HasMaxLength(100);
            e.Property(u => u.PasswordHash).IsRequired();
            // Store enums as readable text ("Lead") instead of numbers (1).
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Bug>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Title).IsRequired().HasMaxLength(120);
            e.Property(b => b.Description).HasMaxLength(2000);
            e.Property(b => b.Severity).HasConversion<string>().HasMaxLength(20);
            e.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

            // Restrict: you cannot delete a user who still has bugs (no silent cascade deletes).
            e.HasOne(b => b.ReportedBy).WithMany().HasForeignKey(b => b.ReportedById)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.AssignedTo).WithMany().HasForeignKey(b => b.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            // Filtering on these columns is the main query, so index them.
            e.HasIndex(b => b.Status);
            e.HasIndex(b => b.Severity);
            e.HasIndex(b => b.UpdatedAt);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no real date type, so it returns DateTime with Kind=Unspecified.
        // This converter marks every DateTime we read back as UTC so JSON gets a trailing "Z".
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
