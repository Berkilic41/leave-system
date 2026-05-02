using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Bll.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _repo;

    public DepartmentService(IDepartmentRepository repo) => _repo = repo;

    public Task<IEnumerable<Department>> GetAllAsync() => _repo.GetAllAsync();
}
