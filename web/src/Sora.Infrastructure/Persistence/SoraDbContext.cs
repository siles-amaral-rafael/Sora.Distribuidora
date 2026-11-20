using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;

namespace Sora.Infrastructure.Persistence;

public sealed class SoraDbContext(DbContextOptions<SoraDbContext> options) : DbContext(options)
{
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var account = modelBuilder.Entity<StaffAccount>();
        account.ToTable("staff_accounts");
        account.HasKey(x => x.Id);
        account.Property(x => x.Id).HasColumnName("id");
        account.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
        account.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        account.HasIndex(x => x.Email).IsUnique();
        account.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(100).IsRequired();
        account.Property(x => x.RoleName).HasColumnName("role_name").HasMaxLength(40).IsRequired();
        account.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        account.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
    }
}
