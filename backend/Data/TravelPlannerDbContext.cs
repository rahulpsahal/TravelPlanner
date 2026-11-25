using Microsoft.EntityFrameworkCore;

namespace TravelPlanner.Api.Data;

public sealed class TravelPlannerDbContext(DbContextOptions<TravelPlannerDbContext> options) : DbContext(options)
{
    internal DbSet<User> Users => Set<User>();
    internal DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    internal DbSet<ApiLog> ApiLogs => Set<ApiLog>();

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

        var logs = modelBuilder.Entity<ApiLog>();
        logs.Property(log => log.RequestId).HasDefaultValueSql("NEWID()");
        logs.HasIndex(log => log.RequestId).IsUnique();
        logs.Property(log => log.MethodName).HasMaxLength(100);
        logs.Property(log => log.TraceId).HasMaxLength(100);
        logs.HasIndex(log => new { log.MethodName, log.CreatedAt });
        logs.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ApiLogs_RequestJson", "ISJSON([RequestJson]) = 1");
            table.HasCheckConstraint("CK_ApiLogs_ResponseJson", "ISJSON([ResponseJson]) = 1");
        });
    }
}
