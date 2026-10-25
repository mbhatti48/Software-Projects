using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Bolo.Core.Learning;

namespace Bolo.App.Ui;

/// <summary>
/// Small factory helpers so the code-built pages stay readable and share one look:
/// the v1.2 type ramp (Cinzel 26 titles, 11 px uppercase accent section heads, 14 px
/// Nunito body, 12 px captions), hairline cards on a 4 px grid, short decelerating
/// motion (120 to 250 ms), and empty states written as sentences.
/// </summary>
public static class UiKit
{
    public const string GlyphHome = "";
    public const string GlyphHistory = "";
    public const string GlyphDictionary = "";
    public const string GlyphSnippets = "";
    public const string GlyphInsights = "";
    public const string GlyphStyle = "";
    public const string GlyphSettings = "";
    public const string GlyphMic = "";
    public const string GlyphCopy = "";
    public const string GlyphEdit = "";
    public const string GlyphChevron = "";

    private static readonly CubicEase EaseOut = new() { EasingMode = EasingMode.EaseOut };

    // ---- type

    public static TextBlock Title(Theme t, string text) => new()
    {
        Text = text,
        FontFamily = Theme.TitleFont,
        FontSize = 26,
        FontWeight = FontWeights.SemiBold,
        Foreground = t.Text,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Subtitle(Theme t, string text) => new()
    {
        Text = text,
        FontSize = 13.5,
        Foreground = t.TextMuted,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 0),
    };

