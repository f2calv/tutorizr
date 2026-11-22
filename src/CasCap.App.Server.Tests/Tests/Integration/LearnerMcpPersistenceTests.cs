using CasCap.AgentRuntime.Contracts.V1.Constants;
using CasCap.Common.Abstractions;
using CasCap.Common.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CasCap.Tests.Integration;

/// <summary>Exercises fresh MCP connections against real learner tools and an offline Redis command fixture.</summary>
[Trait("Category", "Integration")]
public sealed class LearnerMcpPersistenceTests
{
    [Fact]
    public async Task FreshCalls_PersistCorrectionAndIsolateAllLearnerState()
    {
        var fixture = new RedisLearnerTestData();
        await using var host = await StartHost(fixture);
        var endpoint = new Uri(host.Urls.Single() + "/mcp");
        var firstId = new string('a', 64);
        var secondId = new string('b', 64);
        var cancellationToken = TestContext.Current.CancellationToken;
        var expectedNames = new[]
        {
            "add_vocabulary", "get_due_vocabulary", "get_learner_profile", "get_recent_mistakes",
            "record_mistake", "record_vocabulary_review", "set_learner_level",
        };
        await using (var first = await Connect(endpoint, firstId))
        {
            var tools = await first.ListToolsAsync(cancellationToken: cancellationToken);
            Assert.Equal(expectedNames, tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
            foreach (var tool in tools)
                Assert.DoesNotContain("learnerId", tool.ProtocolTool.InputSchema.GetRawText(), StringComparison.OrdinalIgnoreCase);

            var recorded = await first.CallToolAsync("record_mistake", new Dictionary<string, object?>
            {
                ["category"] = "Other",
                ["incorrect"] = "synthetic incorrect",
                ["correction"] = "synthetic correction",
                ["explanation"] = "synthetic rule",
            }, cancellationToken: cancellationToken);
            Assert.False(recorded.IsError is true);
            Assert.Equal("synthetic correction", Content(recorded).GetProperty("correction").GetString());
            Assert.False((await first.CallToolAsync("set_learner_level",
                new Dictionary<string, object?> { ["level"] = "A2" }, cancellationToken: cancellationToken)).IsError is true);
            Assert.False((await first.CallToolAsync("add_vocabulary", new Dictionary<string, object?>
            {
                ["term"] = "synthetic term",
                ["meaning"] = "synthetic meaning",
                ["level"] = "A1",
            }, cancellationToken: cancellationToken)).IsError is true);
        }

        await using (var second = await Connect(endpoint, secondId))
        {
            var profile = Content(await second.CallToolAsync("get_learner_profile", cancellationToken: cancellationToken));
            Assert.Equal(0, profile.GetProperty("mistakeCount").GetInt64());
            Assert.Equal(0, profile.GetProperty("vocabularyCount").GetInt64());
            Assert.False(profile.TryGetProperty("estimatedLevel", out _));
            Assert.Empty(Items(await second.CallToolAsync("get_recent_mistakes", cancellationToken: cancellationToken)));
            Assert.Empty(Items(await second.CallToolAsync("get_due_vocabulary", cancellationToken: cancellationToken)));
            foreach (var correction in new[] { "synthetic second correction", "synthetic third correction" })
                Assert.False((await second.CallToolAsync("record_mistake", new Dictionary<string, object?>
                {
                    ["category"] = "Other",
                    ["incorrect"] = "synthetic second mistake",
                    ["correction"] = correction,
                }, cancellationToken: cancellationToken)).IsError is true);
            Assert.Single(Items(await second.CallToolAsync("get_recent_mistakes", cancellationToken: cancellationToken)));
        }

        await using (var fresh = await Connect(endpoint, firstId))
        {
            var correction = Assert.Single(Items(await fresh.CallToolAsync("get_recent_mistakes", cancellationToken: cancellationToken)));
            Assert.Equal("synthetic incorrect", correction.GetProperty("incorrect").GetString());
            Assert.Equal("synthetic correction", correction.GetProperty("correction").GetString());
            Assert.Equal("synthetic rule", correction.GetProperty("explanation").GetString());
            Assert.Equal("Other", correction.GetProperty("category").GetString());
            Assert.Equal("2026-01-15T09:00:00+00:00", correction.GetProperty("recordedUtc").GetString());
            var profile = Content(await fresh.CallToolAsync("get_learner_profile", cancellationToken: cancellationToken));
            Assert.Equal(1, profile.GetProperty("mistakeCount").GetInt64());
            Assert.Equal(1, profile.GetProperty("vocabularyCount").GetInt64());
            Assert.Equal("A2", profile.GetProperty("estimatedLevel").GetString());
            Assert.Single(Items(await fresh.CallToolAsync("get_due_vocabulary", cancellationToken: cancellationToken)));
            Assert.False((await fresh.CallToolAsync("record_vocabulary_review", new Dictionary<string, object?>
            {
                ["term"] = "synthetic term",
                ["correct"] = true,
            }, cancellationToken: cancellationToken)).IsError is true);
        }
        Assert.All(fixture.Keys, key => Assert.True(key.EndsWith(firstId, StringComparison.Ordinal)
            || key.EndsWith(secondId, StringComparison.Ordinal)));
        Assert.Equal(8, fixture.Keys.Count);
    }

    [Fact]
    public async Task MissingIdentity_FailsWithoutReadingOrWritingLearnerKeys()
    {
        var fixture = new RedisLearnerTestData();
        await using var host = await StartHost(fixture);
        var endpoint = new Uri(host.Urls.Single() + "/mcp");
        await using var client = await Connect(endpoint, null);
        var result = await client.CallToolAsync("get_learner_profile", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
        Assert.Empty(fixture.Keys);
        var write = await client.CallToolAsync("record_mistake", new Dictionary<string, object?>
        {
            ["category"] = "Other",
            ["incorrect"] = "synthetic incorrect",
            ["correction"] = "synthetic correction",
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(write.IsError);
        Assert.Empty(fixture.Keys);

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add(McpCallerHeaders.CallerId, new string('a', 64));
        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(fixture.Keys);
    }

    private static JsonElement Content(CallToolResult result)
    {
        Assert.False(result.IsError is true);
        Assert.NotNull(result.StructuredContent);
        return JsonSerializer.SerializeToElement(result.StructuredContent);
    }

    private static JsonElement[] Items(CallToolResult result) =>
        Content(result).EnumerateArray().ToArray();

    private static Task<McpClient> Connect(Uri endpoint, string? id)
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("synthetic:synthetic")),
        };
        if (id is not null)
            headers[McpCallerHeaders.CallerId] = id;
        return McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = headers,
        }), cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> StartHost(RedisLearnerTestData fixture)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(Options.Create(new ApiAuthConfig { Username = "synthetic", Password = "synthetic" }));
        builder.Services.AddAuthentication(BasicAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationHandler.SchemeName, null);
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(Options.Create(new TutorConfig { MaxMistakes = 1 }));
        builder.Services.AddSingleton<TimeProvider>(new ManualTimeProvider(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero)));
        builder.Services.AddSingleton(fixture.CreateConnection());
        builder.Services.AddScoped<LearnerMcpContext>();
        builder.Services.AddScoped<ILearnerStore, RedisLearnerStore>();
        builder.Services.AddMcpServer().WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<TutorMcpQueryService>();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMcp("/mcp").RequireAuthorization();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
