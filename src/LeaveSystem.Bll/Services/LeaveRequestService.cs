using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Bll.Services;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly ILeaveRequestRepository       _requests;
    private readonly IEmployeeRepository           _employees;
    private readonly ILeaveTypeRepository          _types;
    private readonly ILogger<LeaveRequestService>  _logger;

    public LeaveRequestService(
        ILeaveRequestRepository requests,
        IEmployeeRepository employees,
        ILeaveTypeRepository types,
        ILogger<LeaveRequestService> logger)
    {
        _requests  = requests;
        _employees = employees;
        _types     = types;
        _logger    = logger;
    }

    public async Task<int> SubmitAsync(
        int employeeId, int leaveTypeId, DateTime startDate, DateTime endDate, string? reason)
    {
        if (startDate > endDate)
            throw new InvalidOperationException("End date must be on or after start date.");
        if (startDate.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException("Start date cannot be in the past.");

        var leaveType = await _types.GetByIdAsync(leaveTypeId)
            ?? throw new InvalidOperationException("Leave type not found.");
        if (await _employees.GetByIdAsync(employeeId) is null)
            throw new InvalidOperationException("Employee not found.");

        if (leaveType.IsPaid)
        {
            var balance = (await _requests.GetBalanceAsync(employeeId, startDate.Year))
                .FirstOrDefault(b => b.LeaveTypeId == leaveTypeId);
            if (balance is not null)
            {
                int requested = (endDate - startDate).Days + 1;
                if (balance.Remaining < requested)
                    throw new InvalidOperationException(
                        $"Insufficient balance. You have {balance.Remaining} {leaveType.Name} day(s) remaining (requested: {requested}).");
            }
        }

        var requestId = await _requests.CreateAsync(new LeaveRequest
        {
            EmployeeId  = employeeId,
            LeaveTypeId = leaveTypeId,
            StartDate   = startDate.Date,
            EndDate     = endDate.Date,
            Reason      = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
        _logger.LogInformation("Leave request {RequestId} submitted by employee {EmployeeId} for {StartDate:d}–{EndDate:d}",
            requestId, employeeId, startDate.Date, endDate.Date);
        return requestId;
    }

    public Task<LeaveRequest?> GetByIdAsync(int id) => _requests.GetByIdAsync(id);

    public Task<IEnumerable<LeaveRequest>> GetForEmployeeAsync(int employeeId)
        => _requests.GetForEmployeeAsync(employeeId);

    public Task<IEnumerable<LeaveRequest>> GetForManagerTeamAsync(int managerEmployeeId, string? statusFilter = null)
        => _requests.GetForManagerTeamAsync(managerEmployeeId, statusFilter);

    public Task<IEnumerable<LeaveRequest>> GetAllAsync(string? statusFilter = null)
        => _requests.GetAllAsync(statusFilter);

    public Task<IEnumerable<LeaveBalanceRow>> GetBalanceAsync(int employeeId, int year)
        => _requests.GetBalanceAsync(employeeId, year);

    public async Task<IEnumerable<LeaveRequest>> GetTeamApprovedInRangeAsync(
        int managerEmployeeId, DateTime fromDate, DateTime toDate)
    {
        var team = await _employees.GetTeamAsync(managerEmployeeId);
        var ids = team.Select(t => t.Id).ToList();
        return await _requests.GetApprovedInRangeAsync(ids, fromDate, toDate);
    }

    public async Task DecideAsync(
        int requestId, int actorUserId, string actorRole, int actorEmployeeId,
        string newStatus, string? note)
    {
        if (newStatus is not ("Approved" or "Rejected"))
            throw new InvalidOperationException("Status must be Approved or Rejected.");

        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException("Leave request not found.");

        if (actorRole != "HR")
        {
            if (actorRole != "Manager")
                throw new UnauthorizedAccessException("Only managers or HR can decide leave requests.");
            var team = (await _employees.GetTeamAsync(actorEmployeeId)).Select(t => t.Id).ToHashSet();
            if (!team.Contains(request.EmployeeId))
                throw new UnauthorizedAccessException("You can only decide requests for your own team.");
        }

        await _requests.DecideAsync(requestId, actorUserId, newStatus, note);
        _logger.LogInformation("Leave request {RequestId} {Status} by user {ActorUserId} (role: {Role})",
            requestId, newStatus, actorUserId, actorRole);
    }

    public async Task CancelAsync(int requestId, int actorUserId, int actorEmployeeId)
    {
        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException("Leave request not found.");
        if (request.EmployeeId != actorEmployeeId)
            throw new UnauthorizedAccessException("You can only cancel your own requests.");
        if (request.Status != "Pending")
            throw new InvalidOperationException("Only Pending requests can be cancelled.");

        await _requests.DecideAsync(requestId, actorUserId, "Cancelled", "Cancelled by employee");
    }
}
