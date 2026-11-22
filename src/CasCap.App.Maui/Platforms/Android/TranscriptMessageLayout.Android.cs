using Android.Views;

namespace CasCap.Views;

public sealed partial class TranscriptMessageLayout
{
    partial void InitializeTextProbe()
    {
        // A detached TextView needs layout parameters before changing text after its first measurement.
        if (_probe.Handler?.PlatformView is Android.Views.View probe && probe.LayoutParameters is null)
            probe.LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
    }
}
