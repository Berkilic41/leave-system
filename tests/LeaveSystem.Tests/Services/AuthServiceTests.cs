using Moq;
using Xunit;
using LeaveSystem.Bll.DTOs;
using LeaveSystem.Bll.Helpers;
using LeaveSystem.Bll.Services;
using LeaveSystem.Data.Entities;
using LeaveSystem.Data.Repositories.Interfaces;

namespace LeaveSystem.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository>     _mockUsers;
    private readonly Mock<IEmployeeRepository> _mockEmployees;
    private readonly AuthService               _service;

    public AuthServiceTests()
    {
        _mockUsers     = new Mock<IUserRepository>();
        _mockEmployees = new Mock<IEmployeeRepository>();
        _service       = new AuthService(_mockUsers.Object, _mockEmployees.Object);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccessWithUserAndEmployee()
    {
        var password = "TestPassword123";
        var (hash, salt) = PasswordHasher.Hash(password);
        var user     = new User { Id = 1, Email = "john@test.com", PasswordHash = hash, PasswordSalt = salt, IsActive = true, Role = "Employee" };
        var employee = new Employee { Id = 1, UserId = 1, FullName = "John Doe" };

        _mockUsers.Setup(r => r.GetByEmailAsync("john@test.com")).ReturnsAsync(user);
        _mockEmployees.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(employee);

        var result = await _service.LoginAsync("john@test.com", password);

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.NotNull(result.Employee);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsFailure()
    {
        var (hash, salt) = PasswordHasher.Hash("CorrectPass");
        _mockUsers.Setup(r => r.GetByEmailAsync("u@u.com"))
            .ReturnsAsync(new User { Id = 1, PasswordHash = hash, PasswordSalt = salt, IsActive = true });

        var result = await _service.LoginAsync("u@u.com", "WrongPass");

        Assert.False(result.Success);
        Assert.Equal("Invalid email or password.", result.ErrorMessage);
        _mockEmployees.Verify(r => r.GetByUserIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_EmailNotFound_ReturnsFailure()
    {
        _mockUsers.Setup(r => r.GetByEmailAsync("ghost@test.com")).ReturnsAsync((User?)null);

        var result = await _service.LoginAsync("ghost@test.com", "any");

        Assert.False(result.Success);
        Assert.Equal("Invalid email or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsFailure()
    {
        var (hash, salt) = PasswordHasher.Hash("Pass");
        _mockUsers.Setup(r => r.GetByEmailAsync("u@u.com"))
            .ReturnsAsync(new User { Id = 1, PasswordHash = hash, PasswordSalt = salt, IsActive = false });

        var result = await _service.LoginAsync("u@u.com", "Pass");

        Assert.False(result.Success);
        Assert.Equal("This account is disabled.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_NoEmployeeProfile_ReturnsFailure()
    {
        var (hash, salt) = PasswordHasher.Hash("Pass");
        _mockUsers.Setup(r => r.GetByEmailAsync("u@u.com"))
            .ReturnsAsync(new User { Id = 1, PasswordHash = hash, PasswordSalt = salt, IsActive = true });
        _mockEmployees.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync((Employee?)null);

        var result = await _service.LoginAsync("u@u.com", "Pass");

        Assert.False(result.Success);
        Assert.Equal("No employee profile linked to this account.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("HR")]
    [InlineData("Manager")]
    [InlineData("Employee")]
    public async Task LoginAsync_AnyActiveRole_Succeeds(string role)
    {
        var password = "Pass123";
        var (hash, salt) = PasswordHasher.Hash(password);
        _mockUsers.Setup(r => r.GetByEmailAsync("u@u.com"))
            .ReturnsAsync(new User { Id = 1, PasswordHash = hash, PasswordSalt = salt, IsActive = true, Role = role });
        _mockEmployees.Setup(r => r.GetByUserIdAsync(1))
            .ReturnsAsync(new Employee { Id = 1, Role = role });

        var result = await _service.LoginAsync("u@u.com", password);

        Assert.True(result.Success);
        Assert.Equal(role, result.User!.Role);
    }
}
