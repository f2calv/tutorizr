using Microsoft.Maui.Layouts;
using Microsoft.Maui.Platform;

namespace CasCap.Views;

/// <summary>Flows native utterance text beside a copy icon and then across the full bubble width.</summary>
public sealed partial class TranscriptMessageLayout : Layout
{
    private const double IconSize = 48;
    private const double IconSpacing = 8;
    private readonly Label _leading = new();
    private readonly Label _continuation = new();
    private readonly Label _probe = new();
    private readonly ImageButton _copy = new()
    {
        WidthRequest = IconSize,
        HeightRequest = IconSize,
        MinimumWidthRequest = IconSize,
        MinimumHeightRequest = IconSize,
        Padding = new Thickness(14),
        BackgroundColor = Colors.Transparent,
        BorderWidth = 0,
    };
    private string _text = string.Empty;

    /// <summary>Initializes the two native text regions and their accessible copy action.</summary>
    public TranscriptMessageLayout()
    {
        foreach (var label in new[] { _leading, _continuation, _probe })
            label.SetDynamicResource(StyleProperty, "DefaultLabelStyle");
        AutomationProperties.SetIsInAccessibleTree(_continuation, false);
        _copy.SetAppTheme<ImageSource>(ImageButton.SourceProperty, "copy_light.png", "copy_dark.png");
        SemanticProperties.SetHint(_copy, "Copies this message to the clipboard");
        _copy.Clicked += (_, args) => CopyClicked?.Invoke(_copy, args);
        Add(_leading);
        Add(_continuation);
        Add(_copy);
    }

    /// <summary>Occurs when the message's copy icon is activated; the sender is the image button.</summary>
    public event EventHandler? CopyClicked;

    /// <inheritdoc/>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (BindingContext is TranscriptMessage message)
        {
            _text = message.Text;
            _leading.HorizontalTextAlignment = _continuation.HorizontalTextAlignment =
                message.IsLearner ? TextAlignment.End : TextAlignment.Start;
            SemanticProperties.SetDescription(_leading, $"{message.Speaker}: {message.Text}");
            SemanticProperties.SetDescription(_copy, $"Copy {message.Speaker} message");
        }
        else
            _text = string.Empty;
        InvalidateMeasure();
    }

    /// <inheritdoc/>
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        if (args.OldHandler is not null && _probe.Handler is { } handler)
        {
            handler.DisconnectHandler();
            if (handler is IDisposable disposable)
                disposable.Dispose();
            _probe.Handler = null;
        }
        base.OnHandlerChanging(args);
    }

    /// <inheritdoc/>
    protected override ILayoutManager CreateLayoutManager() => new MessageLayoutManager(this);

    private Size MeasureText(string text, double width)
    {
        // The detached native probe cannot invalidate the parent during prefix measurements.
        if (_probe.Handler is null)
        {
            _probe.ToHandler(Handler?.MauiContext
                ?? throw new InvalidOperationException("Transcript layout has no native measurement context."));
            InitializeTextProbe();
        }
        _probe.FontFamily = _leading.FontFamily;
        _probe.FontSize = _leading.FontSize;
        _probe.FontAttributes = _leading.FontAttributes;
        _probe.FontAutoScalingEnabled = _leading.FontAutoScalingEnabled;
        _probe.LineHeight = _leading.LineHeight;
        _probe.CharacterSpacing = _leading.CharacterSpacing;
        _probe.Text = text;
        return ((IView)_probe).Measure(width, double.PositiveInfinity);
    }

    partial void InitializeTextProbe();

    private sealed class MessageLayoutManager(TranscriptMessageLayout view) : LayoutManager(view)
    {
        private double _leadingHeight;
        private double _continuationHeight;
        private double _height;

        /// <inheritdoc/>
        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var width = widthConstraint;
            if (double.IsPositiveInfinity(width))
                width = view.MeasureText(view._text, width).Width + IconSize + IconSpacing;
            var leadingWidth = Math.Max(0, width - IconSize - IconSpacing);
            var lineHeight = view.MeasureText("M", leadingWidth).Height;
            var split = leadingWidth > 0
                ? TranscriptTextFlow.FindSplit(view._text,
                    text => view.MeasureText(text, leadingWidth).Height <= Math.Max(IconSize, lineHeight))
                : 0;
            var leading = view._text[..split];
            var continuation = view._text[split..];
            if (view._leading.Text != leading)
                view._leading.Text = leading;
            if (view._continuation.Text != continuation)
                view._continuation.Text = continuation;
            var first = ((IView)view._leading).Measure(leadingWidth, double.PositiveInfinity);
            var rest = continuation.Length == 0 ? Size.Zero
                : ((IView)view._continuation).Measure(width, double.PositiveInfinity);
            var icon = ((IView)view._copy).Measure(IconSize, IconSize);
            _leadingHeight = Math.Max(icon.Height, first.Height);
            _continuationHeight = rest.Height;
            _height = _leadingHeight + _continuationHeight;
            return new Size(continuation.Length == 0 ? Math.Min(width, first.Width + IconSize + IconSpacing) : width,
                _height);
        }

        /// <inheritdoc/>
        public override Size ArrangeChildren(Rect bounds)
        {
            Measure(bounds.Width, double.PositiveInfinity);
            ((IView)view._leading).Arrange(new Rect(bounds.X, bounds.Y,
                Math.Max(0, bounds.Width - IconSize - IconSpacing), _leadingHeight));
            ((IView)view._copy).Arrange(new Rect(Math.Max(bounds.X, bounds.Right - IconSize), bounds.Y,
                IconSize, IconSize));
            ((IView)view._continuation).Arrange(new Rect(bounds.X, bounds.Y + _leadingHeight,
                bounds.Width, _continuationHeight));
            return new Size(bounds.Width, _height);
        }
    }
}
