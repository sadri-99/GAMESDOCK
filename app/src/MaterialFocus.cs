using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GamesDock
{
    public sealed class MaterialFocus : FrameworkElement
    {
        readonly MaterialArt _owner;
        public MaterialFocus(MaterialArt owner) { _owner = owner; }
        protected override void OnRender(DrawingContext dc) { _owner.RenderFocus(dc); }
    }

    public sealed partial class MaterialArt
    {
        public static readonly DependencyProperty FocusTransitionProperty = MotionProperty("FocusTransition", 1);
        public FrameworkElement Overlay { get; private set; }
        public double FocusTransition { get { return (double)GetValue(FocusTransitionProperty); } set { SetValue(FocusTransitionProperty, value); } }
        Rect _focusRect = Rect.Empty, _oldFocus = Rect.Empty;
        double _focusRadius, _focusAmount;
        bool _focusVisible;
        int _focusGeneration;

        public void SetFocus(Rect rectangle, double radius, double amount)
        {
            _focusVisible = !rectangle.IsEmpty;
            if (!rectangle.IsEmpty)
            {
                if (_vertical) rectangle = new Rect(rectangle.Y, rectangle.X, rectangle.Height, rectangle.Width);
                if (!_focusRect.IsEmpty && Math.Abs(rectangle.X - _focusRect.X) > Math.Max(12, _focusRect.Width * .6) && CanAnimate)
                {
                    _oldFocus = CurrentFocus();
                    int generation = ++_focusGeneration;
                    var transition = Clock(0, 1, _theme.Material == "orbit" ? .22 : .14);
                    transition.Completed += delegate { if (generation == _focusGeneration) BeginAnimation(FocusTransitionProperty, null); };
                    BeginAnimation(FocusTransitionProperty, transition, HandoffBehavior.SnapshotAndReplace);
                }
                _focusRect = rectangle; _focusRadius = radius; _focusAmount = amount;
            }
            Overlay.InvalidateVisual();
        }

        Rect CurrentFocus()
        {
            if (_oldFocus.IsEmpty || FocusTransition >= .999) return _focusRect;
            double t = Clamp(FocusTransition); t = t * t * (3 - 2 * t);
            return new Rect(Between(_oldFocus.X, _focusRect.X, t), Between(_oldFocus.Y, _focusRect.Y, t),
                Between(_oldFocus.Width, _focusRect.Width, t), Between(_oldFocus.Height, _focusRect.Height, t));
        }

        internal void RenderFocus(DrawingContext dc)
        {
            if (_theme == null || _theme.Material == null || _clip == null || !MotionEnabled) return;
            double activity = Clamp(InteractionLevel);
            double eventEnergy = EventPhase < 0 ? 0 : Envelope(EventPhase);
            if ((!_focusVisible && activity < .001 && eventEnergy < .001) || _focusRect.IsEmpty) return;
            double strength = Math.Max(activity * .72, eventEnergy);
            strength *= .8 + .2 * Clamp(_focusAmount);
            if (strength < .001) return;
            Rect rect = CurrentFocus(); rect.Inflate(3, 3);
            double cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
            double phase = EventPhase >= 0 ? Clamp(EventPhase) : AmbientPhase;
            double radius = Math.Min(Math.Min(rect.Width, rect.Height) / 2, _focusRadius + 3);
            dc.PushClip(_clip);
            if (_vertical) dc.PushTransform(new MatrixTransform(new Matrix(0, 1, 1, 0, 0, 0)));
            dc.PushOpacity(strength);
            switch (_theme.Material)
            {
                case "industrial":
                    Brackets(dc, rect, Stroke(Radar, 1), 8);
                    dc.PushOpacity(.12);
                    double scan = rect.Top + rect.Height * Unit(phase * 2);
                    Line(dc, Stroke(Radar, 1), rect.Left + 2, scan, rect.Right - 2, scan);
                    dc.Pop();
                    for (int i = 0; i < 4; i++)
                        dc.DrawRectangle(i <= (int)(phase * 5) % 4 ? Radar : _dim, null, new Rect(rect.Right - 12 + i * 3, rect.Bottom + 1, 2, 1));
                    break;
                case "shrine":
                    dc.PushOpacity(.35 + .15 * Math.Sin(phase * Math.PI * 4));
                    dc.DrawEllipse(_focusGlow, null, new Point(cx, cy), rect.Width * .75, rect.Height * .75); dc.Pop();
                    Arc(dc, Stroke(_accent, 1), cx, cy, rect.Width * .54, rect.Height * .55, -.8 + phase * Math.PI * 2, 1.65);
                    Arc(dc, Stroke(Ember, .8), cx, cy, rect.Width * .54, rect.Height * .55, 2.1 + phase * Math.PI * 2, 1.35);
                    for (int i = 0; i < 7; i++)
                    {
                        double age = Unit(phase * 2 + i * .173);
                        dc.PushOpacity((1 - age) * .65);
                        dc.DrawEllipse(Ember, null, new Point(cx + Math.Sin(i * 3 + age * 2) * rect.Width * .53,
                            rect.Bottom - age * rect.Height), .65, 1.1); dc.Pop();
                    }
                    break;
                case "portal":
                    double squeeze = EventPhase >= 0 && _launchEvent ? Math.Max(.06, Math.Abs(Math.Cos(phase * Math.PI))) : 1;
                    double rx = rect.Width * .54 * squeeze, ry = rect.Height * .55;
                    Arc(dc, Stroke(Blue, 1.4), cx, cy, rx, ry, phase * Math.PI * 2, Math.PI * .92);
                    Arc(dc, Stroke(Orange, 1.4), cx, cy, rx, ry, phase * Math.PI * 2 + Math.PI, Math.PI * .92);
                    for (int i = 0; i < 4; i++)
                    {
                        double angle = phase * Math.PI * 2 + i * Math.PI / 2;
                        dc.DrawEllipse(i % 2 == 0 ? Blue : Orange, null,
                            new Point(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle)), 1.1, 1.1);
                    }
                    break;
                case "memory":
                    dc.DrawRoundedRectangle(null, Stroke(_detail, .8), rect, Math.Min(4, radius), Math.Min(4, radius));
                    Brackets(dc, rect, Stroke(_accent, 1.3), 5);
                    int step = (int)(phase * 24);
                    for (int i = 0; i < 3; i++)
                    {
                        double x = rect.Left + 4 + Unit((step - i) / 24.0) * Math.Max(1, rect.Width - 9);
                        dc.PushOpacity(1 - i * .25); dc.DrawRectangle(_accent, null, new Rect(x, rect.Bottom, 3, 2)); dc.Pop();
                    }
                    break;
                case "orbit":
                    dc.PushOpacity(.32); dc.DrawEllipse(_focusGlow, null, new Point(cx, cy), rect.Width * .8, rect.Height * .7); dc.Pop();
                    Arc(dc, Stroke(MassField, 1.1), cx, cy, rect.Width * .61, rect.Height * .55,
                        phase * Math.PI * 2 + .18, Math.PI * .77);
                    Arc(dc, Stroke(_accent, .8), cx, cy, rect.Width * .61, rect.Height * .55,
                        phase * Math.PI * 2 + Math.PI + .18, Math.PI * .77);
                    for (int i = 0; i < 3; i++)
                    {
                        double angle = phase * Math.PI * 2 + i * Math.PI * .66;
                        dc.DrawEllipse(_accent, null, new Point(cx + rect.Width * .61 * Math.Cos(angle),
                            cy + rect.Height * .55 * Math.Sin(angle)), 1.1, .8);
                    }
                    break;
                case "terminal":
                    Brackets(dc, rect, Stroke(Phosphor, 1), 7);
                    double row = rect.Top + Math.Floor(Unit(phase * 2) * rect.Height / 3) * 3;
                    dc.PushOpacity(.15); Line(dc, Stroke(Phosphor, 1), rect.Left + 1, row, rect.Right - 1, row); dc.Pop();
                    for (int i = 0; i < 5; i++)
                    {
                        dc.PushOpacity(i < (int)(phase * 23) % 6 ? .9 : .15);
                        dc.DrawRectangle(Phosphor, null, new Rect(rect.Left + 2 + i * 4, rect.Bottom + 1, 2, 2)); dc.Pop();
                    }
                    break;
            }
            dc.Pop();
            if (_vertical) dc.Pop();
            dc.Pop();
        }

        static void Brackets(DrawingContext dc, Rect rect, Pen pen, double length)
        {
            length = Math.Min(length, Math.Min(rect.Width, rect.Height) / 3);
            Corner(dc, pen, rect.Left, rect.Top, -1, -1, length);
            Corner(dc, pen, rect.Left, rect.Bottom, -1, 1, length);
            Corner(dc, pen, rect.Right, rect.Top, 1, -1, length);
            Corner(dc, pen, rect.Right, rect.Bottom, 1, 1, length);
        }

        static void Corner(DrawingContext dc, Pen pen, double x, double y, int signX, int signY, double length)
        {
            Line(dc, pen, x, y, x - signX * length, y);
            Line(dc, pen, x, y, x, y - signY * length);
        }
    }
}
