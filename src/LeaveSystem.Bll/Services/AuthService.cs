using LeaveSystem.Bll.DTOs;
using LeaveSystem.Bll.Helpers;
using LeaveSystem.Bll.Services.Interfaces;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Bll.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IEmployeeRepository _employees;

    public AuthService(IUserRepository users, IEmployeeRepository employees)
    {
        _users = users;
        _employees = employees;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null) return AuthResult.Fail("Invalid email or password.");
        if (!user.IsActive) return AuthResult.Fail("This account is disabled.");
        if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return AuthResult.Fail("Invalid email or password.");
        var employee = await _employees.GetByUserIdAsync(user.Id);
        if (employee is null) return AuthResult.Fail("No employee profile linked to this account.");
        return AuthResult.Ok(user, employee);
    }
}
