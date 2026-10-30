namespace CasCap.Tests.Unit;

public sealed class AppHostTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public async Task RunAsync_InvalidRemoteCacheReturnsFailure()
    {
        const string connectionStringVariable = "CasCap__CachingConfig__RemoteCacheConnectionString";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringVariable);
        Environment.SetEnvironmentVariable(connectionStringVariable, " ");

        try
        {
            var result = await AppHost.RunAsync([], typeof(AppHost).Assembly);

            Assert.Equal(1, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringVariable, previousConnectionString);
        }
    }
}
