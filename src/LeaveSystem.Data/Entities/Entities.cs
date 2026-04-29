namespace LeaveSystem.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Headcount { get; set; }
}

public class Employee
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string Position { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public int Depth { get; set; }
}

public class LeaveType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DefaultAnnualQuota { get; set; }
    public bool IsPaid { get; set; }
}

public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentName { get; set; }
    public int LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? DecidedAt { get; set; }
    public int? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Days => (EndDate - StartDate).Days + 1;
}

public class LeaveBalanceRow
{
    public int LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public int AnnualDays { get; set; }
    public int UsedDays { get; set; }
    public int PendingDays { get; set; }
    public int Remaining => AnnualDays - UsedDays - PendingDays;
}

public class AuditEntry
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public int ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; }
}

public class HrDashboardData
{
    public int TotalEmployees { get; set; }
    public int TotalDepartments { get; set; }
    public int PendingRequests { get; set; }
    public List<Department> Headcount { get; set; } = [];
    public List<Employee> OnLeaveToday { get; set; } = [];
    public List<LeaveTypeUsage> Usage { get; set; } = [];
}

public class LeaveTypeUsage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int TotalDays { get; set; }
}
