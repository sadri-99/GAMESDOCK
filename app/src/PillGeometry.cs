using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    // El contorno de la pildora se rehace en cada cuadro de la animacion, asi que va compilado:
    // muestreado y montado en interprete costaba ~17 ms por cuadro (el 90 % del presupuesto).
    public static class PillGeometry
    {
        const double PI = Math.PI;
        [ThreadStatic] static Workspace _workspace;
        sealed class Workspace
        {
            public readonly List<double> U = new List<double>(96), H = new List<double>(96),
                ContourU = new List<double>(192), ContourV = new List<double>(192);
            public readonly List<Point> Beziers = new List<Point>(576);
            public Point[] Points = new Point[192];
            public double[] Dx = new double[192], Dy = new double[192], Length = new double[192],
                Tx = new double[192], Ty = new double[192];
            public void Reset()
            {
                U.Clear(); H.Clear(); ContourU.Clear(); ContourV.Clear(); Beziers.Clear();
            }
            public void Reserve(int count)
            {
                if (Dx.Length >= count) return;
                int capacity = Math.Max(count, Dx.Length * 2);
                Dx = new double[capacity]; Dy = new double[capacity]; Length = new double[capacity];
                Tx = new double[capacity]; Ty = new double[capacity];
                Points = new Point[capacity];
            }
        }

        static double HalfAt(double m, double baseH, double uni, double amp, double ptr, double r, double peak)
        {
            if (amp <= 0.001) return baseH + uni;
            if (Math.Abs(m - ptr) >= r) return baseH + uni;
            double a = Math.Abs(ptr - m) / r;
            if (a >= 1.0) return baseH + uni;
            double c = Math.Cos(a * PI / 2.0);
            double s = 1.0 + (peak - 1.0) * c * c;
            return baseH + uni + amp * ((s - 1.0) / (peak - 1.0));
        }

        static void Add(List<double> ru, List<double> rv, double u, double v)
        {
            if (ru.Count == 0 || Math.Abs(ru[ru.Count - 1] - u) > 0.4 || Math.Abs(rv[rv.Count - 1] - v) > 0.4)
            {
                ru.Add(u);
                rv.Add(v);
            }
        }

        // spline cerrada de Catmull-Rom convertida a cubicas, con las tangentes ponderadas por longitud
        // de segmento para que el bulto del magnes no estire las tapas
        static void Spline(StreamGeometryContext ctx, Point[] p, int n, Workspace workspace)
        {
            if (n < 3) return;
            workspace.Reserve(n);
            double[] dx = workspace.Dx, dy = workspace.Dy, dl = workspace.Length;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1;
                if (j >= n) j = 0;
                dx[i] = p[j].X - p[i].X;
                dy[i] = p[j].Y - p[i].Y;
                double l = Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]);
                dl[i] = l < 1e-3 ? 1e-3 : l;
            }
            double[] tx = workspace.Tx, ty = workspace.Ty;
            for (int i = 0; i < n; i++)
            {
                int im = i - 1;
                if (im < 0) im = n - 1;
                double h0 = dl[im], h1 = dl[i];
                tx[i] = ((dx[im] / h0) * h1 + (dx[i] / h1) * h0) / (h0 + h1);
                ty[i] = ((dy[im] / h0) * h1 + (dy[i] / h1) * h0) / (h0 + h1);
            }
            ctx.BeginFigure(p[0], true, true);
            List<Point> bez = workspace.Beziers;
            for (int i = 0; i < n; i++)
            {
                int ip = i + 1;
                if (ip >= n) ip = 0;
                bez.Add(new Point(p[i].X + tx[i] / 3.0, p[i].Y + ty[i] / 3.0));
                bez.Add(new Point(p[ip].X - tx[ip] / 3.0, p[ip].Y - ty[ip] / 3.0));
                bez.Add(new Point(p[ip].X, p[ip].Y));
            }
            ctx.PolyBezierTo(bez, true, true);
        }

        // overL/overR sacan las tapas hacia fuera, dentro de la banda transparente de la ventana: el
        // icono de un extremo, agrandado, pisa el ultimo tramo de la tapa y ninguna curva que cierre en
        // punta lo puede cubrir. Cada lado sale lo que entra su propio icono, para no dejar vidrio de
        // sobra en el otro. Nada de esto toca la medida de reposo (over=0 => la pildora de siempre).
        public static Geometry Build(double len, double cc, double baseH, double uni, double amp,
                                     double ptr, double pitch, double peak, bool vert,
                                     double overL, double overR, double cornerRatio = 1.0, double radiusMultiplier = 2.4)
        {
            double L = len + overL + overR;
            if (cornerRatio < .999 && amp <= .001)
            {
                double half = baseH + uni;
                var rectangle = vert ? new Rect(cc - half, -overL, half * 2, L)
                    : new Rect(-overL, cc - half, L, half * 2);
                var rounded = new RectangleGeometry(rectangle, baseH * cornerRatio, baseH * cornerRatio);
                rounded.Freeze();
                return rounded;
            }
            double p0 = ptr + overL;
            double r = pitch * radiusMultiplier;
            double hL = HalfAt(0.0, baseH, uni, amp, p0, r, peak);
            double hR = HalfAt(L, baseH, uni, amp, p0, r, peak);
            // Las tapas son semielipses de radio horizontal anclado al grosor de reposo y radio vertical
            // el grosor real. Con semicirculo puro (radio = grosor actual) la tapa se abre mas de lo que
            // sube su borde, y el icono del extremo la muerde por la esquina.
            double eL = Math.Min(hL, baseH) * cornerRatio, eR = Math.Min(hR, baseH) * cornerRatio;
            double tL = HalfAt(eL, baseH, uni, amp, p0, r, peak);
            double tR = HalfAt(L - eR, baseH, uni, amp, p0, r, peak);
            double a0 = Math.Max(eL, p0 - r);
            double a1 = Math.Min(L - eR, p0 + r);
            if (amp <= 0.001) { a0 = eL; a1 = eL; }
            double paso = Math.Max(6.0, r / 16.0);

            var workspace = _workspace ?? (_workspace = new Workspace());
            workspace.Reset();
            List<double> us = workspace.U, hs = workspace.H;
            us.Add(eL);
            hs.Add(tL);
            double m = a0;
            while (m < a1)
            {
                if (m > us[us.Count - 1] + 0.5)
                {
                    us.Add(m);
                    hs.Add(HalfAt(m, baseH, uni, amp, p0, r, peak));
                }
                m += paso;
            }
            if (a1 > us[us.Count - 1] + 0.5)
            {
                us.Add(a1);
                hs.Add(HalfAt(a1, baseH, uni, amp, p0, r, peak));
            }
            double top = L - eR;
            if (top > us[us.Count - 1] + 0.5)
            {
                us.Add(top);
                hs.Add(tR);
            }

            List<double> ru = workspace.ContourU, rv = workspace.ContourV;
            for (int i = 0; i < us.Count; i++) Add(ru, rv, us[i], cc - hs[i]);
            for (int k = 1; k <= 13; k++)
            {
                double f = PI * (k / 13.0);
                double v = cornerRatio >= 0.999 ? cc - tR * Math.Cos(f)
                    : (k <= 6 ? cc - tR + eR * (1 - Math.Cos(f))
                              : cc + tR - eR * (1 + Math.Cos(f)));
                Add(ru, rv, top + eR * Math.Sin(f), v);
            }
            for (int i = us.Count - 1; i >= 0; i--) Add(ru, rv, us[i], cc + hs[i]);
            for (int k = 1; k <= 13; k++)
            {
                double f = PI * (k / 13.0);
                double v = cornerRatio >= 0.999 ? cc + tL * Math.Cos(f)
                    : (k <= 6 ? cc + tL - eL * (1 - Math.Cos(f))
                              : cc - tL + eL * (1 + Math.Cos(f)));
                Add(ru, rv, eL - eL * Math.Sin(f), v);
            }

            workspace.Reserve(ru.Count);
            Point[] pts = workspace.Points;
            for (int i = 0; i < ru.Count; i++)
            {
                double u = ru[i] - overL;
                pts[i] = vert ? new Point(rv[i], u) : new Point(u, rv[i]);
            }

            StreamGeometry geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open()) Spline(ctx, pts, ru.Count, workspace);
            geo.Freeze();
            return geo;
        }
    }
}
