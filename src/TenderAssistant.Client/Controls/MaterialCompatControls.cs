using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TenderAssistant.Client.Controls;

public enum ButtonAppearance
{
    Primary,
    Secondary,
    Danger,
    Transparent
}

public enum InfoBarSeverity
{
    Informational,
    Success,
    Warning,
    Error
}

public enum InfoBadgeSeverity
{
    Informational,
    Success,
    Attention,
    Caution,
    Critical
}

public sealed class Card : ContentControl
{
    static Card()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card)));
    }
}

public sealed class Button : System.Windows.Controls.Button
{
    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(ButtonAppearance),
        typeof(Button),
        new FrameworkPropertyMetadata(ButtonAppearance.Secondary));

    public ButtonAppearance Appearance
    {
        get => (ButtonAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }
}

public sealed class InfoBar : Control
{
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(InfoBar),
        new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty IsClosableProperty = DependencyProperty.Register(
        nameof(IsClosable),
        typeof(bool),
        typeof(InfoBar),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(InfoBar),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(InfoBar),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(InfoBarSeverity),
        typeof(InfoBar),
        new FrameworkPropertyMetadata(InfoBarSeverity.Informational, OnSeverityChanged));

    public static readonly DependencyProperty SeverityBackgroundProperty = DependencyProperty.Register(
        nameof(SeverityBackground),
        typeof(Brush),
        typeof(InfoBar),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(232, 244, 255))));

    public static readonly DependencyProperty SeverityForegroundProperty = DependencyProperty.Register(
        nameof(SeverityForeground),
        typeof(Brush),
        typeof(InfoBar),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(29, 78, 216))));

    static InfoBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(InfoBar), new FrameworkPropertyMetadata(typeof(InfoBar)));
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public InfoBarSeverity Severity
    {
        get => (InfoBarSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public Brush SeverityBackground
    {
        get => (Brush)GetValue(SeverityBackgroundProperty);
        private set => SetValue(SeverityBackgroundProperty, value);
    }

    public Brush SeverityForeground
    {
        get => (Brush)GetValue(SeverityForegroundProperty);
        private set => SetValue(SeverityForegroundProperty, value);
    }

    private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InfoBar infoBar)
        {
            infoBar.ApplySeverity();
        }
    }

    private void ApplySeverity()
    {
        var (background, foreground) = Severity switch
        {
            InfoBarSeverity.Success => ("#EAF7EA", "#2E7D32"),
            InfoBarSeverity.Warning => ("#FFF7E6", "#AD6800"),
            InfoBarSeverity.Error => ("#FFEBEE", "#C62828"),
            _ => ("#EAF3FF", "#1D4ED8")
        };

        SeverityBackground = ToBrush(background);
        SeverityForeground = ToBrush(foreground);
    }

    private static SolidColorBrush ToBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed class InfoBadge : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(InfoBadge),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(InfoBadgeSeverity),
        typeof(InfoBadge),
        new FrameworkPropertyMetadata(InfoBadgeSeverity.Informational, OnSeverityChanged));

    public static readonly DependencyProperty BadgeBackgroundProperty = DependencyProperty.Register(
        nameof(BadgeBackground),
        typeof(Brush),
        typeof(InfoBadge),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(232, 244, 255))));

    public static readonly DependencyProperty BadgeForegroundProperty = DependencyProperty.Register(
        nameof(BadgeForeground),
        typeof(Brush),
        typeof(InfoBadge),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(29, 78, 216))));

    static InfoBadge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(InfoBadge), new FrameworkPropertyMetadata(typeof(InfoBadge)));
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public InfoBadgeSeverity Severity
    {
        get => (InfoBadgeSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public Brush BadgeBackground
    {
        get => (Brush)GetValue(BadgeBackgroundProperty);
        private set => SetValue(BadgeBackgroundProperty, value);
    }

    public Brush BadgeForeground
    {
        get => (Brush)GetValue(BadgeForegroundProperty);
        private set => SetValue(BadgeForegroundProperty, value);
    }

    private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InfoBadge badge)
        {
            badge.ApplySeverity();
        }
    }

    private void ApplySeverity()
    {
        var (background, foreground) = Severity switch
        {
            InfoBadgeSeverity.Success => ("#EAF7EA", "#2E7D32"),
            InfoBadgeSeverity.Attention => ("#FFF7E6", "#AD6800"),
            InfoBadgeSeverity.Caution => ("#FFF7E6", "#AD6800"),
            InfoBadgeSeverity.Critical => ("#FFEBEE", "#C62828"),
            _ => ("#EAF3FF", "#1D4ED8")
        };

        BadgeBackground = ToBrush(background);
        BadgeForeground = ToBrush(foreground);
    }

    private static SolidColorBrush ToBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed class DynamicScrollViewer : ScrollViewer
{
}
