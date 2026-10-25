using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Learning;

namespace Bolo.App.Ui;

public sealed class SnippetsPage : UserControl
{
    private readonly Theme _theme;
    private readonly DictationController _controller;
    private readonly StackPanel _list = new();

    public SnippetsPage(Theme t, DictationController controller)
    {
        _theme = t;
        _controller = controller;

        var add = UiKit.ActionButton(t, "New snippet", primary: true);
        add.Click += (_, _) =>
        {
            _controller.Snippets.Snippets.Add(new Snippet { Trigger = "trigger phrase", Expansion = "expansion text" });
            _controller.Snippets.Save();
            Refresh();
        };

        var panel = new DockPanel();
        var header = UiKit.PageHeader(t, "Snippets",
            "Say the trigger phrase while dictating and Bolo types the expansion instead. \"My email\" can become your full address.", add);
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 8, 24) });
        Content = panel;
        Refresh();
    }

    private void Refresh()
    {
        _list.Children.Clear();
        if (_controller.Snippets.Snippets.Count == 0)
        {
            _list.Children.Add(UiKit.Empty(_theme,
                "No snippets yet. Add one for anything you type more than you should: your email, your address, a sign-off."));
            return;
        }
        var head = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(228) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var trigger = UiKit.Section(_theme, "When you say");
        trigger.Margin = new Thickness(18, 0, 0, 0);
        var expansion = UiKit.Section(_theme, "Bolo types");
        expansion.Margin = new Thickness(0);
        Grid.SetColumn(expansion, 1);
        head.Children.Add(trigger);
        head.Children.Add(expansion);
        _list.Children.Add(head);
        foreach (var snippet in _controller.Snippets.Snippets)
            _list.Children.Add(SnippetCard(snippet));
    }

    private UIElement SnippetCard(Snippet snippet)
    {
        var trigger = UiKit.Input(_theme, snippet.Trigger, 200);
        trigger.LostFocus += (_, _) => { snippet.Trigger = trigger.Text; _controller.Snippets.Save(); };

        var arrow = UiKit.Glyph(_theme, UiKit.GlyphChevron, 12, _theme.TextFaint);

        var expansion = UiKit.Input(_theme, snippet.Expansion);
        expansion.MinWidth = 280;
        expansion.LostFocus += (_, _) => { snippet.Expansion = expansion.Text; _controller.Snippets.Save(); };

        var remove = UiKit.ActionButton(_theme, "Delete");
        remove.Click += (_, _) =>
        {
            _controller.Snippets.Snippets.Remove(snippet);
            _controller.Snippets.Save();
            Refresh();
        };

        var row = new DockPanel();
        DockPanel.SetDock(trigger, Dock.Left);
        DockPanel.SetDock(arrow, Dock.Left);
        DockPanel.SetDock(remove, Dock.Right);
        trigger.Margin = new Thickness(0, 0, 10, 0);
        arrow.Margin = new Thickness(0, 0, 10, 0);
        remove.Margin = new Thickness(10, 0, 0, 0);
        row.Children.Add(trigger);
        row.Children.Add(arrow);
        row.Children.Add(remove);
        row.Children.Add(expansion);
        return UiKit.Card(_theme, row, padding: 12);
    }
}
