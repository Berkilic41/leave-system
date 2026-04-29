using LeaveSystem.Data.Entities;

namespace LeaveSystem.Bll.DTOs;

public record AuthResult(bool Success, string? ErrorMessage = null, User? User = null, Employee? Employee = null)
{
    public static AuthResult Ok(User user, Employee employee) => new(true, null, user, employee);
    public static AuthResult Fail(string message) => new(false, message);
}
