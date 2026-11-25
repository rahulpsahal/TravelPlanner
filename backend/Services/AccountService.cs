using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TravelPlanner.Api.Data;
using TravelPlanner.Api.DTOs;

namespace TravelPlanner.Api.Services;

public sealed class AccountService(IDbContextFactory<TravelPlannerDbContext> databaseFactory)
{
    public async Task<CurrentUserResponse?> FindByPhoneAsync(string phone, CancellationToken token)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(token);
        return await database.Users.Where(user => user.PhoneNumber == phone)
            .Select(user => new CurrentUserResponse(user.Id, user.DisplayName, user.PhoneNumber))
            .SingleOrDefaultAsync(token);
    }

    public async Task<CurrentUserResponse?> FindByIdAsync(Guid id, CancellationToken token)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(token);
        return await database.Users.Where(user => user.Id == id)
            .Select(user => new CurrentUserResponse(user.Id, user.DisplayName, user.PhoneNumber))
            .SingleOrDefaultAsync(token);
    }

    public async Task<CurrentUserResponse?> RegisterAsync(string verifiedPhone, string displayName, CancellationToken token)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(token);
        var user = new User
        {
            Id = Guid.NewGuid(), DisplayName = displayName,
            PhoneNumber = verifiedPhone, CreatedAt = DateTimeOffset.UtcNow
        };
        database.Users.Add(user);
        try
        {
            await database.SaveChangesAsync(token);
            return new CurrentUserResponse(user.Id, user.DisplayName, user.PhoneNumber);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return null;
        }
    }
}
