using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

public sealed class StylePage : UserControl
{
    public StylePage(Theme t, AppSettings settings)
    {
        var panel = new StackPanel();
        panel.Children.Add(UiKit.Title(t, "Style / Transforms"));

        var hint = UiKit.Label(t,
            "Rule-based cleanups applied to every transcript — all offline, no AI rewriting. Conservative by design.",
            muted: true);
        hint.Margin = new Thickness(0, 0, 0, 12);
        panel.Children.Add(hint);

        panel.Children.Add(ToggleCard(t,
            "Remove filler words",
            "Strips standalone \"um\", \"uh\", \"er\" from transcripts.",
            settings.RemoveFillerWords,
            v => { settings.RemoveFillerWords = v; settings.Save(); }));

        panel.Children.Add(ToggleCard(t,
            "Auto-learn spelled-out words",
            "Spell a word (\"I R C C\") and Bolo types the acronym AND adds it to the Auto-learned dictionary cluster. Also enables the voice command: \"add to dictionary: <word>\".",
            settings.AutoLearn,
            v => { settings.AutoLearn = v; settings.Save(); }));

        panel.Children.Add(ToggleCard(t,
            "Spoken lists → bullet lists",
            "\"First, do X. Second, do Y. Finally, Z.\" becomes an actual bulleted list (needs at least two markers).",
            settings.ConvertSpokenLists,
            v => { settings.ConvertSpokenLists = v; settings.Save(); }));

        var note = UiKit.Label(t,
            "Auto-punctuation and capitalization are handled by the Whisper model itself. True AI style-rewriting is deliberately out of scope for v1 (would need an LLM / break the offline guarantee).",
            muted: true);
        note.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(note);

        Content = panel;
    }

    private static UIElement ToggleCard(Theme t, string title, string description, bool initial, Action<bool> onChange)
    {
        var toggle = new CheckBox
        {
            IsChecked = initial,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 10, 0),
        };
        toggle.Checked += (_, _) => onChange(true);
        toggle.Unchecked += (_, _) => onChange(false);

        var titleBlock = UiKit.Label(t, title);
        titleBlock.FontWeight = FontWeights.SemiBold;
        var desc = UiKit.Label(t, description, muted: true);
        desc.Margin = new Thickness(0, 3, 0, 0);

        var textStack = new StackPanel();
        textStack.Children.Add(titleBlock);
        textStack.Children.Add(desc);

        var row = new DockPanel();
        DockPanel.SetDock(toggle, Dock.Left);
        row.Children.Add(toggle);
        row.Children.Add(textStack);
        return UiKit.Card(t, row);
    }
}
