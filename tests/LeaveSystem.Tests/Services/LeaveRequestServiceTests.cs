using Moq;
using Xunit;
using LeaveSystem.Bll.Services;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Tests.Services;

public class LeaveRequestServiceTests
{
    private readonly Mock<ILeaveRequestRepository> _mockRequests;
    private readonly Mock<IEmployeeRepository>     _mockEmployees;
    private readonly Mock<ILeaveTypeRepository>    _mockTypes;
    private readonly LeaveRequestService           _service;

    public LeaveRequestServiceTests()
    {
        _mockRequests  = new Mock<ILeaveRequestRepository>();
        _mockEmployees = new Mock<IEmployeeRepository>();
        _mockTypes     = new Mock<ILeaveTypeRepository>();
        _service = new LeaveRequestService(_mockRequests.Object, _mockEmployees.Object, _mockTypes.Object);
    }

    // ─── SubmitAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_ValidRequest_CreatesLeaveRequest()
    {
        var start = DateTime.UtcNow.AddDays(5).Date;
        var end   = start.AddDays(3);

        _mockEmployees.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Employee { Id = 1 });
        _mockTypes.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveType { Id = 1, IsPaid = false });
        _mockRequests.Setup(r => r.CreateAsync(It.IsAny<LeaveRequest>())).ReturnsAsync(100);

        var result = await _service.SubmitAsync(1, 1, start, end, "Vacation");

        Assert.Equal(100, result);
        _mockRequests.Verify(r => r.CreateAsync(It.Is<LeaveRequest>(lr =>
            lr.EmployeeId == 1 && lr.StartDate == start && lr.EndDate == end
        )), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhitespaceReason_StoresNull()
    {
        var start = DateTime.UtcNow.AddDays(5).Date;
        var end   = start.AddDays(2);

        _mockEmployees.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Employee { Id = 1 });
        _mockTypes.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveType { Id = 1, IsPaid = false });
        _mockRequests.Setup(r => r.CreateAsync(It.IsAny<LeaveRequest>())).ReturnsAsync(1);

        await _service.SubmitAsync(1, 1, start, end, "   ");

        _mockRequests.Verify(r => r.CreateAsync(It.Is<LeaveRequest>(lr => lr.Reason == null)), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_EndBeforeStart_ThrowsInvalidOperation()
    {
        var start = DateTime.UtcNow.AddDays(10).Date;
        var end   = start.AddDays(-2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(1, 1, start, end, null));
        Assert.Equal("End date must be on or after start date.", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_PastStartDate_ThrowsInvalidOperation()
    {
        var start = DateTime.UtcNow.AddDays(-1).Date;
        var end   = start.AddDays(2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(1, 1, start, end, null));
        Assert.Equal("Start date cannot be in the past.", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_LeaveTypeNotFound_ThrowsInvalidOperation()
    {
        var start = DateTime.UtcNow.AddDays(5).Date;
        _mockTypes.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((LeaveType?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(1, 999, start, start.AddDays(2), null));
        Assert.Equal("Leave type not found.", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_EmployeeNotFound_ThrowsInvalidOperation()
    {
        var start = DateTime.UtcNow.AddDays(5).Date;
        _mockTypes.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveType { Id = 1, IsPaid = false });
        _mockEmployees.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Employee?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(999, 1, start, start.AddDays(2), null));
        Assert.Equal("Employee not found.", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_PaidLeave_SufficientBalance_Succeeds()
    {
        var start = DateTime.UtcNow.AddDays(5).Date;
        var end   = start.AddDays(4); // 5 days

        _mockEmployees.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Employee { Id = 1 });
        _mockTypes.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveType { Id = 1, Name = "Vacation", IsPaid = true });
        _mockRequests.Setup(r => r.GetBalanceAsync(1, start.Year))
            .ReturnsAsync(new[] { new LeaveBalanceRow { LeaveTypeId = 1, AnnualDays = 20, UsedDays = 10, PendingDays = 0 } });
        _mockRequests.Setup(r => r.CreateAsync(It.IsAny<LeaveRequest>())).ReturnsAsync(1);

        await _service.SubmitAsync(1, 1, start, end, null);

        _mockRequests.Verify(r => r.CreateAsync(It.IsAny<LeaveRequest>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_PaidLeave_InsufficientBalance_Throws()
    {
        var start = DateTime.UtcNow.AddDays(5).Date;
        var end   = start.AddDays(9); // 10 days

        _mockEmployees.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Employee { Id = 1 });
        _mockTypes.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveType { Id = 1, Name = "Vacation", IsPaid = true });
        _mockRequests.Setup(r => r.GetBalanceAsync(1, start.Year))
            .ReturnsAsync(new[] { new LeaveBalanceRow { LeaveTypeId = 1, AnnualDays = 20, UsedDays = 15, PendingDays = 0 } });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(1, 1, start, end, null));
        Assert.Contains("Insufficient balance", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_UnpaidLeave_SkipsBalanceCheck()
    {
        var start = DateTime.UtcNow.AddDays(5).Date;

        _mockEmployees.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Employee { Id = 1 });
        _mockTypes.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveType { Id = 1, IsPaid = false });
        _mockRequests.Setup(r => r.CreateAsync(It.IsAny<LeaveRequest>())).ReturnsAsync(1);

        await _service.SubmitAsync(1, 1, start, start.AddDays(30), null);

        _mockRequests.Verify(r => r.GetBalanceAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockRequests.Verify(r => r.CreateAsync(It.IsAny<LeaveRequest>()), Times.Once);
    }

    // ─── DecideAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task DecideAsync_HRApprovesAnyRequest_Succeeds()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 99, Status = "Pending" });
        _mockRequests.Setup(r => r.DecideAsync(1, 10, "Approved", "ok")).Returns(Task.CompletedTask);

        await _service.DecideAsync(1, 10, "HR", 5, "Approved", "ok");

        _mockRequests.Verify(r => r.DecideAsync(1, 10, "Approved", "ok"), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_InvalidStatus_ThrowsInvalidOperation()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 1 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DecideAsync(1, 10, "HR", 5, "Cancelled", null));
        Assert.Equal("Status must be Approved or Rejected.", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_RequestNotFound_ThrowsKeyNotFound()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((LeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.DecideAsync(999, 10, "HR", 5, "Approved", null));
    }

    [Fact]
    public async Task DecideAsync_EmployeeRole_ThrowsUnauthorized()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 1 });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DecideAsync(1, 10, "Employee", 5, "Approved", null));
        Assert.Equal("Only managers or HR can decide leave requests.", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_ManagerApprovingOwnTeam_Succeeds()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 1 });
        _mockEmployees.Setup(r => r.GetTeamAsync(5)).ReturnsAsync(new[] { new Employee { Id = 1 } });
        _mockRequests.Setup(r => r.DecideAsync(1, 10, "Approved", null)).Returns(Task.CompletedTask);

        await _service.DecideAsync(1, 10, "Manager", 5, "Approved", null);

        _mockRequests.Verify(r => r.DecideAsync(1, 10, "Approved", null), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_ManagerApprovingOtherTeam_ThrowsUnauthorized()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 999 });
        _mockEmployees.Setup(r => r.GetTeamAsync(5)).ReturnsAsync(new[] { new Employee { Id = 1 }, new Employee { Id = 2 } });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DecideAsync(1, 10, "Manager", 5, "Approved", null));
        Assert.Equal("You can only decide requests for your own team.", ex.Message);
    }

    // ─── CancelAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_OwnPendingRequest_Succeeds()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 1, Status = "Pending" });
        _mockRequests.Setup(r => r.DecideAsync(1, 10, "Cancelled", "Cancelled by employee")).Returns(Task.CompletedTask);

        await _service.CancelAsync(1, 10, 1);

        _mockRequests.Verify(r => r.DecideAsync(1, 10, "Cancelled", "Cancelled by employee"), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_OtherEmployeeRequest_ThrowsUnauthorized()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 2, Status = "Pending" });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CancelAsync(1, 10, 1));
        Assert.Equal("You can only cancel your own requests.", ex.Message);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task CancelAsync_NonPendingRequest_ThrowsInvalidOperation(string status)
    {
        _mockRequests.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new LeaveRequest { Id = 1, EmployeeId = 1, Status = status });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CancelAsync(1, 10, 1));
        Assert.Equal("Only Pending requests can be cancelled.", ex.Message);
    }

    [Fact]
    public async Task CancelAsync_RequestNotFound_ThrowsKeyNotFound()
    {
        _mockRequests.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((LeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CancelAsync(999, 10, 1));
    }

    // ─── Read-only methods ───────────────────────────────────────────────────

    [Fact]
    public async Task GetBalanceAsync_ReturnsBalance()
    {
        var balances = new[] { new LeaveBalanceRow { LeaveTypeId = 1, AnnualDays = 20, UsedDays = 5, PendingDays = 2 } };
        _mockRequests.Setup(r => r.GetBalanceAsync(1, 2024)).ReturnsAsync(balances);

        var result = await _service.GetBalanceAsync(1, 2024);

        Assert.Single(result);
        Assert.Equal(13, result.First().Remaining);
    }

    [Fact]
    public async Task GetTeamApprovedInRangeAsync_ReturnsApprovedTeamRequests()
    {
        var from = DateTime.UtcNow.Date;
        var to   = from.AddDays(30);
        _mockEmployees.Setup(r => r.GetTeamAsync(5)).ReturnsAsync(new[] { new Employee { Id = 1 }, new Employee { Id = 2 } });
        _mockRequests.Setup(r => r.GetApprovedInRangeAsync(It.IsAny<IEnumerable<int>>(), from, to))
            .ReturnsAsync(new[] { new LeaveRequest { Id = 1, Status = "Approved" } });

        var result = await _service.GetTeamApprovedInRangeAsync(5, from, to);

        Assert.Single(result);
    }
}
