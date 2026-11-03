using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GamesDock
{
    // Animate the artwork, leaving the button's magnification spring, drag opacity and hit area intact.
    public static class SagaTileMotion
    {
        sealed class Pose
        {
            public FrameworkElement Art;
            public readonly ScaleTransform HoverScale = new ScaleTransform(1, 1), LaunchScale = new ScaleTransform(1, 1);
            public readonly RotateTransform HoverRotate = new RotateTransform();
            public readonly TranslateTransform HoverMove = new TranslateTransform(), LaunchMove = new TranslateTransform();
        }
        static readonly DependencyProperty PoseProperty = DependencyProperty.RegisterAttached("Pose", typeof(Pose), typeof(SagaTileMotion));
        static Pose State(Button tile)
        {
            var art = tile.Content as FrameworkElement;
            if (art == null) return null;
            var pose = (Pose)tile.GetValue(PoseProperty);
            if (pose != null && pose.Art == art) return pose;
            pose = new Pose { Art = art };
            var transforms = new TransformGroup();
            transforms.Children.Add(pose.HoverScale); transforms.Children.Add(pose.HoverRotate);
            transforms.Children.Add(pose.HoverMove); transforms.Children.Add(pose.LaunchScale); transforms.Children.Add(pose.LaunchMove);
            art.RenderTransformOrigin = new Point(.5, .5); art.RenderTransform = transforms;
            tile.SetValue(PoseProperty, pose);
            return pose;
        }

        public static void Hover(Button tile, string material, double amount, double side, bool vertical, double outward, double iconSize)
        {
            var pose = (Pose)tile.GetValue(PoseProperty);
            if (material == null && pose == null) return;
            pose = State(tile); if (pose == null) return;
            double a = Math.Max(0, Math.Min(1, amount));
            double lift = ThemeMotionProfile.For(material).Lift * Math.Min(1, iconSize / 64.0) * a;
            pose.HoverScale.ScaleX = 1; pose.HoverScale.ScaleY = 1; pose.HoverRotate.Angle = 0;
            switch (material)
            {
                case "industrial": pose.HoverScale.ScaleX = 1 + .008 * a; pose.HoverScale.ScaleY = 1 - .006 * a; break;
                case "portal": pose.HoverScale.ScaleX = 1 - .018 * a; pose.HoverScale.ScaleY = 1 + .012 * a; break;
                case "orbit":
                    pose.HoverScale.ScaleX = 1 + .02 * a; pose.HoverScale.ScaleY = 1 - .012 * a;
                    pose.HoverRotate.Angle = Math.Max(-1, Math.Min(1, side)) * 1.2 * a;
                    break;
            }
            pose.HoverMove.X = vertical ? -outward * lift : 0;
            pose.HoverMove.Y = vertical ? 0 : -outward * lift;
        }

        public static void Launch(Button tile, string material, bool vertical, double outward = 1)
        {
            var art = tile.Content as FrameworkElement;
            if (art == null || material == null) return;
            var pose = State(tile);
            StopLaunch(pose);
            var scale = pose.LaunchScale;
            var move = pose.LaunchMove;
            double duration = ThemeMotionProfile.For(material).LaunchTime;
            art.BeginAnimation(UIElement.OpacityProperty, null);
            art.Opacity = 1;
            switch (material)
            {
                case "industrial":
                    Animate(scale, ScaleTransform.ScaleXProperty, duration, false, 1, 0.985, 1.012, 1);
                    Animate(scale, ScaleTransform.ScaleYProperty, duration, false, 1, 0.985, 1.012, 1);
                    break;
                case "shrine":
                    Animate(scale, ScaleTransform.ScaleXProperty, duration, false, 1, 0.96, 1.035, 1);
                    Animate(scale, ScaleTransform.ScaleYProperty, duration, false, 1, 0.96, 1.035, 1);
                    Animate(move, vertical ? TranslateTransform.XProperty : TranslateTransform.YProperty,
                        duration, false, 0, -outward, -3 * outward, 0);
                    break;
                case "portal":
                    Animate(scale, ScaleTransform.ScaleXProperty, duration, false, 1, 0.84, 0.035, 0.035, 1.06, 1);
                    Animate(scale, ScaleTransform.ScaleYProperty, duration, false, 1, 1.08, 0.7, 0.7, 1.02, 1);
                    Animate(art, UIElement.OpacityProperty, duration, false, 1, 1, 0, 0, 1, 1);
                    break;
                case "memory":
                    Animate(scale, ScaleTransform.ScaleXProperty, duration, true, 1, 0.97, 0.97, 1, 1.015, 1);
                    Animate(scale, ScaleTransform.ScaleYProperty, duration, true, 1, 0.97, 0.97, 1, 1.015, 1);
                    break;
                case "orbit":
                    Animate(scale, ScaleTransform.ScaleXProperty, duration, false, 1, 0.86, 1.05, 1);
                    Animate(scale, ScaleTransform.ScaleYProperty, duration, false, 1, 1.08, 0.98, 1);
                    Animate(move, vertical ? TranslateTransform.XProperty : TranslateTransform.YProperty,
                        duration, false, 0, -4 * outward, -outward, 0);
                    break;
                case "terminal":
                    Animate(move, vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty,
                        0.32, true, 0, -1.5, 1, -0.5, 0, 0);
                    Animate(art, UIElement.OpacityProperty, 0.32, true, 1, 0.72, 1, 0.88, 1, 1);
                    break;
            }
        }

        public static void Stop(Button tile)
        {
            var pose = (Pose)tile.GetValue(PoseProperty);
            if (pose == null) return;
            StopLaunch(pose);
            pose.HoverScale.ScaleX = pose.HoverScale.ScaleY = 1;
            pose.HoverRotate.Angle = 0; pose.HoverMove.X = pose.HoverMove.Y = 0;
        }

        static void StopLaunch(Pose pose)
        {
            pose.LaunchScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            pose.LaunchScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            pose.LaunchMove.BeginAnimation(TranslateTransform.XProperty, null);
            pose.LaunchMove.BeginAnimation(TranslateTransform.YProperty, null);
            pose.Art.BeginAnimation(UIElement.OpacityProperty, null);
            pose.Art.Opacity = 1;
        }

        static void Animate(Animatable target, DependencyProperty property, double seconds, bool discrete, params double[] values)
        {
            var animation = Frames(seconds, discrete, values);
            animation.Completed += delegate { target.BeginAnimation(property, null); };
            target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }

        static void Animate(UIElement target, DependencyProperty property, double seconds, bool discrete, params double[] values)
        {
            var animation = Frames(seconds, discrete, values);
            animation.Completed += delegate { target.BeginAnimation(property, null); };
            target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }

        static DoubleAnimationUsingKeyFrames Frames(double seconds, bool discrete, double[] values)
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(seconds), FillBehavior = FillBehavior.Stop };
            Timeline.SetDesiredFrameRate(animation, 60);
            for (int i = 0; i < values.Length; i++)
            {
                var time = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds * i / (values.Length - 1)));
                if (discrete) animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(values[i], time));
                else animation.KeyFrames.Add(new SplineDoubleKeyFrame(values[i], time, new KeySpline(0.25, 0.1, 0.25, 1)));
            }
            return animation;
        }
    }
}
