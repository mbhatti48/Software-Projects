using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

/// <summary>
/// The full Bolo window: sidebar navigation + page area. Created on demand from the
/// tray; closing hides it (the app lives in the tray).
/// </summary>
public sealed class MainWindow : Window
{
    private readonly Theme _theme;
    private readonly ContentControl _pageHost = new();
    private readonly Dictionary<string, Func<UIElement>> _pages;
    private readonly List<Button> _navButtons = [];

    public MainWindow(AppSettings settings, DictationController controller, App app)
    {
        _theme = Theme.FromName(settings.Theme);
        ModernStyles.Apply(this, _theme);
        ModernStyles.ApplyTitleBar(this, dark: settings.Theme != "Light");
        Title = "Bolo";
        Width = 900;
        Height = 620;
        MinWidth = 720;
        MinHeight = 480;
        Background = _theme.WindowBg;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _pages = new Dictionary<string, Func<UIElement>>
        {
            ["Home"] = () => new HomePage(_theme, controller, settings.UserName),
            ["History"] = () => new HistoryPage(_theme, controller),
            ["Dictionary"] = () => new DictionaryPage(_theme, controller),
            ["Snippets"] = () => new SnippetsPage(_theme, controller),
            ["Insights"] = () => new InsightsPage(_theme, controller),
            ["Style"] = () => new StylePage(_theme, settings),
            ["Settings"] = () => new SettingsPage(_theme, settings, controller, app),
        };

        var sidebar = new StackPanel { Margin = new Thickness(10, 18, 10, 16) };
        var brand = new StackPanel { Margin = new Thickness(12, 0, 0, 20) };
        brand.Children.Add(new TextBlock
        {
            Text = "Bolo",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = _theme.Accent,
        });
        brand.Children.Add(new TextBlock
        {
            Text = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(2)} · by EmAyeBee",
            FontSize = 11,
            Foreground = _theme.TextMuted,
        });
        sidebar.Children.Add(brand);
        foreach (var name in _pages.Keys)
        {
            var button = new Button
            {
                Content = name,
                FontSize = 14,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 2),
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = _theme.TextMuted,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = name,
            };
            button.Resources.Add(typeof(System.Windows.Controls.Border),
                new Style(typeof(System.Windows.Controls.Border))
                {
                    Setters = { new Setter(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(8)) },
                });
            button.Click += (_, _) => Navigate((string)button.Tag);
            _navButtons.Add(button);
            sidebar.Children.Add(button);
        }

        var sidebarBorder = new System.Windows.Controls.Border
        {
            Background = _theme.SidebarBg,
            Child = sidebar,
        };

        _pageHost.Margin = new Thickness(30, 26, 30, 26);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(sidebarBorder, 0);
        Grid.SetColumn(_pageHost, 1);
        grid.Children.Add(sidebarBorder);
        grid.Children.Add(_pageHost);
        Content = grid;

        Navigate("Home");

        // Tray app pattern: closing the window hides it, Exit lives in the tray menu.
        Closing += (_, e) =>
        {
            if (_allowClose)
                return;
            e.Cancel = true;
            Hide();
        };
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
        foreach (var button in _navButtons)
        {
            var active = (string)button.Tag == page;
            button.Foreground = active ? _theme.Accent : _theme.TextMuted;
            button.Background = active ? _theme.CardBg : System.Windows.Media.Brushes.Transparent;
            button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }
}
