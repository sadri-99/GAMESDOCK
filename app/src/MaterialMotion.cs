using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GamesDock
{
    public sealed partial class MaterialArt
    {
        public static readonly DependencyProperty AmbientPhaseProperty = MotionProperty("AmbientPhase", 0);
        public static readonly DependencyProperty InteractionLevelProperty = MotionProperty("InteractionLevel", 0);
        public static readonly DependencyProperty EventPhaseProperty = MotionProperty("EventPhase", -1);
        static readonly Brush Radar = Themes.Br("#FF63C58E"), Ember = Themes.Br("#FFF3A04A");
        static readonly Brush MassField = Themes.Br("#FF70BFF4"), Phosphor = Themes.Br("#FF78DCAD");
        static readonly Brush Head = Themes.Br("#FFE0FFE8");
        bool _interacting, _launchEvent, _motionEnabled;
        double _eventPosition;
        int _interactionGeneration, _eventGeneration;

        // Previews and settings thumbnails stay static unless explicitly enabled by the dock.
        public bool MotionEnabled
        {
            get { return _motionEnabled; }
            set
            {
                if (_motionEnabled == value) return;
                _motionEnabled = value;
                if (!value) StopMotion();
                else if (CanAnimate) TriggerEvent(_length / 2, false);
            }
        }
        public double AmbientPhase { get { return (double)GetValue(AmbientPhaseProperty); } set { SetValue(AmbientPhaseProperty, value); } }
        public double InteractionLevel { get { return (double)GetValue(InteractionLevelProperty); } set { SetValue(InteractionLevelProperty, value); } }
        public double EventPhase { get { return (double)GetValue(EventPhaseProperty); } set { SetValue(EventPhaseProperty, value); } }

        public MaterialArt()
        {
            Overlay = new MaterialFocus(this) { IsHitTestVisible = false };
            Loaded += delegate { if (CanAnimate && EventPhase < 0) TriggerEvent(_length / 2, false); };
            Unloaded += delegate { StopMotion(); };
            IsVisibleChanged += delegate { if (!IsVisible) StopMotion(); };
        }

        static DependencyProperty MotionProperty(string name, double value)
        {
            return DependencyProperty.Register(name, typeof(double), typeof(MaterialArt),
                new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender,
                    (owner, args) => { var art = (MaterialArt)owner; if (art.Overlay != null) art.Overlay.InvalidateVisual(); }));
        }

        bool CanAnimate
        {
            get { return MotionEnabled && IsVisible && _theme != null && _theme.Material != null; }
        }

        static DoubleAnimation Clock(double from, double to, double seconds)
        {
            var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds)) { FillBehavior = FillBehavior.Stop };
            Timeline.SetDesiredFrameRate(animation, 60);
            return animation;
        }

        void ResetMotion(bool reveal)
        {
            bool resume = _interacting;
            StopMotion();
            if (reveal && CanAnimate) TriggerEvent(_length / 2, false);
            if (resume) SetInteraction(true);
        }

        public void StopMotion()
        {
            _interacting = false;
            _interactionGeneration++; _eventGeneration++;
            _focusGeneration++;
            BeginAnimation(AmbientPhaseProperty, null);
            BeginAnimation(InteractionLevelProperty, null);
            BeginAnimation(EventPhaseProperty, null);
            BeginAnimation(FocusTransitionProperty, null);
            AmbientPhase = 0; InteractionLevel = 0; EventPhase = -1;
            FocusTransition = 1;
        }

        public void SetInteraction(bool active)
        {
            if (active && !CanAnimate) { StopMotion(); return; }
            if (_interacting == active) return;
            _interacting = active;
            int generation = ++_interactionGeneration;
            if (active)
            {
                var loop = Clock(0, 1, ThemeMotionProfile.For(_theme.Material).Cycle);
                loop.RepeatBehavior = RepeatBehavior.Forever;
                BeginAnimation(AmbientPhaseProperty, loop, HandoffBehavior.SnapshotAndReplace);
                var fade = Clock(InteractionLevel, 1, 0.18);
                InteractionLevel = 1;
                BeginAnimation(InteractionLevelProperty, fade, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                var fade = Clock(InteractionLevel, 0, 0.24);
                InteractionLevel = 0;
                fade.Completed += delegate
                {
                    if (generation != _interactionGeneration || _interacting) return;
                    BeginAnimation(AmbientPhaseProperty, null);
                    BeginAnimation(InteractionLevelProperty, null);
                    AmbientPhase = 0;
                };
                BeginAnimation(InteractionLevelProperty, fade, HandoffBehavior.SnapshotAndReplace);
            }
        }

        public void Launch(double position)
        {
            if (CanAnimate) TriggerEvent(position, true);
        }

        void TriggerEvent(double position, bool launch)
        {
            _eventPosition = position; _launchEvent = launch;
            int generation = ++_eventGeneration;
            double seconds = launch ? ThemeMotionProfile.For(_theme.Material).LaunchTime : (_theme.Material == "cyberware" ? 3.7 : 1.85);
            var sequence = Clock(0, 1, seconds);
            sequence.Completed += delegate
            {
                if (generation != _eventGeneration) return;
                BeginAnimation(EventPhaseProperty, null);
                EventPhase = -1;
            };
            BeginAnimation(EventPhaseProperty, sequence, HandoffBehavior.SnapshotAndReplace);
        }

        static double Unit(double n) { return n - Math.Floor(n); }
        static double Clamp(double n) { return Math.Max(0, Math.Min(1, n)); }
        static double Envelope(double progress) { return Clamp(progress * 9) * Clamp((1 - progress) * 4); }
        static double Between(double a, double b, double amount) { return a + (b - a) * amount; }

        void DrawSagaMotion(DrawingContext dc, double start, double end, double top, double bottom)
        {
            double eventProgress = EventPhase;
            double activity = Clamp(InteractionLevel);
            if (activity < 0.001 && eventProgress < 0) return;
            double phase = eventProgress >= 0 ? Clamp(eventProgress) : AmbientPhase;
            double energy = eventProgress >= 0 ? Envelope(phase) : activity * .82;
            if (energy < 0.001) return;
            double position = _marker >= 0 ? _marker : _length / 2;
            if (eventProgress >= 0) position = _eventPosition;
            position = Math.Max(start, Math.Min(end, position));
            dc.PushOpacity(energy);
            if (Themes.SagaHint(_theme.Material) == null)
                DrawLegacyMotion(dc, start, end, top, bottom, position, phase, eventProgress >= 0 && _launchEvent);
            else DrawNewSagaMotion(dc, start, end, top, bottom, position, phase, eventProgress >= 0 && _launchEvent);
            dc.Pop();
        }

        static void Arc(DrawingContext dc, Pen pen, double x, double y, double rx, double ry,
                        double startAngle, double sweep)
        {
            if (rx <= 0 || ry <= 0) return;
            var path = new StreamGeometry();
            using (var ctx = path.Open())
            {
                ctx.BeginFigure(new Point(x + rx * Math.Cos(startAngle), y + ry * Math.Sin(startAngle)), false, false);
                ctx.ArcTo(new Point(x + rx * Math.Cos(startAngle + sweep), y + ry * Math.Sin(startAngle + sweep)),
                    new Size(rx, ry), 0, sweep > Math.PI, SweepDirection.Clockwise, true, false);
            }
            path.Freeze(); dc.DrawGeometry(null, pen, path);
        }
    }
}
