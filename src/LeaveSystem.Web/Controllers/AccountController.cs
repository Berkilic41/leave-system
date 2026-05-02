using System.Security.Claims;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LeaveSystem.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    public AccountController(IAuthService auth) => _auth = auth;

    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _auth.LoginAsync(vm.Email, vm.Password);
        if (!result.Success || result.User is null || result.Employee is null)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Login failed.");
            return View(vm);
        }

        var u = result.User;
        var emp = result.Employee;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Username),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.Role),
            new("EmployeeId", emp.Id.ToString()),
            new("FullName", emp.FullName),
            new("DepartmentId", emp.DepartmentId.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });

        if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() => View();
}
