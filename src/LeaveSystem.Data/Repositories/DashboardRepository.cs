using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;

namespace LeaveSystem.Data.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly DbConnectionFactory _factory;
    public DashboardRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<HrDashboardData> GetHrDashboardAsync()
    {
        var data = new HrDashboardData();
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("sp_GetHrDashboard", conn) { CommandType = CommandType.StoredProcedure };
        using var r = await cmd.ExecuteReaderAsync();

        // 1: headcount
        while (await r.ReadAsync())
            data.Headcount.Add(new Department { Id = r.GetInt32(0), Name = r.GetString(1), Headcount = r.GetInt32(2) });

        // 2: on leave today
        await r.NextResultAsync();
        while (await r.ReadAsync())
            data.OnLeaveToday.Add(new Employee
            {
                Id = r.GetInt32(0), FullName = r.GetString(1),
                DepartmentName = r.GetString(2),
                Position = r.GetString(3),  // reused as leave type name
                HireDate = r.GetDateTime(4), // reused as leave start
                ManagerName = r.GetDateTime(5).ToString("yyyy-MM-dd") // reused as leave end
            });

        // 3: usage
        await r.NextResultAsync();
        while (await r.ReadAsync())
            data.Usage.Add(new LeaveTypeUsage
            {
                Id = r.GetInt32(0), Name = r.GetString(1),
                RequestCount = r.GetInt32(2), TotalDays = r.GetInt32(3)
            });

        // 4: top counts
        await r.NextResultAsync();
        if (await r.ReadAsync())
        {
            data.TotalEmployees = r.GetInt32(0);
            data.TotalDepartments = r.GetInt32(1);
            data.PendingRequests = r.GetInt32(2);
        }
        return data;
    }
}
