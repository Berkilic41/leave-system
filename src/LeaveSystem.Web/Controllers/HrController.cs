using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Web.Controllers;

[Authorize(Roles = "HR")]
public class HrController : Controller
{
    private readonly IDashboardService _dashboard;
    public HrController(IDashboardService dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index()
        => View(new HrDashboardViewModel { Data = await _dashboard.GetHrDashboardAsync() });
}