    /// <summary>Title + one-sentence subtitle on the left, an optional control on the right.</summary>
    public static DockPanel PageHeader(Theme t, string title, string subtitle, UIElement? right = null)
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 26), LastChildFill = true };
        if (right is FrameworkElement fe)
        {
            fe.VerticalAlignment = VerticalAlignment.Top;
            fe.Margin = new Thickness(20, 2, 0, 0);
            DockPanel.SetDock(fe, Dock.Right);
            header.Children.Add(fe);
        }
        var text = new StackPanel();
        text.Children.Add(Title(t, title));
        if (!string.IsNullOrEmpty(subtitle))
            text.Children.Add(Subtitle(t, subtitle));
        header.Children.Add(text);
        return header;
    }

    /// <summary>Small uppercase section head in the accent, like the Workshop's.</summary>
    public static TextBlock Section(Theme t, string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 11,
        FontWeight = FontWeights.ExtraBold,
        Foreground = t.AccentInk,
        Margin = new Thickness(0, 0, 0, 10),
    };

    /// <summary>Uppercase caption in the muted colour (tile captions, meta).</summary>
    public static TextBlock Caption(Theme t, string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 11,
        FontWeight = FontWeights.Bold,
        Foreground = t.TextMuted,
    };

    public static TextBlock Label(Theme t, string text, bool muted = false) => new()
    {
        Text = text,
        FontSize = 14,
        LineHeight = 20,
        Foreground = muted ? t.TextMuted : t.TextBody,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>An empty state: one plain sentence, never a bare "No data".</summary>
    public static TextBlock Empty(Theme t, string sentence)
    {
        var block = Label(t, sentence, muted: true);
        block.Margin = new Thickness(0, 4, 0, 4);
        return block;
    }

    public static TextBlock Glyph(Theme t, string code, double size = 16, Brush? brush = null) => new()
    {
        Text = code,
        FontFamily = Theme.IconFont,
        FontSize = size,
        Foreground = brush ?? t.TextMuted,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>A keycap chip ("Ctrl", "Win", "F9").</summary>
    public static Border Kbd(Theme t, string text) => new()
    {
        Background = t.RaiseBg,
        BorderBrush = t.Line2,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(6, 1, 6, 2),
        Margin = new Thickness(0, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = t.Text,
        },
    };

    /// <summary>"Ctrl+Win" as a row of keycaps.</summary>
    public static StackPanel HotkeyKbds(Theme t, string hotkey)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var part in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            row.Children.Add(Kbd(t, part));
        return row;
    }

    // ---- surfaces

    /// <summary>A panel card: hairline border, 4 px corners, no shadow (depth comes from the layer colours).</summary>
    public static Border Card(Theme t, UIElement child, double padding = 18) => new()
    {
        Background = t.CardBg,
        BorderBrush = t.CardBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(Theme.Radius),
        Padding = new Thickness(padding),
        Margin = new Thickness(0, 0, 0, 12),
        Child = child,
    };

    /// <summary>A raised row inside a card (list entries).</summary>
    public static Border Raised(Theme t, UIElement child, double padding = 12) => new()
    {
        Background = t.RaiseBg,
        BorderBrush = t.CardBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(Theme.Radius),
        Padding = new Thickness(padding),
        Margin = new Thickness(0, 0, 0, 8),
        Child = child,
    };

    public static Border Divider(Theme t, double top = 12, double bottom = 0) => new()
    {
        Height = 1,
        Background = t.CardBorder,
        Margin = new Thickness(0, top, 0, bottom),
    };

    // ---- controls

    /// <summary>Primary = accent fill; otherwise a quiet raised tile with a hairline.</summary>
    public static Button ActionButton(Theme t, string text, bool primary = false) => new()
    {
        Content = text,
        Padding = new Thickness(14, 7, 14, 7),
        FontSize = 13,
        FontWeight = primary ? FontWeights.ExtraBold : FontWeights.SemiBold,
        Foreground = primary ? t.OnAccent : t.TextBody,
        Background = primary ? t.Accent : t.RaiseBg,
        BorderBrush = primary ? t.Accent : t.Line2,
        BorderThickness = new Thickness(1),
        Cursor = System.Windows.Input.Cursors.Hand,
    };

    /// <summary>A quiet button with a glyph before the label (Copy, Edit &amp; learn).</summary>
    public static Button GlyphButton(Theme t, string glyph, string text)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(Glyph(t, glyph, 13, t.TextMuted));
        content.Children.Add(new TextBlock { Text = text, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        var button = ActionButton(t, "");
        button.Content = content;
        button.Padding = new Thickness(11, 6, 12, 6);
        return button;
    }

    /// <summary>The record button: a pill with the mic glyph. Colour is driven by the caller (accent idle, red live).</summary>
    public static Button PillButton(Theme t, string text, string glyph)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(Glyph(t, glyph, 17, t.OnAccent));
        content.Children.Add(new TextBlock
        {
            Text = text,
            Margin = new Thickness(9, 0, 0, 0),
            FontWeight = FontWeights.ExtraBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(18, 0, 22, 0),
            Height = 44,
            Foreground = t.OnAccent,
            Background = t.Accent,
            BorderBrush = t.Accent,
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect { Color = t.AccentGlow, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.25 },
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Pill");
        return button;
    }

    /// <summary>Repaints a pill button for its state and starts or stops the breathing glow.</summary>
    public static void SetPillState(Theme t, Button button, bool live, string text)
    {
        var content = (StackPanel)button.Content;
        ((TextBlock)content.Children[1]).Text = text;
        var fg = live ? Brushes.White : t.OnAccent;
        button.Foreground = fg;
        ((TextBlock)content.Children[0]).Foreground = fg;
        button.Background = live ? t.Recording : t.Accent;
        button.BorderBrush = button.Background;
        if (button.Effect is DropShadowEffect glow)
        {
            glow.Color = live ? t.RecordingGlow : t.AccentGlow;
            if (live)
                glow.BeginAnimation(DropShadowEffect.OpacityProperty,
                    new DoubleAnimation(0.3, 0.8, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            else
            {
                glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                glow.Opacity = 0.25;
            }
        }
    }

    public static TextBox Input(Theme t, string text = "", double width = double.NaN) => new()
    {
        Text = text,
        FontSize = 14,
        Foreground = t.Text,
        Background = t.InputBg,
        BorderBrush = t.Line2,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 7, 10, 7),
        Width = width,
        VerticalContentAlignment = VerticalAlignment.Center,
        CaretBrush = t.Text,
    };

    public static ComboBox Combo(Theme t, IEnumerable<string> items, string selected)
    {
        var combo = new ComboBox
        {
            FontSize = 13.5,
            Padding = new Thickness(8, 7, 8, 7),
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        foreach (var item in items)
            combo.Items.Add(item);
        combo.SelectedItem = selected;
        return combo;
    }

    /// <summary>A card with a title, a sentence and a switch on the right (Style page, on/off settings).</summary>
    public static Border Toggle(Theme t, string title, string description, bool initial, Action<bool> onChange)
    {
        var toggle = new CheckBox { IsChecked = initial, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        toggle.SetResourceReference(FrameworkElement.StyleProperty, "Switch");
        toggle.Checked += (_, _) => onChange(true);
        toggle.Unchecked += (_, _) => onChange(false);

        var titleBlock = Label(t, title);
        titleBlock.Foreground = t.Text;
        titleBlock.FontWeight = FontWeights.Bold;
        var desc = Label(t, description, muted: true);
        desc.FontSize = 13;
        desc.LineHeight = 18;
        desc.Margin = new Thickness(0, 3, 0, 0);
        var text = new StackPanel();
        text.Children.Add(titleBlock);
        text.Children.Add(desc);

        var row = new DockPanel();
        DockPanel.SetDock(toggle, Dock.Right);
        row.Children.Add(toggle);
        row.Children.Add(text);
        return Card(t, row);
    }

    public static StackPanel Row(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            if (child is FrameworkElement fe)
            {
                fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top, fe.Margin.Right + 8, fe.Margin.Bottom);
                fe.VerticalAlignment = VerticalAlignment.Center;
            }
            panel.Children.Add(child);
        }
        return panel;
    }

    // ---- motion

    /// <summary>Page entrance: a short fade with an 8 px rise, 167 ms, decelerating. Honours the OS animation setting.</summary>
    public static void AnimateIn(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;
        var slide = new TranslateTransform(0, 8);
        element.RenderTransform = slide;
        element.Opacity = 0;
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(167)) { EasingFunction = EaseOut });
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = EaseOut });
    }

    /// <summary>A barely-there lift on hover (1.5%), 120 ms in, 160 ms out.</summary>
    public static void AddHoverLift(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;
        var scale = new ScaleTransform(1, 1);
        element.RenderTransform = scale;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.MouseEnter += (_, _) =>
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.015, TimeSpan.FromMilliseconds(120)) { EasingFunction = EaseOut });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.015, TimeSpan.FromMilliseconds(120)) { EasingFunction = EaseOut });
        };
        element.MouseLeave += (_, _) =>
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut });
        };
    }

    // ---- data views

    /// <summary>Big number, uppercase caption, optional one-line detail.</summary>
    public static UIElement StatTile(Theme t, string value, string caption, string? detail = null, Brush? detailBrush = null)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 32,
            FontWeight = FontWeights.ExtraBold,
            Foreground = t.Text,
            LineHeight = 34,
        });
        var cap = Caption(t, caption);
        cap.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(cap);
        if (!string.IsNullOrEmpty(detail))
        {
            stack.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 12,
                Foreground = detailBrush ?? t.TextFaint,
                Margin = new Thickness(0, 6, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        var card = Card(t, stack, padding: 16);
        card.Margin = new Thickness(0, 0, 12, 12);
        AddHoverLift(card);
        return card;
    }

    /// <summary>Words-per-day bars for the last 14 days, today brighter. Shared by Home and Insights.</summary>
    public static UIElement BarChart(Theme t, IReadOnlyList<DayStats> days, string caption, string emptySentence)
    {
        var byDate = days.ToDictionary(d => d.Date, d => d.Words);
        var today = DateTime.Today;
        var window = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13)).ToList();
        var stack = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var right = Label(t, "last 14 days", muted: true);
        right.FontSize = 12;
        right.Foreground = t.TextFaint;
        DockPanel.SetDock(right, Dock.Right);
        head.Children.Add(right);
        var section = Section(t, caption);
        section.Margin = new Thickness(0);
        head.Children.Add(section);
        stack.Children.Add(head);

        if (window.Sum(d => byDate.GetValueOrDefault(d)) == 0)
        {
            stack.Children.Add(Empty(t, days.Count == 0 ? emptySentence : "Nothing in the last two weeks. The chart fills in again as soon as you dictate."));
            return Card(t, stack);
        }
        var maxWords = Math.Max(1, window.Max(d => byDate.GetValueOrDefault(d)));
        var bars = new UniformGrid { Rows = 1, Columns = 14, Height = 110 };
        var index = 0;
        var animate = SystemParameters.ClientAreaAnimation;
        foreach (var date in window)
        {
            var words = byDate.GetValueOrDefault(date);
            var target = Math.Max(3, 104.0 * words / maxWords);
            var rect = new Rectangle
            {
                Height = animate ? 3 : target,
                Margin = new Thickness(3, 0, 3, 0),
                RadiusX = 2, RadiusY = 2,
                Fill = t.AccentGradient,
                Opacity = date == today ? 1 : 0.55,
                VerticalAlignment = VerticalAlignment.Bottom,
                ToolTip = $"{date:ddd, MMM d}: {words:N0} words",
            };
            if (animate)
                rect.BeginAnimation(FrameworkElement.HeightProperty,
                    new DoubleAnimation(3, target, TimeSpan.FromMilliseconds(350))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(80 + 25 * index),
                        EasingFunction = EaseOut,
                    });
            index++;
            bars.Children.Add(rect);
        }
        stack.Children.Add(bars);

        var axis = new DockPanel { Margin = new Thickness(3, 8, 3, 0) };
        var end = Label(t, "Today", muted: true);
        end.FontSize = 10.5;
        end.FontWeight = FontWeights.Bold;
        end.Foreground = t.TextFaint;
        DockPanel.SetDock(end, Dock.Right);
        axis.Children.Add(end);
        var start = Label(t, window[0].ToString("d MMM"), muted: true);
        start.FontSize = 10.5;
        start.FontWeight = FontWeights.Bold;
        start.Foreground = t.TextFaint;
        axis.Children.Add(start);
        stack.Children.Add(axis);
        return Card(t, stack);
    }
}
