using Microsoft.Extensions.Options;
using System.Net;

namespace CasCap.Tests.Unit;

/// <summary>Checks capability routing through the same base-address semantics as voice calls.</summary>
[Trait("Category", "Routing")]
public sealed class VoiceCapabilityClientTests
{
    [Theory]
    [InlineData("https://example.com/", "/api/v1/agents/test-agent/realtime/capabilities")]
    [InlineData("https://example.com/runtime/", "/runtime/api/v1/agents/test-agent/realtime/capabilities")]
    public async Task GetAsync_PreservesRuntimePathPrefix(string baseAddress, string expectedPath)
    {
        // Regression for an available capability route returning 404 through a prefixed runtime URL.
        using var handler = new CapabilityHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
        var client = new VoiceCapabilityClient(httpClient,
            Options.Create(new TutorClientConfig { AgentName = "test-agent" }));
        var capabilities = await client.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expectedPath, handler.RequestPath);
        Assert.NotNull(capabilities);
        Assert.True(capabilities.VoiceEnabled);
        Assert.True(capabilities.RetentionAllowed);
    }

    private sealed class CapabilityHandler : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"voiceEnabled":true,"knowledgeToolsEnabled":false,"retentionAllowed":true}"""),
            });
        }
    }
}
