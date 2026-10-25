using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

/// <summary>
/// The full Bolo window: a chrome-coloured sidebar (wordmark, glyph navigation, a live
/// engine status chip with the hotkey) and the page area. Created on demand from the
/// tray; closing hides it (the app lives in the tray).
/// </summary>
public sealed class MainWindow : Window
{
    private readonly Theme _theme;
    private readonly AppSettings _settings;
    private readonly DictationController _controller;
    private readonly ContentControl _pageHost = new();
    private readonly Dictionary<string, Func<UIElement>> _pages;
    private readonly Dictionary<string, (Button Button, TextBlock Glyph, TextBlock Label)> _nav = [];
    private readonly Ellipse _statusDot = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _statusText;
    private readonly Action<DictationState> _onStateChanged;

    private static readonly (string Name, string Glyph)[] NavItems =
    [
        ("Home", UiKit.GlyphHome), ("History", UiKit.GlyphHistory), ("Dictionary", UiKit.GlyphDictionary),
        ("Snippets", UiKit.GlyphSnippets), ("Insights", UiKit.GlyphInsights), ("Style", UiKit.GlyphStyle),
        ("Settings", UiKit.GlyphSettings),
    ];

    public MainWindow(AppSettings settings, DictationController controller, App app)
    {
        _settings = settings;
        _controller = controller;
        _theme = Theme.FromName(settings.Theme, settings.Accent);
        ModernStyles.Apply(this, _theme);
        ModernStyles.ApplyTitleBar(this, _theme);
        Title = "Bolo";
        Width = 1040;
        Height = 680;
        MinWidth = 820;
        MinHeight = 560;
        FontFamily = Theme.BodyFont;
        Background = _theme.WindowBg;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        _pages = new Dictionary<string, Func<UIElement>>
        {
            ["Home"] = () => new HomePage(_theme, controller, settings, Navigate),
            ["History"] = () => new HistoryPage(_theme, controller, settings),
            ["Dictionary"] = () => new DictionaryPage(_theme, controller),
            ["Snippets"] = () => new SnippetsPage(_theme, controller),
            ["Insights"] = () => new InsightsPage(_theme, controller, settings),
            ["Style"] = () => new StylePage(_theme, settings),
            ["Settings"] = () => new SettingsPage(_theme, settings, controller, app),
        };

        // ---- sidebar
        var sidebar = new DockPanel { Margin = new Thickness(12, 18, 12, 14), LastChildFill = true };

        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 0, 20) };
        brand.Children.Add(LogoImage(40));
        var words = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(new TextBlock
        {
            Text = "B O L O",
            FontFamily = Theme.TitleFont,
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = _theme.Text,
            LineHeight = 20,
        });
        words.Children.Add(new TextBlock
        {
            Text = "BY EMAYEBEE",
            FontSize = 9.5,
            FontWeight = FontWeights.Bold,
            Foreground = _theme.TextFaint,
            Margin = new Thickness(1, 4, 0, 0),
        });
        brand.Children.Add(words);
        DockPanel.SetDock(brand, Dock.Top);
        sidebar.Children.Add(brand);

        _statusText = new TextBlock { FontSize = 12, Foreground = _theme.TextMuted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 0, 0) };
        var statusRow = new DockPanel();
        DockPanel.SetDock(_statusDot, Dock.Left);
        statusRow.Children.Add(_statusDot);
        statusRow.Children.Add(_statusText);
        var hotkeyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var hold = UiKit.Label(_theme, settings.HotkeyMode == HotkeyMode.HoldToTalk ? "Hold" : "Tap", muted: true);
        hold.FontSize = 12;
        hold.Margin = new Thickness(0, 0, 6, 0);
        hotkeyRow.Children.Add(hold);
        hotkeyRow.Children.Add(UiKit.HotkeyKbds(_theme, settings.Hotkey));
        var toDictate = UiKit.Label(_theme, "to dictate", muted: true);
        toDictate.FontSize = 12;
        toDictate.Margin = new Thickness(2, 0, 0, 0);
        hotkeyRow.Children.Add(toDictate);
        var statusStack = new StackPanel();
        statusStack.Children.Add(statusRow);
        statusStack.Children.Add(hotkeyRow);
        var statusChip = UiKit.Card(_theme, statusStack, padding: 12);
        statusChip.Margin = new Thickness(0);
        DockPanel.SetDock(statusChip, Dock.Bottom);
        sidebar.Children.Add(statusChip);

        var nav = new StackPanel();
        foreach (var (name, glyph) in NavItems)
        {
            var glyphBlock = UiKit.Glyph(_theme, glyph, 16);
            var label = new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(glyphBlock);
            content.Children.Add(label);
            var button = new Button
            {
                Content = content,
                Height = 38,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 0, 2),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = _theme.TextMuted,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = name,
            };
            button.Click += (_, _) => Navigate((string)button.Tag);
            _nav[name] = (button, glyphBlock, label);
            nav.Children.Add(button);
        }
        sidebar.Children.Add(nav);

        var sidebarBorder = new Border
        {
            Background = _theme.SidebarBg,
            BorderBrush = _theme.CardBorder,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = sidebar,
        };

        _pageHost.Margin = new Thickness(40, 34, 40, 0);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(sidebarBorder, 0);
        Grid.SetColumn(_pageHost, 1);
        grid.Children.Add(sidebarBorder);
        grid.Children.Add(_pageHost);
        Content = grid;

        _onStateChanged = state => Dispatcher.Invoke(() => ShowState(state));
        controller.StateChanged += _onStateChanged;
        ShowState(controller.State);

        Navigate("Home");

        // Tray app pattern: closing the window hides it, Exit lives in the tray menu.
        Closing += (_, e) =>
        {
            if (_allowClose)
                return;
            e.Cancel = true;
            Hide();
        };
        Closed += (_, _) => controller.StateChanged -= _onStateChanged;
    }

    /// <summary>The app mark from the embedded 1024 px logo, clipped to the tile's corners.</summary>
    public static Border LogoImage(double size)
    {
        var image = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Assets/bolo-logo.png")),
            Width = size,
            Height = size,
            Stretch = Stretch.UniformToFill,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.2),
            Child = image,
            VerticalAlignment = VerticalAlignment.Center,
            Clip = new RectangleGeometry(new Rect(0, 0, size, size), size * 0.2, size * 0.2),
        };
    }

    private void ShowState(DictationState state)
    {
        var model = System.IO.Path.GetFileNameWithoutExtension(_settings.ModelPath).Replace("ggml-", "");
        (Brush dot, string text) = state switch
        {
            DictationState.Loading => (_theme.TextFaint, "Loading the model…"),
            DictationState.Recording => (_theme.Recording, "Listening"),
            DictationState.Transcribing => (_theme.Busy, "Finalizing…"),
            _ when _controller.LoadedRuntime is "none" or "unknown" =>
                (_theme.Recording, "No model loaded. Check Settings."),
            _ => (_theme.Ok, $"Ready · {_controller.LoadedRuntime} · {model}"),
        };
        _statusDot.Fill = dot;
        _statusDot.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = _theme.ColorOf(dot), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.7,
        };
        _statusText.Text = text;
    }

    private bool _allowClose;

    /// <summary>Really closes the window (used when rebuilding for a theme change).</summary>
    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    public void Navigate(string page)
    {
        var content = _pages[page]();
        _pageHost.Content = content;
        if (content is FrameworkElement fe)
            UiKit.AnimateIn(fe);
        foreach (var (name, (button, glyph, label)) in _nav)
        {
            var active = name == page;
            button.Background = active ? _theme.AccentSoft : Brushes.Transparent;
            button.BorderBrush = active ? _theme.AccentSoft : Brushes.Transparent;
            glyph.Foreground = active ? _theme.AccentInk : _theme.TextMuted;
            label.Foreground = active ? _theme.AccentInk : _theme.TextMuted;
            label.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
        }
    }
}
