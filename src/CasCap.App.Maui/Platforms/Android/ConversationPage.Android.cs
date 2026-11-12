using Android.Webkit;
using Microsoft.Maui.ApplicationModel;

namespace CasCap.Pages;

public sealed partial class ConversationPage
{
    partial void AllowMicrophone(WebViewInitializedEventArgs e)
    {
        if (e.PlatformArgs?.Sender is not { } webView)
            return;
        // Tutor audio starts when the call connects, after a native button tap the web view never sees.
        webView.Settings.MediaPlaybackRequiresUserGesture = false;
        webView.SetWebChromeClient(new MicrophoneChromeClient());
    }

    // The call page is local app content; it may use the microphone and nothing else that needs permission.
    private sealed class MicrophoneChromeClient : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest? request)
        {
            if (request is null)
                return;
            if (request.GetResources()?.Contains(PermissionRequest.ResourceAudioCapture) is not true)
            {
                request.Deny();
                return;
            }
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (await Permissions.RequestAsync<Permissions.Microphone>() == PermissionStatus.Granted)
                    request.Grant([PermissionRequest.ResourceAudioCapture]);
                else
                    request.Deny();
            });
        }
    }
}
