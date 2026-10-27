namespace Maskot.Core;

public readonly record struct MascotFrame(double Bob, double Lean, double HeadAngle,
    double LeftFoot, double RightFoot, double LeftArm, double RightArm,
    double TailAngle, double EyeScale, double Breathe, double SleepOpacity, double MouthOpen = 0);

/// <summary>Samples articulated animation independently from walking and panel state.</summary>
public static class MascotAnimation
{
    public static MascotFrame Sample(MascotPose pose, double seconds, bool enabled = true, KaraokeMood performance = KaraokeMood.None)
    {
        if (pose == MascotPose.Lifted) performance = KaraokeMood.None;
        if (!enabled) return new(0, 0, performance == KaraokeMood.Confused ? 12 : 0, 0, 0,
            performance == KaraokeMood.Confused ? -16 : 0, performance == KaraokeMood.None ? 0 : -28, 0, 1, 1, 0);
        seconds = double.IsFinite(seconds) ? Math.Max(0, seconds) : 0;
        var breath = Math.Sin(seconds * Math.Tau / 3.2);
        var stride = Math.Sin(seconds * Math.Tau / .72);
        var blinkPhase = seconds % 4.1;
        var eyes = blinkPhase < .14 ? .08 + .92 * Math.Abs(blinkPhase - .07) / .07 : 1;
        var tail = Math.Sin(seconds * Math.Tau / 2.4) * 15;
        var effectivePose = performance != KaraokeMood.None && pose is MascotPose.Sleep or MascotPose.Sit ? MascotPose.Idle : pose;
        MascotFrame frame = effectivePose switch
        {
            MascotPose.Walk => new(-2 - Math.Abs(stride) * 4.5, stride * 2.5, -stride * 2,
                stride * 24, -stride * 24, -stride * 13, stride * 13, tail, eyes, 1, 0),
            MascotPose.Lifted => new(-3 + breath * 2, Math.Sin(seconds * 4) * 5, -3,
                15 + stride * 5, -15 - stride * 5, -32, 32, tail * 1.4, eyes, 1, 0),
            MascotPose.Sleep => new(5 + breath * 1.6, 0, 8, -13, 13, 5, -5,
                tail * .25, .06, 1 + breath * .025, .65 + breath * .35),
            MascotPose.Sit => new(5 + breath * 2, 0, breath * 3, -22, 22, 8, -8,
                tail * .65, eyes, 1 + breath * .018, 0),
            MascotPose.Greet => new(-2 + breath * 2.2, 0, Math.Sin(seconds * 2) * 7,
                0, 0, 0, -35 + Math.Sin(seconds * 10) * 18, tail, eyes, 1 + breath * .02, 0),
            _ => new(breath * 2.8, 0, Math.Sin(seconds * 1.2) * 3, 0, 0,
                breath * 3, -breath * 3, tail, eyes, 1 + breath * .025, 0)
        };
        return performance switch
        {
            KaraokeMood.Singing => frame with { HeadAngle = frame.HeadAngle + Math.Sin(seconds * 5) * 4,
                RightArm = -28 + Math.Sin(seconds * 4) * 4, MouthOpen = .25 + .75 * Math.Abs(Math.Sin(seconds * 8)), SleepOpacity = 0 },
            KaraokeMood.Confused => frame with { HeadAngle = 13 + Math.Sin(seconds * 1.8) * 7,
                LeftArm = -16 + Math.Sin(seconds * 2.4) * 8, RightArm = 8 + Math.Sin(seconds * 2.7) * 8, SleepOpacity = 0 },
            KaraokeMood.Listening => frame with { HeadAngle = 3 + Math.Sin(seconds * 1.8) * 3, RightArm = -24, SleepOpacity = 0 },
            _ => frame
        };
    }
}
