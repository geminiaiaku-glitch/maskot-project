using Maskot.Core;

namespace Maskot.Core.Tests;

public sealed class NotchPeekTests
{
    [Fact] public void BriefPassDoesNotOpenAndSustainedHoverDoes()
    {
        var peek = new NotchPeek();
        Assert.Equal(PeekAction.None, At(peek, 0));
        Assert.Equal(PeekAction.None, At(peek, 120));
        At(peek, 140, inside: false);
        Assert.Equal(PeekAction.None, At(peek, 200));
        Assert.Equal(PeekAction.Open, At(peek, 381));
    }
    [Fact] public void MovingIntoPanelCancelsLeaveGrace()
    {
        var p = new NotchPeek();
        At(p, 0, false, true); At(p, 300, true, true);
        Assert.Equal(PeekAction.None, At(p, 450, false, true));
        Assert.Equal(PeekAction.None, At(p, 800, false, true));
        Assert.Equal(PeekAction.Close, At(p, 851, false, true));
    }
    [Fact] public void EditingOrDraggingDoesNotCollapsePreview()
    {
        var p = new NotchPeek();
        Assert.Equal(PeekAction.None, At(p, 0, false, true, editing: true));
        Assert.Equal(PeekAction.None, At(p, 2000, false, true, editing: true));
        Assert.Equal(PeekAction.None, At(p, 2100, false, true));
        Assert.Equal(PeekAction.Close, At(p, 2501, false, true));
    }
    [Fact] public void ExplicitDismissDoesNotImmediatelyReopen()
    {
        var p = new NotchPeek(); p.Dismiss();
        Assert.Equal(PeekAction.None, At(p, 0)); Assert.Equal(PeekAction.None, At(p, 2000));
        At(p, 2100, inside: false); At(p, 2200);
        Assert.Equal(PeekAction.Open, At(p, 2400));
    }
    [Fact] public void ManuallyOpenedPanelStaysUntilDismissed()
    {
        var p = new NotchPeek();
        Assert.Equal(PeekAction.None, p.Update(TimeSpan.Zero, true, false, true, false, false));
        Assert.Equal(PeekAction.None, p.Update(TimeSpan.FromSeconds(10), true, false, true, false, false));
    }
    [Fact] public void DisabledPeekClearsPendingIntent()
    {
        var p = new NotchPeek(); At(p, 0);
        p.Update(TimeSpan.FromMilliseconds(100), false, true, false, false, false);
        Assert.False(p.Pending); Assert.Equal(PeekAction.None, At(p, 500));
        Assert.Equal(PeekAction.Open, At(p, 700));
    }
    [Theory] [InlineData(1)] [InlineData(1.5)] [InlineData(2)]
    public void ExpandedPanelFitsNegativeMonitorAndStartsAtTop(double dpi)
    {
        var display = new DisplayArea("left", new(-1280, -200, 1280, 800), new(-1280, -200, 1280, 760), dpi, 540);
        var r = DesktopGeometry.NotchPanel(display, 620, 520);
        Assert.Equal(-200, r.Y); Assert.Equal(display.Bounds.Center.X, r.Center.X);
        Assert.True(r.X >= display.Bounds.X && r.Right <= display.Bounds.Right);
        Assert.True(r.Bottom <= display.WorkArea.Bottom);
    }
    private static PeekAction At(NotchPeek p, double ms, bool inside = true, bool open = false, bool editing = false) =>
        p.Update(TimeSpan.FromMilliseconds(ms), true, inside, open, true, editing);
}
