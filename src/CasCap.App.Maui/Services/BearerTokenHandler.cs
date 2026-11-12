using System.Net.Http.Headers;

namespace CasCap.Services;

/// <summary>Adds the learner's delegated Agent Runtime token to each request.</summary>
/// <param name="tokenProvider">The learner's token provider.</param>
public sealed class BearerTokenHandler(MsalAccessTokenProvider tokenProvider) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", await tokenProvider.GetAccessTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }
}
