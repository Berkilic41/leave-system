using LeaveSystem.Bll.Services;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data;
using LeaveSystem.Data.Repositories;
using LeaveSystem.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10; opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; opt.QueueLimit = 0;
    });
    o.RejectionStatusCode = 429;
});

// Data layer
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<ILeaveTypeRepository, LeaveTypeRepository>();
builder.Services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();

// BLL
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<ILeaveTypeService, LeaveTypeService>();
builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

var app = builder.Build();

app.UseMiddleware<LeaveSystem.Web.Middleware.CorrelationIdMiddleware>();
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["X-XSS-Protection"]       = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"]        = "no-referrer";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:";
    await next();
});
app.UseRateLimiter();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapGet("/health", async (DbConnectionFactory db) =>
{
    try { using var c = db.CreateConnection(); await c.OpenAsync(); return Results.Ok(new { status = "healthy", database = "ok", timestamp = DateTime.UtcNow }); }
    catch { return Results.Json(new { status = "degraded", database = "error", timestamp = DateTime.UtcNow }, statusCode: 503); }
}).AllowAnonymous();

    Log.Information("LeaveSystem starting up");
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "LeaveSystem terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }

public partial class Program { }
