using System.Windows;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Views;

public partial class LyricBubbleWindow : Window
{
    public LyricBubbleWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Desktop.MakePassive(this, true);
    }
    internal string CurrentLine => Line.Text;
    internal void Update(KaraokeCue cue, string title, RectD body, DisplayArea display)
    {
        Line.Text = cue.Current; Song.Text = cue.Mood == KaraokeMood.Singing && !cue.Synchronized ? "Tanpa waktu · " + title : title;
        Symbol.Text = cue.Mood == KaraokeMood.Confused ? "?" : cue.Mood == KaraokeMood.Listening ? "♪" : "♫";
        if (!IsVisible) Show();
        var width = Math.Min(308 * display.Scale, display.WorkArea.Width - 12 * display.Scale);
        var height = 106 * display.Scale;
        var point = DesktopGeometry.PlaceLyrics(body, display.WorkArea, width, height, 6 * display.Scale);
        Desktop.Position(this, point, width, height);
    }
}
