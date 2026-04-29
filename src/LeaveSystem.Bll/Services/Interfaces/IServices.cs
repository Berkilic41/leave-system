using LeaveSystem.Bll.DTOs;
using LeaveSystem.Data.Entities;

namespace LeaveSystem.Bll.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password);
}

public interface IEmployeeService
{
    Task<IEnumerable<Employee>> GetAllAsync(int? departmentId = null, string? search = null);
    Task<Employee?> GetByIdAsync(int id);
    Task<Employee?> GetByUserIdAsync(int userId);
    Task<IEnumerable<Employee>> GetTeamAsync(int managerId);
    Task<IEnumerable<Employee>> GetAvailableManagersAsync();
    Task UpdateAsync(Employee employee, int actorUserId);
}

public interface IDepartmentService
{
    Task<IEnumerable<Department>> GetAllAsync();
}

public interface ILeaveTypeService
{
    Task<IEnumerable<LeaveType>> GetAllAsync();
}

public interface ILeaveRequestService
{
    Task<int> SubmitAsync(int employeeId, int leaveTypeId, DateTime startDate, DateTime endDate, string? reason);
    Task<LeaveRequest?> GetByIdAsync(int id);
    Task<IEnumerable<LeaveRequest>> GetForEmployeeAsync(int employeeId);
    Task<IEnumerable<LeaveRequest>> GetForManagerTeamAsync(int managerEmployeeId, string? statusFilter = null);
    Task<IEnumerable<LeaveRequest>> GetAllAsync(string? statusFilter = null);
    Task<IEnumerable<LeaveRequest>> GetTeamApprovedInRangeAsync(int managerEmployeeId, DateTime fromDate, DateTime toDate);
    Task<IEnumerable<LeaveBalanceRow>> GetBalanceAsync(int employeeId, int year);
    Task DecideAsync(int requestId, int actorUserId, string actorRole, int actorEmployeeId, string newStatus, string? note);
    Task CancelAsync(int requestId, int actorUserId, int actorEmployeeId);
}

public interface IAuditService
{
    Task<IEnumerable<AuditEntry>> GetForEntityAsync(string entityType, int entityId);
}

public interface IDashboardService
{
    Task<HrDashboardData> GetHrDashboardAsync();
}
