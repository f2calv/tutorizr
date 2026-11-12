using Microsoft.Web.WebView2.Core;

namespace CasCap.Pages;

public sealed partial class ConversationPage
{
    // The call page is local app content; it may use the microphone and nothing else that needs permission.
    partial void AllowMicrophone(WebViewInitializedEventArgs e)
    {
        if (e.PlatformArgs?.Sender is not { } webView)
            return;
        webView.PermissionRequested += (_, request) => request.State =
            request.PermissionKind == CoreWebView2PermissionKind.Microphone
                ? CoreWebView2PermissionState.Allow
                : CoreWebView2PermissionState.Deny;
    }
}
