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
            bool launchFallback = EventPhase >= 0 && _launchEvent && (!_focusVisible || _focusRect.IsEmpty);
            if ((!_focusVisible && activity < .001 && eventEnergy < .001) || (_focusRect.IsEmpty && !launchFallback)) return;
            double strength = Math.Max(activity * .72, eventEnergy);
            strength *= .8 + .2 * Clamp(_focusAmount);
            if (strength < .001) return;
            double launchSize = Math.Min(80, _half * 1.45);
            Rect rect = launchFallback ? new Rect(_eventPosition - launchSize / 2, _center - launchSize / 2, launchSize, launchSize) : CurrentFocus();
            rect.Inflate(3, 3);
            double cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
            double phase = EventPhase >= 0 ? Clamp(EventPhase) : AmbientPhase;
            double radius = Math.Min(Math.Min(rect.Width, rect.Height) / 2, _focusRadius + 3);
            dc.PushClip(_clip);
            if (_vertical) dc.PushTransform(new MatrixTransform(new Matrix(0, 1, 1, 0, 0, 0)));
            dc.PushOpacity(strength);
            if (Themes.SagaHint(_theme.Material) == null) DrawLegacyFocus(dc, rect, phase, radius);
            else DrawNewSagaFocus(dc, rect, phase, radius);
            if (_launchEvent && EventPhase >= 0) DrawLaunchStory(dc, rect, phase);
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
