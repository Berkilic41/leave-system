using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Bll.Services;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _repo;

    public AuditService(IAuditRepository repo) => _repo = repo;

    public Task<IEnumerable<AuditEntry>> GetForEntityAsync(string entityType, int entityId)
        => _repo.GetForEntityAsync(entityType, entityId);
}
