using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace LeaveSystem.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DbConnectionFactory _factory;
    public UserRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<User?> GetByEmailAsync(string email) => await OneAsync("WHERE Email = @V", email);
    public async Task<User?> GetByIdAsync(int id)            => await OneAsync("WHERE Id = @V", id);

    private async Task<User?> OneAsync(string where, object value)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            $"SELECT Id, Username, Email, PasswordHash, PasswordSalt, Role, IsActive, CreatedAt FROM Users {where}", conn);
        cmd.Parameters.AddWithValue("@V", value);
        using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new User
        {
            Id = r.GetInt32(0), Username = r.GetString(1), Email = r.GetString(2),
            PasswordHash = r.GetString(3), PasswordSalt = r.GetString(4),
            Role = r.GetString(5), IsActive = r.GetBoolean(6), CreatedAt = r.GetDateTime(7)
        };
    }
}
