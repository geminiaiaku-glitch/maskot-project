namespace Maskot.Core;

// All desktop geometry is in physical pixels, including monitors with negative origins.
public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);
    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);
    public double Length => Math.Sqrt(X * X + Y * Y);
}

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public PointD Center => new(X + Width / 2, Y + Height / 2);
    public bool Contains(PointD p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;
    public PointD Clamp(PointD p, double width, double height) =>
        new(Math.Clamp(p.X, X, Math.Max(X, Right - width)),
            Math.Clamp(p.Y, Y, Math.Max(Y, Bottom - height)));
}

public sealed record DisplayArea(string Id, RectD Bounds, RectD WorkArea, double Scale, double WalkingBottom)
{
    public RectD DockTarget => new(Bounds.Center.X - 150 * Scale, Bounds.Y, 300 * Scale, 100 * Scale);
    public RectD DockHint => new(Bounds.Center.X - 90 * Scale, Bounds.Y + 8 * Scale, 180 * Scale, 32 * Scale);
}

public static class DesktopGeometry
{
    public static DisplayArea Nearest(IReadOnlyList<DisplayArea> displays, PointD point) =>
        displays.MinBy(d => DistanceSquared(d.Bounds, point))
        ?? throw new ArgumentException("At least one monitor is required.", nameof(displays));

    private static double DistanceSquared(RectD r, PointD p)
    {
        var dx = Math.Max(Math.Max(r.X - p.X, 0), p.X - r.Right);
        var dy = Math.Max(Math.Max(r.Y - p.Y, 0), p.Y - r.Bottom);
        return dx * dx + dy * dy;
    }

    public static PointD PlacePanel(RectD body, RectD work, double width, double height, double gap = 12)
    {
        var y = body.Bottom + gap;
        if (y + height > work.Bottom) y = body.Y - height - gap;
        return work.Clamp(new(body.Center.X - width / 2, y), width, height);
    }
}
