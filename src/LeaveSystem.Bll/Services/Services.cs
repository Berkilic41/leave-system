using LeaveSystem.Bll.DTOs;
using LeaveSystem.Bll.Helpers;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Bll.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IEmployeeRepository _employees;

    public AuthService(IUserRepository users, IEmployeeRepository employees)
    {
        _users = users;
        _employees = employees;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null) return AuthResult.Fail("Invalid email or password.");
        if (!user.IsActive) return AuthResult.Fail("This account is disabled.");
        if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return AuthResult.Fail("Invalid email or password.");
        var employee = await _employees.GetByUserIdAsync(user.Id);
        if (employee is null) return AuthResult.Fail("No employee profile linked to this account.");
        return AuthResult.Ok(user, employee);
    }
}

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repo;
    private readonly IAuditRepository _audit;

    public EmployeeService(IEmployeeRepository repo, IAuditRepository audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public Task<IEnumerable<Employee>> GetAllAsync(int? departmentId = null, string? search = null)
        => _repo.GetAllAsync(departmentId, search);

    public Task<Employee?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
    public Task<Employee?> GetByUserIdAsync(int userId) => _repo.GetByUserIdAsync(userId);
    public Task<IEnumerable<Employee>> GetTeamAsync(int managerId) => _repo.GetTeamAsync(managerId);
    public Task<IEnumerable<Employee>> GetAvailableManagersAsync() => _repo.GetAvailableManagersAsync();

    public async Task UpdateAsync(Employee e, int actorUserId)
    {
        var current = await _repo.GetByIdAsync(e.Id) ?? throw new KeyNotFoundException("Employee not found.");
        if (e.ManagerId == e.Id)
            throw new InvalidOperationException("An employee cannot be their own manager.");
        await _repo.UpdateAsync(e);
        await _audit.LogAsync("Employee", e.Id, actorUserId, "Update",
            $"Mgr={current.ManagerId} Dept={current.DepartmentId}",
            $"Mgr={e.ManagerId} Dept={e.DepartmentId}",
            null);
    }
}

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _repo;
    public DepartmentService(IDepartmentRepository repo) => _repo = repo;
    public Task<IEnumerable<Department>> GetAllAsync() => _repo.GetAllAsync();
}

public class LeaveTypeService : ILeaveTypeService
{
    private readonly ILeaveTypeRepository _repo;
    public LeaveTypeService(ILeaveTypeRepository repo) => _repo = repo;
    public Task<IEnumerable<LeaveType>> GetAllAsync() => _repo.GetAllAsync();
}

public class LeaveRequestService : ILeaveRequestService
{
    private readonly ILeaveRequestRepository _requests;
    private readonly IEmployeeRepository _employees;
    private readonly ILeaveTypeRepository _types;

    public LeaveRequestService(ILeaveRequestRepository requests, IEmployeeRepository employees, ILeaveTypeRepository types)
    {
        _requests = requests;
        _employees = employees;
        _types = types;
    }

    public async Task<int> SubmitAsync(int employeeId, int leaveTypeId, DateTime startDate, DateTime endDate, string? reason)
    {
        if (startDate > endDate) throw new InvalidOperationException("End date must be on or after start date.");
        if (startDate.Date < DateTime.UtcNow.Date) throw new InvalidOperationException("Start date cannot be in the past.");

        var leaveType = await _types.GetByIdAsync(leaveTypeId)
            ?? throw new InvalidOperationException("Leave type not found.");
        if (await _employees.GetByIdAsync(employeeId) is null)
            throw new InvalidOperationException("Employee not found.");

        // Check balance for paid leave types
        if (leaveType.IsPaid)
        {
            var balance = (await _requests.GetBalanceAsync(employeeId, startDate.Year)).FirstOrDefault(b => b.LeaveTypeId == leaveTypeId);
            if (balance is not null)
            {
                int requested = (endDate - startDate).Days + 1;
                if (balance.Remaining < requested)
                    throw new InvalidOperationException(
                        $"Insufficient balance. You have {balance.Remaining} {leaveType.Name} day(s) remaining (requested: {requested}).");
            }
        }

        return await _requests.CreateAsync(new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
    }

    public Task<LeaveRequest?> GetByIdAsync(int id) => _requests.GetByIdAsync(id);
    public Task<IEnumerable<LeaveRequest>> GetForEmployeeAsync(int employeeId) => _requests.GetForEmployeeAsync(employeeId);
    public Task<IEnumerable<LeaveRequest>> GetForManagerTeamAsync(int managerEmployeeId, string? statusFilter = null) => _requests.GetForManagerTeamAsync(managerEmployeeId, statusFilter);
    public Task<IEnumerable<LeaveRequest>> GetAllAsync(string? statusFilter = null) => _requests.GetAllAsync(statusFilter);
    public Task<IEnumerable<LeaveBalanceRow>> GetBalanceAsync(int employeeId, int year) => _requests.GetBalanceAsync(employeeId, year);

    public async Task<IEnumerable<LeaveRequest>> GetTeamApprovedInRangeAsync(int managerEmployeeId, DateTime fromDate, DateTime toDate)
    {
        var team = await _employees.GetTeamAsync(managerEmployeeId);
        var ids = team.Select(t => t.Id).ToList();
        // Manager themselves should also see their own leave on the team calendar? Probably not — only reports.
        return await _requests.GetApprovedInRangeAsync(ids, fromDate, toDate);
    }

    public async Task DecideAsync(int requestId, int actorUserId, string actorRole, int actorEmployeeId, string newStatus, string? note)
    {
        if (newStatus is not ("Approved" or "Rejected"))
            throw new InvalidOperationException("Status must be Approved or Rejected.");

        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException("Leave request not found.");

        // HR can decide any request. Manager can decide only direct/indirect reports.
        if (actorRole != "HR")
        {
            if (actorRole != "Manager") throw new UnauthorizedAccessException("Only managers or HR can decide leave requests.");
            var team = (await _employees.GetTeamAsync(actorEmployeeId)).Select(t => t.Id).ToHashSet();
            if (!team.Contains(request.EmployeeId))
                throw new UnauthorizedAccessException("You can only decide requests for your own team.");
        }

        await _requests.DecideAsync(requestId, actorUserId, newStatus, note);
    }

    public async Task CancelAsync(int requestId, int actorUserId, int actorEmployeeId)
    {
        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException("Leave request not found.");
        if (request.EmployeeId != actorEmployeeId)
            throw new UnauthorizedAccessException("You can only cancel your own requests.");
        if (request.Status != "Pending")
            throw new InvalidOperationException("Only Pending requests can be cancelled.");

        // Cancel uses the same SP plumbing — but the SP only allows Pending → ... transition.
        // We pipe "Cancelled" through the SP; the SP audits the change.
        await _requests.DecideAsync(requestId, actorUserId, "Cancelled", "Cancelled by employee");
    }
}

public class AuditService : IAuditService
{
    private readonly IAuditRepository _repo;
    public AuditService(IAuditRepository repo) => _repo = repo;
    public Task<IEnumerable<AuditEntry>> GetForEntityAsync(string entityType, int entityId) => _repo.GetForEntityAsync(entityType, entityId);
}

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _repo;
    public DashboardService(IDashboardRepository repo) => _repo = repo;
    public Task<HrDashboardData> GetHrDashboardAsync() => _repo.GetHrDashboardAsync();
}
