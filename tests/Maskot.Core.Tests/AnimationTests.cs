using Maskot.Core;

namespace Maskot.Core.Tests;

public sealed class AnimationTests
{
    [Fact] public void WalkingArticulatesFeetInOppositeDirections()
    {
        var frame = MascotAnimation.Sample(MascotPose.Walk, .18);
        Assert.InRange(frame.LeftFoot, 23, 25); Assert.InRange(frame.RightFoot, -25, -23);
        Assert.True(frame.Bob < -5); Assert.NotEqual(frame, MascotAnimation.Sample(MascotPose.Walk, .36));
    }
    [Fact] public void StationaryPoseStillBreathesAndWagsTail()
    {
        var a = MascotAnimation.Sample(MascotPose.Idle, .3);
        var b = MascotAnimation.Sample(MascotPose.Idle, 1.4);
        Assert.NotEqual(a.Bob, b.Bob); Assert.NotEqual(a.TailAngle, b.TailAngle);
        Assert.NotEqual(a.Breathe, b.Breathe);
    }
    [Fact] public void BlinkHasClosedAndOpenFrames()
    {
        Assert.InRange(MascotAnimation.Sample(MascotPose.Idle, .07).EyeScale, 0, .1);
        Assert.Equal(1, MascotAnimation.Sample(MascotPose.Idle, .3).EyeScale);
    }
    [Fact] public void DisablingExtraAnimationsReturnsNeutralFrame()
    {
        var a = MascotAnimation.Sample(MascotPose.Walk, .2, false);
        Assert.Equal(a, MascotAnimation.Sample(MascotPose.Walk, 17, false));
        Assert.Equal(1, a.EyeScale); Assert.Equal(0, a.LeftFoot);
    }
    [Theory] [InlineData(MascotPose.Lifted)] [InlineData(MascotPose.Sleep)] [InlineData(MascotPose.Sit)] [InlineData(MascotPose.Greet)]
    public void EveryPoseHasDistinctMovingFrames(MascotPose pose)
    {
        Assert.NotEqual(MascotAnimation.Sample(pose, .2), MascotAnimation.Sample(pose, .6));
    }
    [Fact] public void UpgradeEnablesAnimationAndKeepsMascotVisibleWithoutLosingData()
    {
        var state = new AppState { Notes = "keep", Placement = new(MascotMode.Stationary, new(500, 200), "main") };
        state.Preferences.HideInFullscreen = true; state.Preferences.ExtraAnimations = false;
        state.Normalize();
        Assert.False(state.Preferences.HideInFullscreen); Assert.True(state.Preferences.ExtraAnimations);
        Assert.Equal("keep", state.Notes); Assert.Equal(MascotMode.Stationary, state.Placement!.Mode);
    }
    [Fact] public void CurrentPreferencesStillRespectExplicitHideAndAnimationChoices()
    {
        var state = new AppState(); state.Preferences.BehaviorVersion = 2;
        state.Preferences.HideInFullscreen = true; state.Preferences.ExtraAnimations = false;
        state.Normalize(); Assert.True(state.Preferences.HideInFullscreen); Assert.False(state.Preferences.ExtraAnimations);
    }
    [Fact] public void WalkStartsImmediatelyWithoutRandomSleep()
    {
        DisplayArea d = new("main", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 1, 1035);
        for (var seed = 0; seed < 40; seed++)
        {
            var c = new MascotController(seed); c.Restore(null, [d]); var x = c.Position.X;
            c.Tick(.04, new(0, 0), [d]); Assert.True(c.Position.X > x); Assert.Equal(MascotPose.Walk, c.Pose);
        }
    }
    [Fact] public void RepeatedCloseNotificationsDoNotKeepPausingWalking()
    {
        DisplayArea d = new("main", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 1, 1035);
        var c = new MascotController(1); c.Restore(null, [d]); c.SetPanel(true); c.SetPanel(false); var x = c.Position.X;
        for (var i = 0; i < 30; i++) { c.SetPanel(false); c.Tick(.04, new(0, 0), [d]); }
        Assert.True(c.Position.X > x);
    }
}
