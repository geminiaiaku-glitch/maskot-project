using Maskot.Core;

namespace Maskot.Core.Tests;

public sealed class InteractionTests
{
    private static readonly DisplayArea Main = new("main", new(0, 0, 1920, 1080), new(0, 0, 1920, 1032), 1, 1029);
    private static readonly DisplayArea Left = new("left", new(-2560, -200, 2560, 1440), new(-2560, -200, 2560, 1380), 1.5, 1175);
    private static readonly DisplayArea[] Displays = [Main, Left];
    private static MascotController Create(MascotMode mode = MascotMode.Stationary)
    {
        var c = new MascotController(42); c.Restore(new(mode, new(300, 400), Main.Id), Displays); return c;
    }
    [Fact] public void ClickAllowsSmallJitter()
    {
        var c = Create(); var before = c.Position;
        c.Press(before + new PointD(50, 30)); c.MovePointer(before + new PointD(52, 32), Displays);
        Assert.True(c.Release(Displays)); Assert.Equal(before, c.Position);
    }
    [Fact] public void DragKeepsPointerOffsetAndNeverClicks()
    {
        var c = Create(MascotMode.Walking); var start = c.Position;
        c.Press(start + new PointD(20, 30)); c.MovePointer(new(750, 430), Displays);
        Assert.True(c.Dragging); Assert.Equal(new PointD(730, 400), c.Position);
        Assert.False(c.Release(Displays)); Assert.Equal(MascotMode.Stationary, c.Mode);
        for (var i = 0; i < 300; i++) c.Tick(.04, new(1800, 1000), Displays);
        Assert.Equal(new PointD(730, 400), c.Position);
    }
    [Fact] public void DockOnlyOnRelease()
    {
        var c = Create(); c.Press(c.Position + new PointD(20, 30)); c.MovePointer(new(960, 25), Displays);
        Assert.True(c.DockEligible); Assert.True(c.DockNearby); Assert.NotEqual(MascotMode.Notch, c.Mode);
        Assert.False(c.Release(Displays)); Assert.Equal(MascotMode.Notch, c.Mode); Assert.Equal(new PointD(845, 0), c.Position);
    }
    [Fact] public void OutsideTargetRemainsStationary()
    {
        var c = Create(); c.Press(c.Position + new PointD(20, 30)); c.MovePointer(new(500, 30), Displays);
        Assert.False(c.DockEligible); c.Release(Displays); Assert.Equal(MascotMode.Stationary, c.Mode);
    }
    [Fact] public void DockEligibilityHasHysteresis()
    {
        var c = Create(); c.Press(c.Position); c.MovePointer(new(960, 90), Displays);
        Assert.True(c.DockEligible); c.MovePointer(new(960, 108), Displays); Assert.True(c.DockEligible);
        c.MovePointer(new(960, 120), Displays); Assert.False(c.DockEligible);
        c.MovePointer(new(960, 108), Displays); Assert.False(c.DockEligible);
    }
    [Theory] [InlineData(MascotMode.Walking)] [InlineData(MascotMode.Stationary)] [InlineData(MascotMode.Notch)]
    public void EscapeRestoresExactModePositionAndMonitor(MascotMode mode)
    {
        var c = Create(mode); var before = c.Snapshot(); c.Press(c.Position + new PointD(20, 10));
        c.MovePointer(new(-1000, 400), Displays); Assert.True(c.Dragging); c.CancelDrag();
        Assert.Equal(before, c.Snapshot()); Assert.False(c.PointerHeld); Assert.False(c.Release(Displays));
    }
    [Fact] public void NotchWiggleDoesNotClickOrUndock()
    {
        var c = Create(MascotMode.Notch); var before = c.Position;
        c.Press(c.Position + new PointD(50, 20)); c.MovePointer(c.Position + new PointD(100, 30), Displays);
        Assert.False(c.Release(Displays)); Assert.Equal(MascotMode.Notch, c.Mode); Assert.Equal(before, c.Position);
    }
    [Fact] public void NotchPullDownUndocksAndReleaseStays()
    {
        var c = Create(MascotMode.Notch); c.Press(c.Position + new PointD(50, 20)); c.MovePointer(new(940, 250), Displays);
        Assert.Equal(MascotPose.Lifted, c.Pose); Assert.Equal(MascotMode.Stationary, c.Mode);
        c.Release(Displays); Assert.Equal(MascotMode.Stationary, c.Mode);
    }
    [Fact] public void UndockingWithinTargetDoesNotImmediatelyRedock()
    {
        var c = Create(MascotMode.Notch); c.Press(c.Position + new PointD(50, 10));
        c.MovePointer(new(940, 80), Displays); Assert.Equal(MascotMode.Stationary, c.Mode);
        Assert.False(c.DockEligible); c.Release(Displays); Assert.Equal(MascotMode.Stationary, c.Mode);
    }
    [Fact] public void UndockedMascotCanRedockAfterLeavingAndReenteringTarget()
    {
        var c = Create(MascotMode.Notch); c.Press(c.Position + new PointD(50, 10));
        c.MovePointer(new(940, 200), Displays); c.MovePointer(new(940, 20), Displays);
        Assert.True(c.DockEligible); c.Release(Displays); Assert.Equal(MascotMode.Notch, c.Mode);
    }
    [Fact] public void DockUsesMonitorUnderPointerIncludingNegativeCoordinates()
    {
        var c = Create(); c.Press(c.Position); c.MovePointer(new(-1280, -170), Displays); c.Release(Displays);
        Assert.Equal(MascotMode.Notch, c.Mode); Assert.Equal("left", c.MonitorId);
        Assert.Equal(new PointD(-1452.5, -200), c.Position);
    }
    [Fact] public void PanelAndPointerProximityPauseWalking()
    {
        var c = Create(MascotMode.Walking); c.ExtraAnimations = false; var before = c.Position;
        c.SetPanel(true); c.Tick(.1, new(0, 0), Displays); Assert.Equal(before, c.Position);
        c.SetPanel(false); c.Tick(.1, new(0, 0), Displays); Assert.Equal(before, c.Position);
        for (var i = 0; i < 15; i++) c.Tick(.1, c.Position + new PointD(20, 20), Displays);
        Assert.Equal(before, c.Position); Assert.Equal(MascotPose.Greet, c.Pose);
        c.Tick(.1, new(1800, 200), Displays); Assert.NotEqual(before.X, c.Position.X);
    }
    [Fact] public void WalkCommandReturnsToBottom()
    {
        var c = Create(); c.Walk(Main); Assert.Equal(MascotMode.Walking, c.Mode); Assert.Equal(Main.WalkingBottom - 142, c.Position.Y);
    }
    [Fact] public void StationaryDoesNotFollowTaskbar()
    {
        var c = Create(); var before = c.Position; var changed = Main with { WalkingBottom = 1075 };
        c.Tick(.1, new(1800, 0), [changed]); Assert.Equal(before, c.Position);
    }
    [Fact] public void WalkingSmoothlyTracksTaskbarVisibility()
    {
        var c = Create(MascotMode.Walking); var y = c.Position.Y; var changed = Main with { WalkingBottom = 1075 };
        c.Tick(.04, new(1800, 0), [changed]); Assert.InRange(c.Position.Y, y + 1, changed.WalkingBottom - 142 - 1);
    }
    [Fact] public void MonitorRemovalRestoresSafePosition()
    {
        var c = Create(); c.Restore(new(MascotMode.Stationary, new(-2400, -100), "left"), Displays);
        c.ReconcileDisplays([Main]); Assert.Equal("main", c.MonitorId); Assert.True(Main.Bounds.Contains(c.Position));
    }
    [Fact] public void DpiDimensionsAndDockAreScaled()
    {
        var c = Create(); c.Size = 1.5; Assert.Equal((252d, 319.5), c.Dimensions(1.5));
        c.Restore(new(MascotMode.Notch, new(-1000, 0), "left"), Displays); Assert.Equal((345d, 63d), c.Dimensions(1.5));
    }
    [Fact] public void PanelPlacementClampsToNegativeMonitorWorkArea()
    {
        var p = DesktopGeometry.PlacePanel(new(-30, 1150, 112, 142), Left.WorkArea, 600, 800);
        Assert.True(p.X >= Left.WorkArea.X); Assert.True(p.X + 600 <= Left.WorkArea.Right);
        Assert.True(p.Y >= Left.WorkArea.Y); Assert.True(p.Y + 800 <= Left.WorkArea.Bottom);
    }
    [Fact] public void WalkingBouncesAtBoundary()
    {
        var c = Create(MascotMode.Walking); c.ExtraAnimations = false;
        c.Restore(new(MascotMode.Walking, new(1807, 100), Main.Id), Displays);
        for (var i = 0; i < 10; i++) c.Tick(.1, new(0, 0), Displays);
        Assert.False(c.FacingRight); Assert.InRange(c.Position.X, 0, 1804);
    }
}
