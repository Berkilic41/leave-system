using System.Security.Claims;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IEmployeeService _employees;
    private readonly ILeaveRequestService _leave;

    public HomeController(IEmployeeService employees, ILeaveRequestService leave)
    {
        _employees = employees;
        _leave = leave;
    }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("HR")) return RedirectToAction("Index", "Hr");

        var empId = int.Parse(User.FindFirstValue("EmployeeId")!);
        var employee = await _employees.GetByIdAsync(empId);
        if (employee is null) return Unauthorized();

        return View(new HomeViewModel
        {
            Employee = employee,
            Year = DateTime.UtcNow.Year,
            Balance = await _leave.GetBalanceAsync(empId, DateTime.UtcNow.Year),
            RecentRequests = (await _leave.GetForEmployeeAsync(empId)).Take(10)
        });
    }
}
