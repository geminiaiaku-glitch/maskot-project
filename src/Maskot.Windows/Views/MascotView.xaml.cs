using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Maskot.Windows.Views;

public partial class MascotView : UserControl
{
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _frames;
    private MascotPose _pose;
    private bool _extra = true;
    private KaraokeMood _performance;
    internal MascotFrame CurrentFrame { get; private set; }
    internal long FramesRendered { get; private set; }
    internal KaraokeMood Performance => _performance;
    public void SetPerformance(KaraokeMood mood)
    {
        if (_performance == mood) return;
        _performance = mood; DrawFrame();
    }

    public MascotView()
    {
        InitializeComponent();
        _frames = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
        _frames.Tick += (_, _) => DrawFrame();
        Loaded += (_, _) => UpdateClock();
        Unloaded += (_, _) => { _frames.Stop(); _clock.Stop(); };
        IsVisibleChanged += (_, _) => UpdateClock();
    }
    public void SetPose(MascotPose pose, bool facingRight, bool extra)
    {
        Flip.ScaleX = facingRight ? 1 : -1;
        if (_pose == pose && _extra == extra) return;
        _pose = pose; _extra = extra;
        DrawFrame(); UpdateClock();
    }
    private void UpdateClock()
    {
        if (_frames is null) return;
        if (IsLoaded && IsVisible && _extra)
        {
            _frames.Interval = TimeSpan.FromMilliseconds(_pose is MascotPose.Walk or MascotPose.Lifted ? 40 : 50);
            _clock.Start(); _frames.Start(); DrawFrame();
        }
        else { _frames.Stop(); _clock.Stop(); if (!_extra) DrawFrame(); }
    }
    private void DrawFrame()
    {
        var f = MascotAnimation.Sample(_pose, _clock.Elapsed.TotalSeconds, _extra, _performance);
        CurrentFrame = f; FramesRendered++;
        Bob.Y = f.Bob; Lean.Angle = f.Lean; Head.Angle = f.HeadAngle;
        LeftFoot.Angle = f.LeftFoot; RightFoot.Angle = f.RightFoot;
        LeftArm.Angle = f.LeftArm; RightArm.Angle = f.RightArm;
        Tail.Angle = f.TailAngle; Eyes.ScaleY = f.EyeScale; Breath.ScaleY = f.Breathe;
        SleepMark.Opacity = f.SleepOpacity;
        Greeting.Visibility = _pose == MascotPose.Greet && _performance == KaraokeMood.None ? Visibility.Visible : Visibility.Collapsed;
        var performing = _performance != KaraokeMood.None && _pose != MascotPose.Lifted;
        Microphone.Visibility = performing ? Visibility.Visible : Visibility.Collapsed;
        MicLift.Angle = f.RightArm;
        var singing = performing && _performance == KaraokeMood.Singing;
        var confused = performing && _performance == KaraokeMood.Confused;
        SingingMouth.Visibility = singing && f.MouthOpen > 0 ? Visibility.Visible : Visibility.Collapsed;
        NormalMouth.Visibility = SingingMouth.Visibility == Visibility.Visible || confused ? Visibility.Collapsed : Visibility.Visible;
        MouthOpening.ScaleY = Math.Max(.1, f.MouthOpen);
        MusicNote.Visibility = singing ? Visibility.Visible : Visibility.Collapsed;
        ConfusedMouth.Visibility = ConfusedBrows.Visibility = confused ? Visibility.Visible : Visibility.Collapsed;
    }
}
