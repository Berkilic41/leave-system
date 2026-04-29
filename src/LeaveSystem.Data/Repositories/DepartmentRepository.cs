using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace LeaveSystem.Data.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly DbConnectionFactory _factory;
    public DepartmentRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<Department>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT d.Id, d.Name, (SELECT COUNT(*) FROM Employees e WHERE e.DepartmentId = d.Id) AS HC
            FROM Departments d ORDER BY d.Name", conn);
        var list = new List<Department>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Department { Id = r.GetInt32(0), Name = r.GetString(1), Headcount = r.GetInt32(2) });
        return list;
    }

    public async Task<int> CreateAsync(string name)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "INSERT INTO Departments (Name) OUTPUT INSERTED.Id VALUES (@N)", conn);
        cmd.Parameters.AddWithValue("@N", name);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }
}
