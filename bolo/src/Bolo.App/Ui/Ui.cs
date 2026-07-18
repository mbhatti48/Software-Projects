using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Bolo.Core.Learning;

namespace Bolo.App.Ui;

/// <summary>Small factory helpers so the code-built pages stay readable.</summary>
public static class UiKit
{
    public static TextBlock Title(Theme t, string text) => new()
    {
        Text = text,
        FontSize = 24,
        FontWeight = FontWeights.SemiBold,
        Foreground = t.Text,
        Margin = new Thickness(0, 0, 0, 16),
    };

    public static TextBlock Label(Theme t, string text, bool muted = false) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = muted ? t.TextMuted : t.Text,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static Border Card(Theme t, UIElement child, double padding = 18) => new()
    {
        Background = t.CardBg,
        BorderBrush = t.CardBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(padding),
        Margin = new Thickness(0, 0, 0, 12),
        Child = child,
        // Soft elevation — the "3D" depth without the 2005 bevel look.
        Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 14,
            ShadowDepth = 3,
            Opacity = 0.30,
        },
    };

    public static Button ActionButton(Theme t, string text, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(16, 8, 16, 8),
            FontSize = 13,
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = primary ? Brushes.White : t.Text,
            Background = primary ? t.AccentGradient : t.InputBg,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        if (primary)
        {
            button.Effect = new DropShadowEffect
            {
                Color = t.AccentGlow,
                BlurRadius = 16,
                ShadowDepth = 0,
                Opacity = 0.35,
            };
        }
        button.Resources.Add(typeof(Border), new Style(typeof(Border))
        {
            Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(8)) },
        });
        return button;
    }

    public static TextBox Input(Theme t, string text = "", double width = double.NaN) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = t.Text,
        Background = t.InputBg,
        BorderBrush = t.CardBorder,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(9, 7, 9, 7),
        Width = width,
        VerticalContentAlignment = VerticalAlignment.Center,
        CaretBrush = t.Text,
    };

    public static ComboBox Combo(Theme t, IEnumerable<string> items, string selected)
    {
        var combo = new ComboBox
        {
            FontSize = 13,
            Padding = new Thickness(8, 5, 8, 5),
            MinWidth = 160,
        };
        foreach (var item in items)
            combo.Items.Add(item);
        combo.SelectedItem = selected;
        return combo;
    }

    public static StackPanel Row(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            if (child is FrameworkElement fe)
                fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top, fe.Margin.Right + 8, fe.Margin.Bottom);
            panel.Children.Add(child);
        }
        return panel;
    }

    private static readonly CubicEase EaseOut = new() { EasingMode = EasingMode.EaseOut };

    /// <summary>Fade + upward glide, used when a page loads.</summary>
    public static void AnimateIn(FrameworkElement element)
    {
        var slide = new TranslateTransform(0, 14);
        element.RenderTransform = slide;
        element.Opacity = 0;
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = EaseOut });
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = EaseOut });
    }

    /// <summary>Subtle grow-toward-you on hover.</summary>
    private static void AddHoverLift(FrameworkElement element)
    {
        var scale = new ScaleTransform(1, 1);
        element.RenderTransform = scale;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.MouseEnter += (_, _) =>
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.025, TimeSpan.FromMilliseconds(130)) { EasingFunction = EaseOut });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.025, TimeSpan.FromMilliseconds(130)) { EasingFunction = EaseOut });
        };
        element.MouseLeave += (_, _) =>
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut });
        };
    }

    public static UIElement StatTile(Theme t, string value, string caption)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            Foreground = t.Text,
        });
        var cap = Label(t, caption, muted: true);
        cap.Margin = new Thickness(0, 2, 0, 0);
        stack.Children.Add(cap);
        var card = Card(t, stack);
        card.Margin = new Thickness(0, 0, 12, 12);
        AddHoverLift(card);
        return card;
    }

    /// <summary>Words-per-day bar strip (last 14 days). Shared by Home and Insights.</summary>
    public static UIElement BarChart(Theme t, IReadOnlyList<DayStats> days, string caption)
    {
        var recent = days.TakeLast(14).ToList();
        var chartStack = new StackPanel();
        chartStack.Children.Add(Label(t, caption, muted: true));
        if (recent.Count == 0)
        {
            var none = Label(t, "No data yet.");
            none.Margin = new Thickness(0, 8, 0, 0);
            chartStack.Children.Add(none);
            return Card(t, chartStack);
        }

        var maxWords = Math.Max(1, recent.Max(d => d.Words));
        var bars = new StackPanel { Orientation = Orientation.Horizontal, Height = 96, Margin = new Thickness(0, 10, 0, 0) };
        var index = 0;
        foreach (var day in recent)
        {
            var bar = new StackPanel { Width = 46, VerticalAlignment = VerticalAlignment.Bottom };
            var rect = new Rectangle
            {
                Height = 3,
                Width = 26,
                RadiusX = 5, RadiusY = 5,
                Fill = t.AccentGradient,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            };
            // Bars ripple upward as the page opens.
            rect.BeginAnimation(FrameworkElement.HeightProperty,
                new DoubleAnimation(3, Math.Max(3, 62.0 * day.Words / maxWords), TimeSpan.FromMilliseconds(450))
                {
                    BeginTime = TimeSpan.FromMilliseconds(120 + 35 * index++),
                    EasingFunction = EaseOut,
                });
            bar.Children.Add(rect);
            var label = Label(t, day.Date.ToString("M/d"), muted: true);
            label.FontSize = 10;
            label.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 5, 0, 0);
            bar.Children.Add(label);
            bars.Children.Add(bar);
        }
        chartStack.Children.Add(bars);
        return Card(t, chartStack);
    }
}
