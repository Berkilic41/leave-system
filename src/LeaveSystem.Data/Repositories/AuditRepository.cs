using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace LeaveSystem.Data.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly DbConnectionFactory _factory;
    public AuditRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task LogAsync(string entityType, int entityId, int actorUserId, string action, string? oldValue, string? newValue, string? notes)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            INSERT INTO AuditLog (EntityType, EntityId, ActorUserId, Action, OldValue, NewValue, Notes)
            VALUES (@T, @Id, @A, @Act, @OV, @NV, @N)", conn);
        cmd.Parameters.AddWithValue("@T", entityType);
        cmd.Parameters.AddWithValue("@Id", entityId);
        cmd.Parameters.AddWithValue("@A", actorUserId);
        cmd.Parameters.AddWithValue("@Act", action);
        cmd.Parameters.AddWithValue("@OV", (object?)oldValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NV", (object?)newValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@N", (object?)notes ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<AuditEntry>> GetForEntityAsync(string entityType, int entityId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT a.Id, a.EntityType, a.EntityId, a.ActorUserId, u.Username,
                   a.Action, a.OldValue, a.NewValue, a.Notes, a.[Timestamp]
            FROM AuditLog a INNER JOIN Users u ON u.Id = a.ActorUserId
            WHERE a.EntityType = @T AND a.EntityId = @Id
            ORDER BY a.[Timestamp] DESC", conn);
        cmd.Parameters.AddWithValue("@T", entityType);
        cmd.Parameters.AddWithValue("@Id", entityId);
        var list = new List<AuditEntry>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AuditEntry
            {
                Id = r.GetInt32(0), EntityType = r.GetString(1), EntityId = r.GetInt32(2),
                ActorUserId = r.GetInt32(3), ActorName = r.GetString(4),
                Action = r.GetString(5),
                OldValue = r.IsDBNull(6) ? null : r.GetString(6),
                NewValue = r.IsDBNull(7) ? null : r.GetString(7),
                Notes    = r.IsDBNull(8) ? null : r.GetString(8),
                Timestamp = r.GetDateTime(9)
            });
        return list;
    }
}
