using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Maskot.Windows.Views;

public partial class MascotView : UserControl
{
    private MascotPose? _pose;
    private bool _extra;
    public MascotView() => InitializeComponent();
    public void SetPose(MascotPose pose, bool facingRight, bool extra)
    {
        Flip.ScaleX = facingRight ? 1 : -1;
        if (_pose == pose && _extra == extra) return;
        _pose = pose; _extra = extra;
        Bob.BeginAnimation(TranslateTransform.YProperty, null);
        Tilt.BeginAnimation(RotateTransform.AngleProperty, null);
        Bob.Y = 0; Tilt.Angle = 0;
        SleepEyes.Visibility = SleepMark.Visibility = pose == MascotPose.Sleep ? Visibility.Visible : Visibility.Collapsed;
        Greeting.Visibility = pose == MascotPose.Greet ? Visibility.Visible : Visibility.Collapsed;
        if (pose == MascotPose.Sit) Bob.Y = 7;
        if (!extra) return;
        var movement = new DoubleAnimation(pose == MascotPose.Walk ? -3 : pose == MascotPose.Lifted ? -5 : -1,
            new Duration(TimeSpan.FromSeconds(pose == MascotPose.Walk ? .3 : 1.8))) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(movement, 20);
        Bob.BeginAnimation(TranslateTransform.YProperty, movement);
        if (pose is MascotPose.Walk or MascotPose.Lifted)
        {
            var tilt = new DoubleAnimation(-3, 3, TimeSpan.FromSeconds(.35)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(tilt, 20); Tilt.BeginAnimation(RotateTransform.AngleProperty, tilt);
        }
    }
}
