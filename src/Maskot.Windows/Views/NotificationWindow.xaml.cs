using System.Windows;
using Maskot.Windows.Platform;

namespace Maskot.Windows.Views;

public partial class NotificationWindow : Window
{
    private App Host => (App)Application.Current;
    public NotificationWindow()
    {
        InitializeComponent(); SourceInitialized += (_, _) => Desktop.MakePassive(this);
    }
    internal void Update(DesktopNotice notice, bool hideContent, RectD anchor, DisplayArea display, bool docked)
    {
        Source.Text = notice.SourceName;
        Heading.Text = hideContent ? "Notifikasi baru" : notice.Title;
        Message.Text = hideContent ? "Buka aplikasi sumber untuk membaca isinya." : notice.Body;
        if (!IsVisible) Show();
        var scale = Math.Min(display.Scale, Math.Min(display.WorkArea.Width / 392, display.WorkArea.Height / 194));
        var width = 380 * scale; var height = 182 * scale;
        var p = docked ? display.WorkArea.Clamp(new(anchor.Center.X - width / 2, anchor.Bottom + 10 * scale), width, height)
            : DesktopGeometry.PlaceLyrics(anchor, display.WorkArea, width, height, 8 * scale);
        Desktop.Position(this, p, width, height);
    }
    private void DismissClicked(object sender, RoutedEventArgs e) => Host.DismissNotification();
    internal void ClearContents() { Hide(); Source.Text = Heading.Text = Message.Text = ""; }
    private void PauseClicked(object sender, RoutedEventArgs e) => Host.PauseNotifications();
    private void OpenClicked(object sender, RoutedEventArgs e) => Host.OpenNotificationsPanel();
}
