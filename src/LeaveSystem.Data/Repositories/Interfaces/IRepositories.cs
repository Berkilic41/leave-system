using LeaveSystem.Data.Entities;

namespace LeaveSystem.Data.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
}

public interface IDepartmentRepository
{
    Task<IEnumerable<Department>> GetAllAsync();
    Task<int> CreateAsync(string name);
}

public interface IEmployeeRepository
{
    Task<IEnumerable<Employee>> GetAllAsync(int? departmentId = null, string? search = null);
    Task<Employee?> GetByIdAsync(int id);
    Task<Employee?> GetByUserIdAsync(int userId);
    Task<int> CreateAsync(Employee employee);
    Task UpdateAsync(Employee employee);
    Task<IEnumerable<Employee>> GetTeamAsync(int managerId);
    Task<IEnumerable<Employee>> GetAvailableManagersAsync();
}

public interface ILeaveTypeRepository
{
    Task<IEnumerable<LeaveType>> GetAllAsync();
    Task<LeaveType?> GetByIdAsync(int id);
}

public interface ILeaveRequestRepository
{
    Task<IEnumerable<LeaveRequest>> GetForEmployeeAsync(int employeeId);
    Task<IEnumerable<LeaveRequest>> GetForManagerTeamAsync(int managerId, string? statusFilter = null);
    Task<IEnumerable<LeaveRequest>> GetAllAsync(string? statusFilter = null);
    Task<IEnumerable<LeaveRequest>> GetApprovedInRangeAsync(IEnumerable<int> employeeIds, DateTime fromDate, DateTime toDate);
    Task<LeaveRequest?> GetByIdAsync(int id);
    Task<int> CreateAsync(LeaveRequest request);
    /// <summary>Calls sp_DecideLeaveRequest. Throws if status not Pending or row not found.</summary>
    Task DecideAsync(int requestId, int actorUserId, string newStatus, string? note);
    Task<IEnumerable<LeaveBalanceRow>> GetBalanceAsync(int employeeId, int year);
}

public interface IAuditRepository
{
    Task LogAsync(string entityType, int entityId, int actorUserId, string action, string? oldValue, string? newValue, string? notes);
    Task<IEnumerable<AuditEntry>> GetForEntityAsync(string entityType, int entityId);
}

public interface IDashboardRepository
{
    Task<HrDashboardData> GetHrDashboardAsync();
}
