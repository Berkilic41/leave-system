using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Bll.Services;

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
        var current = await _repo.GetByIdAsync(e.Id)
            ?? throw new KeyNotFoundException("Employee not found.");
        if (e.ManagerId == e.Id)
            throw new InvalidOperationException("An employee cannot be their own manager.");
        await _repo.UpdateAsync(e);
        await _audit.LogAsync("Employee", e.Id, actorUserId, "Update",
            $"Mgr={current.ManagerId} Dept={current.DepartmentId}",
            $"Mgr={e.ManagerId} Dept={e.DepartmentId}",
            null);
    }
}
