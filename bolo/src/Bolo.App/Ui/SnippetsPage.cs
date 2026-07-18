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

        var panel = new DockPanel();
        var title = UiKit.Title(t, "Snippets");
        DockPanel.SetDock(title, Dock.Top);

        var hint = UiKit.Label(t,
            "Say the trigger phrase while dictating and Bolo types the expansion instead — e.g. \"my email\" → your real address.",
            muted: true);
        hint.Margin = new Thickness(0, 0, 0, 12);
        DockPanel.SetDock(hint, Dock.Top);

        var add = UiKit.ActionButton(t, "+ New snippet");
        add.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        add.Margin = new Thickness(0, 10, 0, 0);
        add.Click += (_, _) =>
        {
            _controller.Snippets.Snippets.Add(new Snippet { Trigger = "trigger phrase", Expansion = "expansion text" });
            _controller.Snippets.Save();
            Refresh();
        };
        DockPanel.SetDock(add, Dock.Bottom);

        panel.Children.Add(title);
        panel.Children.Add(hint);
        panel.Children.Add(add);
        panel.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = panel;
        Refresh();
    }

    private void Refresh()
    {
        _list.Children.Clear();
        if (_controller.Snippets.Snippets.Count == 0)
        {
            _list.Children.Add(UiKit.Label(_theme, "No snippets yet.", muted: true));
            return;
        }
        foreach (var snippet in _controller.Snippets.Snippets)
            _list.Children.Add(SnippetCard(snippet));
    }

    private UIElement SnippetCard(Snippet snippet)
    {
        var trigger = UiKit.Input(_theme, snippet.Trigger, 200);
        trigger.LostFocus += (_, _) => { snippet.Trigger = trigger.Text; _controller.Snippets.Save(); };

        var arrow = UiKit.Label(_theme, "→", muted: true);

        var expansion = UiKit.Input(_theme, snippet.Expansion, 320);
        expansion.LostFocus += (_, _) => { snippet.Expansion = expansion.Text; _controller.Snippets.Save(); };

        var remove = UiKit.ActionButton(_theme, "Delete");
        remove.Click += (_, _) =>
        {
            _controller.Snippets.Snippets.Remove(snippet);
            _controller.Snippets.Save();
            Refresh();
        };

        return UiKit.Card(_theme, UiKit.Row(trigger, arrow, expansion, remove));
    }
}
