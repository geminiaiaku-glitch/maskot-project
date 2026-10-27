using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Views;

internal sealed class DockHintWindow : Window
{
    private readonly Border _border;
    private readonly TextBlock _text;
    public DockHintWindow()
    {
        Width = 180; Height = 32; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        _text = new() { Text = "Lepas untuk menjadi notch", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _border = new() { CornerRadius = new(0, 0, 14, 14), BorderThickness = new(1), Child = _text };
        Content = _border;
        SourceInitialized += (_, _) => Desktop.MakePassive(this, true);
    }
    public void Update(DisplayArea display, bool eligible)
    {
        _border.Background = new SolidColorBrush(eligible ? Color.FromArgb(240, 69, 87, 45) : Color.FromArgb(130, 25, 29, 28));
        _border.BorderBrush = new SolidColorBrush(eligible ? Color.FromRgb(210, 237, 145) : Color.FromArgb(110, 190, 200, 180));
        _text.Text = eligible ? "✦ Lepaskan di sini" : "Tarik ke sini untuk notch";
        if (!IsVisible) Show();
        var r = display.DockHint; Desktop.Position(this, new(r.X, r.Y), r.Width, r.Height);
    }
}
