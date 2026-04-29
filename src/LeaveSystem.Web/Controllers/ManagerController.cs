using System.Security.Claims;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Web.Controllers;

[Authorize(Roles = "Manager,HR")]
public class ManagerController : Controller
{
    private readonly ILeaveRequestService _leave;
    private readonly IEmployeeService _employees;

    public ManagerController(ILeaveRequestService leave, IEmployeeService employees)
    {
        _leave = leave;
        _employees = employees;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private int CurrentEmpId  => int.Parse(User.FindFirstValue("EmployeeId")!);
    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)!;

    public async Task<IActionResult> Approvals(string? status)
    {
        ViewBag.Status = status ?? "Pending";
        var requests = User.IsInRole("HR")
            ? await _leave.GetAllAsync(string.IsNullOrEmpty(status) ? "Pending" : status)
            : await _leave.GetForManagerTeamAsync(CurrentEmpId, string.IsNullOrEmpty(status) ? "Pending" : status);
        return View(requests);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int id, string status, string? note)
    {
        try
        {
            await _leave.DecideAsync(id, CurrentUserId, CurrentRole, CurrentEmpId, status, note);
            TempData["Success"] = $"Request {status.ToLower()}.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Approvals));
    }

    public async Task<IActionResult> Calendar(DateTime? month)
    {
        var m = (month ?? DateTime.UtcNow).Date;
        m = new DateTime(m.Year, m.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = m.AddMonths(1).AddDays(-1);

        var team = User.IsInRole("HR")
            ? await _employees.GetAllAsync()
            : await _employees.GetTeamAsync(CurrentEmpId);

        var leaves = User.IsInRole("HR")
            ? await _leave.GetAllAsync("Approved") // for HR show all approved
            : await _leave.GetTeamApprovedInRangeAsync(CurrentEmpId, m, monthEnd);

        // Filter approved leaves to those overlapping the month
        leaves = leaves.Where(l => l.Status == "Approved" && l.StartDate <= monthEnd && l.EndDate >= m);

        return View(new TeamCalendarViewModel
        {
            Month = m,
            Team = team,
            ApprovedLeaves = leaves
        });
    }
}
