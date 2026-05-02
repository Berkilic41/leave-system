using System.Net;
using System.Text.Json;
using Xunit;

namespace LeaveSystem.Tests.Integration;

public class LeaveSystemIntegrationTests : IClassFixture<LeaveSystemFactory>
{
    private readonly HttpClient _client;

    public LeaveSystemIntegrationTests(LeaveSystemFactory factory)
        => _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    [Fact]
    public async Task GET_Health_ReturnsOkOrDegraded()
    {
        var response = await _client.GetAsync("/health");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GET_Health_HasCorrelationId()
    {
        var response = await _client.GetAsync("/health");

        Assert.True(response.Headers.Contains("X-Correlation-ID"),
            "X-Correlation-ID header missing from response");
    }

    [Fact]
    public async Task GET_Login_ReturnsOk()
    {
        var response = await _client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GET_Leave_WithoutAuth_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/Leave/Mine");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task GET_Manager_WithoutAuth_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/Manager/Approvals");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task GET_HR_WithoutAuth_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/Hr/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task GET_AllSecurityHeaders_Present()
    {
        var response = await _client.GetAsync("/health");
        var headers  = response.Headers.ToDictionary(h => h.Key, h => h.Value.First());

        Assert.True(headers.ContainsKey("X-Frame-Options"),         "X-Frame-Options missing");
        Assert.True(headers.ContainsKey("X-Content-Type-Options"),  "X-Content-Type-Options missing");
        Assert.True(headers.ContainsKey("X-Correlation-ID"),        "X-Correlation-ID missing");
        Assert.True(headers.ContainsKey("Content-Security-Policy"), "Content-Security-Policy missing");
    }
}
