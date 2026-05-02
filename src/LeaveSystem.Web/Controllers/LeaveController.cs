using System.Security.Claims;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Web.Controllers;

[Authorize]
public class LeaveController : Controller
{
    private readonly ILeaveRequestService _leave;
    private readonly ILeaveTypeService    _types;

    public LeaveController(ILeaveRequestService leave, ILeaveTypeService types)
    {
        _leave = leave;
        _types = types;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private int CurrentEmpId  => int.Parse(User.FindFirstValue("EmployeeId")!);

    public async Task<IActionResult> Mine()
    {
        ViewBag.Balance = await _leave.GetBalanceAsync(CurrentEmpId, DateTime.UtcNow.Year);
        return View(await _leave.GetForEmployeeAsync(CurrentEmpId));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // Fetch leave types and balance in parallel — independent queries
        var typesTask   = _types.GetAllAsync();
        var balanceTask = _leave.GetBalanceAsync(CurrentEmpId, DateTime.UtcNow.Year);
        await Task.WhenAll(typesTask, balanceTask);

        return View(new LeaveRequestFormViewModel
        {
            LeaveTypes = typesTask.Result,
            Balance    = balanceTask.Result
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeaveRequestFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var typesTask   = _types.GetAllAsync();
            var balanceTask = _leave.GetBalanceAsync(CurrentEmpId, DateTime.UtcNow.Year);
            await Task.WhenAll(typesTask, balanceTask);
            vm.LeaveTypes = typesTask.Result;
            vm.Balance    = balanceTask.Result;
            return View(vm);
        }
        try
        {
            await _leave.SubmitAsync(CurrentEmpId, vm.LeaveTypeId, vm.StartDate, vm.EndDate, vm.Reason);
            TempData["Success"] = "Leave request submitted! Awaiting manager approval.";
            return RedirectToAction(nameof(Mine));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            var typesTask   = _types.GetAllAsync();
            var balanceTask = _leave.GetBalanceAsync(CurrentEmpId, DateTime.UtcNow.Year);
            await Task.WhenAll(typesTask, balanceTask);
            vm.LeaveTypes = typesTask.Result;
            vm.Balance    = balanceTask.Result;
            return View(vm);
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        var request = await _leave.GetByIdAsync(id);
        if (request is null) return NotFound();

        bool authorized = request.EmployeeId == CurrentEmpId
            || User.IsInRole("HR") || User.IsInRole("Manager");
        if (!authorized) return Forbid();

        return View(request);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await _leave.CancelAsync(id, CurrentUserId, CurrentEmpId);
            TempData["Success"] = "Leave request cancelled.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
