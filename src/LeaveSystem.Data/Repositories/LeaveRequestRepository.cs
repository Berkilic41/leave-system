using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace LeaveSystem.Data.Repositories;

public class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly DbConnectionFactory _factory;
    public LeaveRequestRepository(DbConnectionFactory factory) => _factory = factory;

    public Task<IEnumerable<LeaveRequest>> GetForEmployeeAsync(int employeeId)
        => QueryAsync("WHERE lr.EmployeeId = @V", ("@V", employeeId));

    public async Task<IEnumerable<LeaveRequest>> GetForManagerTeamAsync(int managerId, string? statusFilter = null)
    {
        // Use the hierarchy SP to find the manager's reports, then fetch their requests
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();

        var teamIds = new List<int>();
        using (var teamCmd = new SqlCommand("sp_GetTeamHierarchy", conn) { CommandType = CommandType.StoredProcedure })
        {
            teamCmd.Parameters.AddWithValue("@ManagerId", managerId);
            using var tr = await teamCmd.ExecuteReaderAsync();
            while (await tr.ReadAsync()) teamIds.Add(tr.GetInt32(0));
        }
        if (!teamIds.Any()) return [];

        var idCsv = string.Join(",", teamIds);
        var sql = new StringBuilder($@"
            {BaseSelect}
            WHERE lr.EmployeeId IN ({idCsv})");
        if (!string.IsNullOrWhiteSpace(statusFilter)) sql.Append(" AND lr.Status = @S");
        sql.Append(" ORDER BY lr.CreatedAt DESC");

        using var cmd = new SqlCommand(sql.ToString(), conn);
        if (!string.IsNullOrWhiteSpace(statusFilter)) cmd.Parameters.AddWithValue("@S", statusFilter);
        var list = new List<LeaveRequest>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public Task<IEnumerable<LeaveRequest>> GetAllAsync(string? statusFilter = null)
        => string.IsNullOrWhiteSpace(statusFilter)
            ? QueryAsync("")
            : QueryAsync("WHERE lr.Status = @V", ("@V", statusFilter));

    public async Task<IEnumerable<LeaveRequest>> GetApprovedInRangeAsync(IEnumerable<int> employeeIds, DateTime fromDate, DateTime toDate)
    {
        var ids = employeeIds.ToList();
        if (!ids.Any()) return [];

        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        var idCsv = string.Join(",", ids);
        using var cmd = new SqlCommand($@"
            {BaseSelect}
            WHERE lr.Status = 'Approved'
              AND lr.EmployeeId IN ({idCsv})
              AND lr.StartDate <= @To
              AND lr.EndDate   >= @From
            ORDER BY lr.StartDate", conn);
        cmd.Parameters.AddWithValue("@From", fromDate);
        cmd.Parameters.AddWithValue("@To", toDate);
        var list = new List<LeaveRequest>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task<LeaveRequest?> GetByIdAsync(int id)
    {
        var rows = await QueryAsync("WHERE lr.Id = @V", ("@V", id));
        return rows.FirstOrDefault();
    }

    public async Task<int> CreateAsync(LeaveRequest req)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, StartDate, EndDate, Reason, Status)
            OUTPUT INSERTED.Id
            VALUES (@E, @T, @S, @End, @R, 'Pending')", conn);
        cmd.Parameters.AddWithValue("@E", req.EmployeeId);
        cmd.Parameters.AddWithValue("@T", req.LeaveTypeId);
        cmd.Parameters.AddWithValue("@S", req.StartDate);
        cmd.Parameters.AddWithValue("@End", req.EndDate);
        cmd.Parameters.AddWithValue("@R", (object?)req.Reason ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task DecideAsync(int requestId, int actorUserId, string newStatus, string? note)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("sp_DecideLeaveRequest", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@RequestId", requestId);
        cmd.Parameters.AddWithValue("@ActorUserId", actorUserId);
        cmd.Parameters.AddWithValue("@NewStatus", newStatus);
        cmd.Parameters.AddWithValue("@Note", (object?)note ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<LeaveBalanceRow>> GetBalanceAsync(int employeeId, int year)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("sp_GetLeaveBalance", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
        cmd.Parameters.AddWithValue("@Year", year);
        var list = new List<LeaveBalanceRow>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new LeaveBalanceRow
            {
                LeaveTypeId = r.GetInt32(0), LeaveTypeName = r.GetString(1),
                IsPaid = r.GetBoolean(2),
                AnnualDays = r.GetInt32(3),
                UsedDays = r.GetInt32(4),
                PendingDays = r.GetInt32(5)
            });
        return list;
    }

    private const string BaseSelect = @"
        SELECT lr.Id, lr.EmployeeId, e.FullName, d.Name AS DeptName,
               lr.LeaveTypeId, lt.Name AS TypeName,
               lr.StartDate, lr.EndDate, lr.Reason, lr.Status,
               lr.DecidedAt, lr.DecidedByUserId, db.Username AS DecidedBy,
               lr.DecisionNote, lr.CreatedAt
        FROM LeaveRequests lr
        INNER JOIN Employees   e  ON e.Id  = lr.EmployeeId
        INNER JOIN Departments d  ON d.Id  = e.DepartmentId
        INNER JOIN LeaveTypes  lt ON lt.Id = lr.LeaveTypeId
        LEFT JOIN  Users       db ON db.Id = lr.DecidedByUserId";

    private async Task<IEnumerable<LeaveRequest>> QueryAsync(string where, params (string, object)[] parameters)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand($"{BaseSelect} {where} ORDER BY lr.CreatedAt DESC", conn);
        foreach (var (n, v) in parameters) cmd.Parameters.AddWithValue(n, v);
        var list = new List<LeaveRequest>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    private static LeaveRequest Map(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0), EmployeeId = r.GetInt32(1),
        EmployeeName = r.GetString(2), DepartmentName = r.GetString(3),
        LeaveTypeId = r.GetInt32(4), LeaveTypeName = r.GetString(5),
        StartDate = r.GetDateTime(6), EndDate = r.GetDateTime(7),
        Reason = r.IsDBNull(8) ? null : r.GetString(8),
        Status = r.GetString(9),
        DecidedAt = r.IsDBNull(10) ? null : r.GetDateTime(10),
        DecidedByUserId = r.IsDBNull(11) ? null : r.GetInt32(11),
        DecidedByName = r.IsDBNull(12) ? null : r.GetString(12),
        DecisionNote = r.IsDBNull(13) ? null : r.GetString(13),
        CreatedAt = r.GetDateTime(14)
    };
}
