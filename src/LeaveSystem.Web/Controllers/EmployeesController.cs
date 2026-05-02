using System.Security.Claims;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Web.Controllers;

[Authorize]
public class EmployeesController : Controller
{
    private readonly IEmployeeService _employees;
    private readonly IDepartmentService _departments;
    private readonly IAuditService _audit;
    private readonly ILeaveRequestService _leave;

    public EmployeesController(IEmployeeService employees, IDepartmentService departments, IAuditService audit, ILeaveRequestService leave)
    {
        _employees = employees;
        _departments = departments;
        _audit = audit;
        _leave = leave;
    }

    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> Index(int? departmentId, string? search)
    {
        ViewBag.DepartmentId = departmentId;
        ViewBag.Search = search;
        ViewBag.Departments = await _departments.GetAllAsync();
        return View(await _employees.GetAllAsync(departmentId, search));
    }

    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> Details(int id)
    {
        var employee = await _employees.GetByIdAsync(id);
        if (employee is null) return NotFound();
        ViewBag.Balance = await _leave.GetBalanceAsync(id, DateTime.UtcNow.Year);
        ViewBag.Requests = (await _leave.GetForEmployeeAsync(id)).Take(15);
        ViewBag.Audit = await _audit.GetForEntityAsync("Employee", id);
        return View(employee);
    }

    [HttpGet, Authorize(Roles = "HR")]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _employees.GetByIdAsync(id);
        if (e is null) return NotFound();
        return View(new EmployeeFormViewModel
        {
            Id = e.Id, FullName = e.FullName,
            DepartmentId = e.DepartmentId, Position = e.Position,
            HireDate = e.HireDate, ManagerId = e.ManagerId,
            Departments = await _departments.GetAllAsync(),
            AvailableManagers = await _employees.GetAvailableManagersAsync()
        });
    }

    [HttpPost, Authorize(Roles = "HR"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmployeeFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Departments = await _departments.GetAllAsync();
            vm.AvailableManagers = await _employees.GetAvailableManagersAsync();
            return View(vm);
        }
        try
        {
            var actorUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _employees.UpdateAsync(new Data.Entities.Employee
            {
                Id = vm.Id, FullName = vm.FullName,
                DepartmentId = vm.DepartmentId, Position = vm.Position,
                HireDate = vm.HireDate, ManagerId = vm.ManagerId
            }, actorUserId);
            TempData["Success"] = "Employee updated.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            vm.Departments = await _departments.GetAllAsync();
            vm.AvailableManagers = await _employees.GetAvailableManagersAsync();
            return View(vm);
        }
    }
}
