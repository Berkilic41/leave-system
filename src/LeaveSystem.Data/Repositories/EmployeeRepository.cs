using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;

namespace LeaveSystem.Data.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly DbConnectionFactory _factory;
    public EmployeeRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<Employee>> GetAllAsync(int? departmentId = null, string? search = null)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        var conditions = new List<string>();
        if (departmentId.HasValue) conditions.Add("e.DepartmentId = @D");
        if (!string.IsNullOrWhiteSpace(search)) conditions.Add("(e.FullName LIKE @S OR e.Position LIKE @S)");
        var where = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";

        using var cmd = new SqlCommand($@"
            SELECT e.Id, e.UserId, e.FullName, e.DepartmentId, d.Name, e.Position, e.HireDate, e.ManagerId,
                   m.FullName, u.Email, u.Username, u.Role
            FROM Employees e
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            INNER JOIN Users u ON u.Id = e.UserId
            LEFT JOIN Employees m ON m.Id = e.ManagerId
            {where}
            ORDER BY e.FullName", conn);
        if (departmentId.HasValue) cmd.Parameters.AddWithValue("@D", departmentId.Value);
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@S", $"%{search}%");

        var list = new List<Employee>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task<Employee?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT e.Id, e.UserId, e.FullName, e.DepartmentId, d.Name, e.Position, e.HireDate, e.ManagerId,
                   m.FullName, u.Email, u.Username, u.Role
            FROM Employees e
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            INNER JOIN Users u ON u.Id = e.UserId
            LEFT JOIN Employees m ON m.Id = e.ManagerId
            WHERE e.Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    public async Task<Employee?> GetByUserIdAsync(int userId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT e.Id, e.UserId, e.FullName, e.DepartmentId, d.Name, e.Position, e.HireDate, e.ManagerId,
                   m.FullName, u.Email, u.Username, u.Role
            FROM Employees e
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            INNER JOIN Users u ON u.Id = e.UserId
            LEFT JOIN Employees m ON m.Id = e.ManagerId
            WHERE e.UserId = @U", conn);
        cmd.Parameters.AddWithValue("@U", userId);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    public async Task<int> CreateAsync(Employee e)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            INSERT INTO Employees (UserId, FullName, DepartmentId, Position, HireDate, ManagerId)
            OUTPUT INSERTED.Id
            VALUES (@U, @N, @D, @P, @H, @M)", conn);
        cmd.Parameters.AddWithValue("@U", e.UserId);
        cmd.Parameters.AddWithValue("@N", e.FullName);
        cmd.Parameters.AddWithValue("@D", e.DepartmentId);
        cmd.Parameters.AddWithValue("@P", e.Position);
        cmd.Parameters.AddWithValue("@H", e.HireDate);
        cmd.Parameters.AddWithValue("@M", (object?)e.ManagerId ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateAsync(Employee e)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            UPDATE Employees
            SET FullName = @N, DepartmentId = @D, Position = @P, HireDate = @H, ManagerId = @M
            WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", e.Id);
        cmd.Parameters.AddWithValue("@N", e.FullName);
        cmd.Parameters.AddWithValue("@D", e.DepartmentId);
        cmd.Parameters.AddWithValue("@P", e.Position);
        cmd.Parameters.AddWithValue("@H", e.HireDate);
        cmd.Parameters.AddWithValue("@M", (object?)e.ManagerId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<Employee>> GetTeamAsync(int managerId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("sp_GetTeamHierarchy", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@ManagerId", managerId);
        var list = new List<Employee>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Employee
            {
                Id = r.GetInt32(0), UserId = r.GetInt32(1), FullName = r.GetString(2),
                DepartmentId = r.GetInt32(3), Position = r.GetString(4),
                HireDate = r.GetDateTime(5),
                ManagerId = r.IsDBNull(6) ? null : r.GetInt32(6),
                Depth = r.GetInt32(7),
                DepartmentName = r.GetString(8)
            });
        }
        return list;
    }

    public async Task<IEnumerable<Employee>> GetAvailableManagersAsync()
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT e.Id, e.UserId, e.FullName, e.DepartmentId, d.Name, e.Position, e.HireDate, e.ManagerId,
                   NULL, u.Email, u.Username, u.Role
            FROM Employees e
            INNER JOIN Departments d ON d.Id = e.DepartmentId
            INNER JOIN Users u ON u.Id = e.UserId
            WHERE u.Role IN ('Manager','HR')
            ORDER BY e.FullName", conn);
        var list = new List<Employee>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    private static Employee Map(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0), UserId = r.GetInt32(1), FullName = r.GetString(2),
        DepartmentId = r.GetInt32(3), DepartmentName = r.GetString(4),
        Position = r.GetString(5), HireDate = r.GetDateTime(6),
        ManagerId = r.IsDBNull(7) ? null : r.GetInt32(7),
        ManagerName = r.IsDBNull(8) ? null : r.GetString(8),
        Email = r.IsDBNull(9) ? null : r.GetString(9),
        Username = r.IsDBNull(10) ? null : r.GetString(10),
        Role = r.IsDBNull(11) ? null : r.GetString(11)
    };
}
