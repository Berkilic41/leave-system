using Moq;
using LeaveSystem.Bll.Services;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Entities;
using Xunit;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Tests for business logic used by ManagerController — DecideAsync and GetForManagerTeamAsync.
/// Controller-level routing tested indirectly via service layer.
/// </summary>
public class ManagerServiceTests
{
    private readonly Mock<ILeaveRequestRepository> _requests;
    private readonly Mock<IEmployeeRepository>     _employees;
    private readonly Mock<ILeaveTypeRepository>    _types;
    private readonly LeaveRequestService           _service;

    public ManagerServiceTests()
    {
        _requests  = new Mock<ILeaveRequestRepository>();
        _employees = new Mock<IEmployeeRepository>();
        _types     = new Mock<ILeaveTypeRepository>();
        _service   = new LeaveRequestService(_requests.Object, _employees.Object, _types.Object);
    }

    // ─── GetForManagerTeamAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetForManagerTeamAsync_DelegatesToRepo()
    {
        var pending = new[] { new LeaveRequest { Id = 1, Status = "Pending" } };
        _requests.Setup(r => r.GetForManagerTeamAsync(5, "Pending")).ReturnsAsync(pending);

        var result = await _service.GetForManagerTeamAsync(5, "Pending");

        Assert.Single(result);
        _requests.Verify(r => r.GetForManagerTeamAsync(5, "Pending"), Times.Once);
    }

    [Fact]
    public async Task GetForManagerTeamAsync_NoFilter_PassesNull()
    {
        _requests.Setup(r => r.GetForManagerTeamAsync(5, null)).ReturnsAsync([]);

        await _service.GetForManagerTeamAsync(5);

        _requests.Verify(r => r.GetForManagerTeamAsync(5, null), Times.Once);
    }

    // ─── GetAllAsync (HR path) ────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_DelegatesToRepo()
    {
        var all = new[] { new LeaveRequest { Id = 1, Status = "Approved" } };
        _requests.Setup(r => r.GetAllAsync("Approved")).ReturnsAsync(all);

        var result = await _service.GetAllAsync("Approved");

        Assert.Single(result);
        _requests.Verify(r => r.GetAllAsync("Approved"), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_NoFilter_PassesNull()
    {
        _requests.Setup(r => r.GetAllAsync(null)).ReturnsAsync([]);

        await _service.GetAllAsync();

        _requests.Verify(r => r.GetAllAsync(null), Times.Once);
    }

    // ─── DecideAsync — Manager Authorization ─────────────────────────────────

    [Fact]
    public async Task DecideAsync_ManagerCanApproveOwnTeam()
    {
        _requests.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 2, Status = "Pending" });
        _employees.Setup(r => r.GetTeamAsync(5))
            .ReturnsAsync(new[] { new Employee { Id = 2 } });
        _requests.Setup(r => r.DecideAsync(1, 10, "Approved", null)).Returns(Task.CompletedTask);

        await _service.DecideAsync(1, actorUserId: 10, "Manager", actorEmployeeId: 5, "Approved", null);

        _requests.Verify(r => r.DecideAsync(1, 10, "Approved", null), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_ManagerCannotApproveOutsideTeam()
    {
        _requests.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 99, Status = "Pending" });
        _employees.Setup(r => r.GetTeamAsync(5))
            .ReturnsAsync(new[] { new Employee { Id = 2 }, new Employee { Id = 3 } });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DecideAsync(1, 10, "Manager", 5, "Approved", null));

        _requests.Verify(r => r.DecideAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DecideAsync_HRCanApproveAnyRequest()
    {
        _requests.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 999, Status = "Pending" });
        _requests.Setup(r => r.DecideAsync(1, 10, "Approved", "OK")).Returns(Task.CompletedTask);

        await _service.DecideAsync(1, 10, "HR", 5, "Approved", "OK");

        _requests.Verify(r => r.DecideAsync(1, 10, "Approved", "OK"), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_HRCanRejectAnyRequest()
    {
        _requests.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 999, Status = "Pending" });
        _requests.Setup(r => r.DecideAsync(1, 10, "Rejected", "Not approved")).Returns(Task.CompletedTask);

        await _service.DecideAsync(1, 10, "HR", 5, "Rejected", "Not approved");

        _requests.Verify(r => r.DecideAsync(1, 10, "Rejected", "Not approved"), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_EmployeeRole_ThrowsUnauthorized()
    {
        _requests.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 1, Status = "Pending" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DecideAsync(1, 10, "Employee", 5, "Approved", null));
    }

    [Fact]
    public async Task DecideAsync_InvalidStatus_ThrowsInvalidOperation()
    {
        _requests.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 1, Status = "Pending" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DecideAsync(1, 10, "HR", 5, "Pending", null));
    }

    [Fact]
    public async Task DecideAsync_RequestNotFound_ThrowsKeyNotFound()
    {
        _requests.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((LeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.DecideAsync(999, 10, "HR", 5, "Approved", null));
    }

    // ─── GetTeamApprovedInRangeAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetTeamApprovedInRangeAsync_IncludesAllTeamMembers()
    {
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

        _employees.Setup(r => r.GetTeamAsync(5))
            .ReturnsAsync(new[] { new Employee { Id = 1 }, new Employee { Id = 2 } });
        _requests.Setup(r => r.GetApprovedInRangeAsync(
            It.Is<IEnumerable<int>>(ids => ids.Contains(1) && ids.Contains(2)), from, to))
            .ReturnsAsync(new[] { new LeaveRequest { Id = 10, Status = "Approved" } });

        var result = await _service.GetTeamApprovedInRangeAsync(5, from, to);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetTeamApprovedInRangeAsync_EmptyTeam_ReturnsEmpty()
    {
        var from = DateTime.UtcNow;
        var to   = from.AddMonths(1);
        _employees.Setup(r => r.GetTeamAsync(5)).ReturnsAsync([]);
        _requests.Setup(r => r.GetApprovedInRangeAsync(
            It.IsAny<IEnumerable<int>>(), from, to)).ReturnsAsync([]);

        var result = await _service.GetTeamApprovedInRangeAsync(5, from, to);

        Assert.Empty(result);
    }
}
