using Chess_Backend.Services;
using Chess_Backend.Tests.Helpers;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chess_Backend.Tests.Services;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task HealthyDatabase_ReturnsHealthy()
    {
        using var context = TestHelper.CreateInMemoryContext();
        var logger = TestHelper.CreateMockLogger<DatabaseHealthCheck>();
        var healthCheck = new DatabaseHealthCheck(context, logger);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("successful", result.Description);
    }

    [Fact]
    public async Task CancelledToken_ReturnsDegraded()
    {
        using var context = TestHelper.CreateInMemoryContext();
        var logger = TestHelper.CreateMockLogger<DatabaseHealthCheck>();
        var healthCheck = new DatabaseHealthCheck(context, logger);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}
