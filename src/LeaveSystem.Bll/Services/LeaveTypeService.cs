using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Bll.Services;

public class LeaveTypeService : ILeaveTypeService
{
    private readonly ILeaveTypeRepository _repo;

    public LeaveTypeService(ILeaveTypeRepository repo) => _repo = repo;

    public Task<IEnumerable<LeaveType>> GetAllAsync() => _repo.GetAllAsync();
}
