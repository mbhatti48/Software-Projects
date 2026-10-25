using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

public sealed class StylePage : UserControl
{
    public StylePage(Theme t, AppSettings settings)
    {
        var panel = new StackPanel();
        panel.Children.Add(UiKit.PageHeader(t, "Style",
            "Rule-based cleanups applied to every transcript. All offline, no AI rewriting, conservative by design."));

        panel.Children.Add(UiKit.Toggle(t,
            "Remove filler words",
            "Strips standalone \"um\", \"uh\" and \"er\" from transcripts.",
            settings.RemoveFillerWords,
            v => { settings.RemoveFillerWords = v; settings.Save(); }));

        panel.Children.Add(UiKit.Toggle(t,
            "Auto-learn spelled-out words",
            "Spell a word (\"I R C C\") and Bolo types the acronym and adds it to the Auto-learned dictionary cluster. Also enables the voice command \"add to dictionary: <word>\".",
            settings.AutoLearn,
            v => { settings.AutoLearn = v; settings.Save(); }));

        panel.Children.Add(UiKit.Toggle(t,
            "Spoken lists to bullet lists",
            "\"First, do X. Second, do Y. Finally, Z.\" becomes a bulleted list. Needs at least two markers.",
            settings.ConvertSpokenLists,
            v => { settings.ConvertSpokenLists = v; settings.Save(); }));

        var note = UiKit.Label(t,
            "Punctuation and capitalisation come from the Whisper model itself. AI style rewriting is deliberately out of scope: it would need a language model and break the offline guarantee.",
            muted: true);
        note.FontSize = 13;
        note.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(note);

        Content = panel;
    }
}
