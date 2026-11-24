using Microsoft.EntityFrameworkCore;

namespace TravelPlanner.Api.Data;

internal sealed class TravelPlannerDbContext(DbContextOptions<TravelPlannerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var users = modelBuilder.Entity<User>();
        users.Property(user => user.PhoneNumber).HasMaxLength(16).IsUnicode(false);
        users.HasIndex(user => user.PhoneNumber).IsUnique();
        users.Property(user => user.DisplayName).HasMaxLength(100);

        var challenges = modelBuilder.Entity<OtpChallenge>();
        challenges.HasKey(challenge => challenge.PhoneNumber);
        challenges.Property(challenge => challenge.PhoneNumber).HasMaxLength(16).IsUnicode(false);
        challenges.HasIndex(challenge => challenge.Id).IsUnique();
        challenges.Property(challenge => challenge.CodeHash).HasMaxLength(32).IsFixedLength();
        challenges.Property(challenge => challenge.Version).IsRowVersion();
    }
}
