using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Bolo.App;

/// <summary>
/// Edit the last transcript; differences are harvested as correction pairs so Bolo
/// stops making the same mistake. (The full History page in Phase 3 supersedes this
/// as the main correction entry point.)
/// </summary>
public sealed class FixTranscriptWindow : Window
{
    private readonly TextBox _editor;

    public string EditedText => _editor.Text;

    public FixTranscriptWindow(string originalText)
    {
        var settings = Bolo.Core.Storage.AppSettings.Load();
        var theme = Ui.Theme.FromName(settings.Theme);
        Ui.ModernStyles.Apply(this, theme);
        Ui.ModernStyles.ApplyTitleBar(this, dark: settings.Theme != "Light");
        Title = "Bolo — Fix last transcript";
        Width = 560;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24));

        _editor = new TextBox
        {
            Text = originalText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Margin = new Thickness(12),
            Padding = new Thickness(8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var hint = new TextBlock
        {
            Text = "Fix anything Bolo misheard, then save — it learns the corrections and applies them automatically next time.",
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 0, 12, 0),
        };

        var save = new Button { Content = "Save & learn", IsDefault = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(16, 6, 16, 6) };
        save.Click += (_, _) => { DialogResult = true; Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(12),
        };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);

        var root = new DockPanel();
        DockPanel.SetDock(hint, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(hint);
        root.Children.Add(buttons);
        root.Children.Add(_editor);
        Content = root;
    }
}
