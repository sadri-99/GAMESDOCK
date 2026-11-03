using System;

namespace GamesDock
{
    // Geometry, focus response and timing share a profile; the clearance model uses the same peak/lift.
    public sealed class ThemeMotionProfile
    {
        public readonly double Peak, Radius, Locality, Lift, TileK, Damping, PointerK, ShellK, Cycle, LaunchTime;
        public readonly bool Classic;
        ThemeMotionProfile(bool classic, double peak, double radius, double locality, double lift,
                           double tileK, double damping, double pointerK, double shellK, double cycle, double launchTime)
        {
            Classic = classic; Peak = peak; Radius = radius; Locality = locality; Lift = lift;
            TileK = tileK; Damping = damping; PointerK = pointerK; ShellK = shellK; Cycle = cycle; LaunchTime = launchTime;
        }
        static readonly ThemeMotionProfile Original = new ThemeMotionProfile(true, 1.55, 2.4, .55, 0, 900, .85, 3200, 340, 6.4, 1.55);
        static readonly ThemeMotionProfile Industrial = new ThemeMotionProfile(false, 1.24, 1.2, 0, 1, 1500, 1.04, 4200, 650, 4.8, 1.25);
        static readonly ThemeMotionProfile Shrine = new ThemeMotionProfile(false, 1.40, 1.65, .55, 4, 850, .98, 2800, 400, 6.0, 1.65);
        static readonly ThemeMotionProfile Portal = new ThemeMotionProfile(false, 1.36, 1.8, 0, 2.5, 1250, .96, 3800, 600, 4.2, 1.5);
        static readonly ThemeMotionProfile Memory = new ThemeMotionProfile(false, 1.22, 1.05, 0, .5, 1800, 1.08, 5000, 800, 3.8, 1.15);
        static readonly ThemeMotionProfile Orbit = new ThemeMotionProfile(false, 1.46, 2.1, .70, 5, 800, 1.02, 2400, 380, 7.2, 1.7);
        static readonly ThemeMotionProfile Terminal = new ThemeMotionProfile(false, 1.28, 1.15, 0, 0, 1700, 1.06, 4800, 780, 4.6, .95);
        public static ThemeMotionProfile For(string material)
        {
            switch (material)
            {
                case "industrial": return Industrial;
                case "shrine": return Shrine;
                case "portal": return Portal;
                case "memory": return Memory;
                case "orbit": return Orbit;
                case "terminal": return Terminal;
                default: return Original;
            }
        }
        public void Tune(Spring spring, double stiffness, double damping)
        {
            spring.K = stiffness; spring.C = 2 * Math.Sqrt(stiffness) * damping;
        }
        public double Influence(double distance, double radius)
        {
            double a = Math.Abs(distance) / radius;
            if (a >= 1) return 0;
            if (Classic) { double c = Math.Cos(a * Math.PI / 2); return c * c; }
            double smooth = 1 - a * a * a * (10 + a * (-15 + 6 * a));
            return Math.Pow(Math.Max(0, smooth), Radius < 1.3 ? 1.7 : 1.05);
        }
    }
}
