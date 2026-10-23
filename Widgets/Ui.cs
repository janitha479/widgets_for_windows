using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WidgetTools;

/// <summary>
/// Shared overlay helpers: a progress-ring arc builder and the ease-out
/// animation used by the collapse/expand of every widget.
/// </summary>
internal static class Ui
{
    /// <summary>Set <paramref name="arc"/> to a circular progress arc starting at 12 o'clock, clockwise.</summary>
    public static void SetArc(Path arc, double cx, double cy, double r, double pct)
    {
        pct = Math.Clamp(pct, 0, 100);
        if (pct <= 0)
        {
            arc.Data = Geometry.Empty;
            return;
        }

        double sweep = pct / 100.0 * 360.0;
        if (sweep >= 359.999) sweep = 359.999;   // a full circle can't be one arc segment

        const double start = -90;                 // top
        var p0 = OnCircle(cx, cy, r, start);
        var p1 = OnCircle(cx, cy, r, start + sweep);

        var fig = new PathFigure { StartPoint = p0, IsClosed = false };
        fig.Segments.Add(new ArcSegment
        {
            Point = p1,
            Size = new Size(r, r),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = sweep > 180,
        });
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        geo.Freeze();
        arc.Data = geo;
    }

    private static Point OnCircle(double cx, double cy, double r, double deg)
    {
        double rad = deg * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    /// <summary>Ease-out animate a double dependency property.</summary>
    public static void Animate(IAnimatable target, DependencyProperty prop, double to, int ms, Action? done = null)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        if (done != null) anim.Completed += (_, _) => done();
        target.BeginAnimation(prop, anim);
    }
}
