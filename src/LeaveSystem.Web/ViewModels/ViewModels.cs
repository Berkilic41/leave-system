using System.ComponentModel.DataAnnotations;
using LeaveSystem.Data.Entities;

namespace LeaveSystem.Web.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class HomeViewModel
{
    public Employee Employee { get; set; } = null!;
    public IEnumerable<LeaveBalanceRow> Balance { get; set; } = [];
    public IEnumerable<LeaveRequest> RecentRequests { get; set; } = [];
    public int Year { get; set; }
}

public class EmployeeFormViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [Required] public int DepartmentId { get; set; }
    [Required, MaxLength(150)] public string Position { get; set; } = string.Empty;
    [Required] public DateTime HireDate { get; set; }
    public int? ManagerId { get; set; }

    public IEnumerable<Department> Departments { get; set; } = [];
    public IEnumerable<Employee> AvailableManagers { get; set; } = [];
}

public class LeaveRequestFormViewModel
{
    [Required] public int LeaveTypeId { get; set; }
    [Required, DataType(DataType.Date)] public DateTime StartDate { get; set; } = DateTime.UtcNow.Date.AddDays(1);
    [Required, DataType(DataType.Date)] public DateTime EndDate { get; set; } = DateTime.UtcNow.Date.AddDays(1);
    [MaxLength(1000)] public string? Reason { get; set; }
    public IEnumerable<LeaveType> LeaveTypes { get; set; } = [];
    public IEnumerable<LeaveBalanceRow> Balance { get; set; } = [];
}

public class TeamCalendarViewModel
{
    public DateTime Month { get; set; }
    public IEnumerable<Employee> Team { get; set; } = [];
    public IEnumerable<LeaveRequest> ApprovedLeaves { get; set; } = [];
}

public class HrDashboardViewModel
{
    public HrDashboardData Data { get; set; } = new();
}
