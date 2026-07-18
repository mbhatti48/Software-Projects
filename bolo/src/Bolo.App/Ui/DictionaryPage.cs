using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bolo.Core.Learning;

namespace Bolo.App.Ui;

public sealed class DictionaryPage : UserControl
{
    private readonly Theme _theme;
    private readonly DictationController _controller;
    private readonly StackPanel _clusterList = new();

    public DictionaryPage(Theme t, DictationController controller)
    {
        _theme = t;
        _controller = controller;

        var panel = new DockPanel();
        var title = UiKit.Title(t, "Dictionary");
        DockPanel.SetDock(title, Dock.Top);

        var hint = UiKit.Label(t,
            "Words, names, and jargon Bolo should recognize — grouped into clusters you can switch on and off. Changes apply to the next dictation.",
            muted: true);
        hint.Margin = new Thickness(0, 0, 0, 12);
        DockPanel.SetDock(hint, Dock.Top);

        var addCluster = UiKit.ActionButton(t, "+ New cluster");
        addCluster.Click += (_, _) =>
        {
            _controller.Vocabulary.Clusters.Add(new VocabCluster { Name = "New cluster" });
            SaveQuiet();
            Refresh();
        };
        DockPanel.SetDock(addCluster, Dock.Bottom);
        addCluster.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        addCluster.Margin = new Thickness(0, 10, 0, 0);

        panel.Children.Add(title);
        panel.Children.Add(hint);
        panel.Children.Add(addCluster);
        panel.Children.Add(new ScrollViewer
        {
            Content = _clusterList,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        Content = panel;
        Refresh();
    }

    private void Refresh()
    {
        _clusterList.Children.Clear();
        foreach (var cluster in _controller.Vocabulary.Clusters)
            _clusterList.Children.Add(ClusterCard(cluster));
    }

    private UIElement ClusterCard(VocabCluster cluster)
    {
        // Header: name (left) · count · enabled · delete (right), one aligned line.
        var name = UiKit.Input(_theme, cluster.Name, 170);
        name.FontWeight = FontWeights.SemiBold;
        name.FontSize = 14;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.LostFocus += (_, _) => { cluster.Name = name.Text; _controller.Vocabulary.Save(); };

        var count = UiKit.Label(_theme, TermCountText(cluster), muted: true);
        count.VerticalAlignment = VerticalAlignment.Center;

        var enabled = new CheckBox
        {
            IsChecked = cluster.Enabled,
            Content = "enabled",
            Foreground = _theme.TextMuted,
            VerticalAlignment = VerticalAlignment.Center,
        };
        enabled.Checked += (_, _) => { cluster.Enabled = true; SaveQuiet(); };
        enabled.Unchecked += (_, _) => { cluster.Enabled = false; SaveQuiet(); };

        var remove = UiKit.ActionButton(_theme, "Delete");
        remove.ToolTip = "Delete this cluster and all its terms";
        remove.Click += (_, _) =>
        {
            if (MessageBox.Show($"Delete cluster \"{cluster.Name}\" and its {cluster.Terms.Count} terms?",
                    "Bolo", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _controller.Vocabulary.Clusters.Remove(cluster);
                SaveQuiet();
                Refresh();
            }
        };

        var headerRight = UiKit.Row(count, enabled, remove);
        headerRight.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(headerRight, Dock.Right);
        var header = new DockPanel();
        header.Children.Add(headerRight);
        header.Children.Add(name);

        // Thin divider separates the header from the term chips.
        var divider = new Border
        {
            Height = 1,
            Background = _theme.CardBorder,
            Margin = new Thickness(0, 12, 0, 0),
        };

        // Terms as removable chips, wrapping neatly.
        var chips = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };

        // Add-term box lives at the end of the chip flow, with a visible hint.
        var addBox = UiKit.Input(_theme, "", 120);
        addBox.Background = System.Windows.Media.Brushes.Transparent;
        addBox.VerticalAlignment = VerticalAlignment.Center;
        addBox.ToolTip = "Type a term and press Enter";

        var addHint = UiKit.Label(_theme, "+ add term", muted: true);
        addHint.Margin = new Thickness(11, 0, 0, 0);
        addHint.IsHitTestVisible = false;
        addHint.VerticalAlignment = VerticalAlignment.Center;
        addBox.TextChanged += (_, _) =>
            addHint.Visibility = addBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        var addSlot = new Grid { Margin = new Thickness(0, 0, 8, 8) };
        addSlot.Children.Add(addBox);
        addSlot.Children.Add(addHint);

        void CommitTerm()
        {
            var term = addBox.Text.Trim().TrimEnd(',');
            if (term.Length == 0)
                return;
            if (!cluster.Terms.Contains(term, StringComparer.OrdinalIgnoreCase))
            {
                cluster.Terms.Add(term);
                SaveQuiet();
                RefreshChips();
            }
            addBox.Text = "";
        }

        addBox.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.OemComma)
            {
                e.Handled = true;
                CommitTerm();
                addBox.Focus(); // keep typing the next term
            }
        };
        addBox.LostFocus += (_, _) =>
        {
            if (addBox.Text.Trim().Length > 0)
                CommitTerm();
        };

        void RefreshChips()
        {
            chips.Children.Clear();
            foreach (var term in cluster.Terms)
                chips.Children.Add(Chip(term, () =>
                {
                    cluster.Terms.Remove(term);
                    SaveQuiet();
                    RefreshChips();
                }));
            chips.Children.Add(addSlot);
            count.Text = TermCountText(cluster);
        }
        RefreshChips();

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(divider);
        stack.Children.Add(chips);
        return UiKit.Card(_theme, stack);
    }

    /// <summary>A rounded tag: term text + a small ✕ to remove it.</summary>
    private UIElement Chip(string term, Action onRemove)
    {
        var text = new TextBlock
        {
            Text = term,
            Foreground = _theme.Text,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var removeButton = new Button
        {
            Content = "✕",
            FontSize = 9,
            Foreground = _theme.TextMuted,
            Background = System.Windows.Media.Brushes.Transparent,
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            ToolTip = $"Remove \"{term}\"",
        };
        removeButton.Click += (_, _) => onRemove();

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(text);
        content.Children.Add(removeButton);

        return new Border
        {
            Background = _theme.InputBg,
            BorderBrush = _theme.CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(11, 5, 7, 5),
            Margin = new Thickness(0, 0, 8, 8),
            Child = content,
        };
    }

    private static string TermCountText(VocabCluster cluster) =>
        cluster.Terms.Count == 1 ? "1 term" : $"{cluster.Terms.Count} terms";

    private void SaveQuiet()
    {
        _controller.Vocabulary.Save();
        _ = _controller.RefreshVocabularyPromptAsync();
    }
}
