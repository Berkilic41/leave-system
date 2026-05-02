using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Bll.Services;

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _repo;

    public DashboardService(IDashboardRepository repo) => _repo = repo;

    public Task<HrDashboardData> GetHrDashboardAsync() => _repo.GetHrDashboardAsync();
}
