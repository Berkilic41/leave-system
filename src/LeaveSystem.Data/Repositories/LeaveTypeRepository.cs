using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace LeaveSystem.Data.Repositories;

public class LeaveTypeRepository : ILeaveTypeRepository
{
    private readonly DbConnectionFactory _factory;
    public LeaveTypeRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<LeaveType>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT Id, Name, DefaultAnnualQuota, IsPaid FROM LeaveTypes ORDER BY Name", conn);
        var list = new List<LeaveType>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new LeaveType { Id = r.GetInt32(0), Name = r.GetString(1), DefaultAnnualQuota = r.GetInt32(2), IsPaid = r.GetBoolean(3) });
        return list;
    }

    public async Task<LeaveType?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT Id, Name, DefaultAnnualQuota, IsPaid FROM LeaveTypes WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new LeaveType { Id = r.GetInt32(0), Name = r.GetString(1), DefaultAnnualQuota = r.GetInt32(2), IsPaid = r.GetBoolean(3) };
    }
}
