using System.Windows;
using System.Windows.Controls;
using Bolo.App.Ui;
using Bolo.Core.Storage;

namespace Bolo.App;

/// <summary>
/// Edit a transcript; differences are harvested as correction pairs so Bolo stops
/// making the same mistake. Used by the History page and the tray's "Fix last transcript".
/// </summary>
public sealed class FixTranscriptWindow : Window
{
    private readonly TextBox _editor;

    public string EditedText => _editor.Text;

    public FixTranscriptWindow(string originalText, AppSettings settings)
    {
        var theme = Theme.FromName(settings.Theme, settings.Accent);
        ModernStyles.Apply(this, theme);
        ModernStyles.ApplyTitleBar(this, theme);
        Title = "Bolo";
        Width = 600;
        Height = 380;
        MinWidth = 440;
        MinHeight = 300;
        FontFamily = Theme.BodyFont;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = theme.WindowBg;

        var header = UiKit.PageHeader(theme, "Fix the transcript",
            "Correct anything Bolo misheard, then save. It learns the corrections and applies them automatically next time.");
        header.Margin = new Thickness(0, 0, 0, 16);

        _editor = UiKit.Input(theme, originalText);
        _editor.AcceptsReturn = true;
        _editor.TextWrapping = TextWrapping.Wrap;
        _editor.VerticalContentAlignment = VerticalAlignment.Top;
        _editor.Padding = new Thickness(12);
        _editor.FontSize = 14;
        _editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        var save = UiKit.ActionButton(theme, "Save & learn", primary: true);
        save.IsDefault = true;
        var cancel = UiKit.ActionButton(theme, "Cancel");
        cancel.IsCancel = true;
        save.Click += (_, _) => { DialogResult = true; Close(); };

        var buttons = UiKit.Row(cancel, save);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        buttons.Margin = new Thickness(0, 14, 0, 0);

        var root = new DockPanel { Margin = new Thickness(28, 26, 28, 24) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(buttons);
        root.Children.Add(_editor);
        Content = root;
    }
}
