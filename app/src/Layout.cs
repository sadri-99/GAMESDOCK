using System;

namespace GamesDock
{
    // Toda la medida de la pildora sale de aqui: ancho, alto, radios y posiciones de la fila.
    public class Layout
    {
        public const double Margin = 34.0;    // banda transparente que rodea la pildora
        public const double MagPeak = 1.55;   // cuanto estira el magnes al icono del centro de la fila

        public readonly Config Cfg;

        public Layout(Config cfg) { Cfg = cfg; }

        public bool Vert { get { return Cfg.Position == "left" || Cfg.Position == "right"; } }
        public int IconSize { get { return Cfg.IconSize; } }

        public double PadV { get { return Math.Max(2, Cfg.PillPad); } }
        public double PadH { get { return PadV + 6; } }

        // el hueco del tile tiene dos medidas: a lo largo de la fila separa iconos y da sitio al halo,
        // y en transversal es lo que manda en el grosor de la pildora, asi que va apretado
        public double RingMain { get { return Math.Max(7, (int)(IconSize * 0.18)); } }
        public double RingCross { get { return Math.Max(4, (int)(IconSize * 0.09)); } }
        public double TileBox { get { return IconSize + 2 * RingCross; } }
        public double Pitch { get { return IconSize + 2 * RingMain; } }

        // separacion de la fila respecto al borde de la pildora a lo largo de la fila: es lo que evita
        // que el primer icono se salga por la izquierda y define el ancho junto con la cantidad de
        // iconos. El colchon extra es el "espacio vacio" donde dar click derecho para abrir el menu.
        public double RowInset { get { return PadH + 10; } }

        // medio grosor de la pildora en reposo y con el raton encima
        public double HalfIdle { get { return (TileBox + 2 * PadV) / 2.0; } }

        // el maximo deja el tile entero agrandado (con su halo) dentro del cristal, con un dedo de
        // aire para que el borde animado nunca le muerda la punta
        public double HalfMax
        {
            get
            {
                double need = TileBox * MagPeak / 2.0 + PadV + 4.0;
                return Math.Max(need, HalfIdle + 7.0);
            }
        }

        // cuanto desborda la fila por el eje transversal: la punta del icono en su maximo agrandado,
        // contada desde el eje EN REPOSO, mas lo que la fila entera se desplaza hacia dentro cuando la
        // pildora se hincha (RowShift). Sin esa segunda cuenta el ScrollViewer recorta la punta de
        // arriba justo cuando el magnes esta a tope, que es cuando mas se nota.
        public double MagOverrun
        {
            get
            {
                if (!Cfg.Magnify) return 6.0;
                double delta = HalfMax - HalfIdle;
                double need = TileBox * MagPeak / 2.0 - HalfIdle + 4.0 + delta * 0.45;
                return Math.Max(6.0, Math.Min(need, Margin - 2));
            }
        }

        public double MagScale(double d, double r)
        {
            if (r <= 0) return 1.0;
            double a = Math.Abs(d) / r;
            if (a >= 1.0) return 1.0;
            double c = Math.Cos(a * Math.PI / 2.0);
            return 1.0 + (MagPeak - 1.0) * c * c;
        }

        public double MagRadius { get { return Pitch * 2.4; } }

        // +1 si la pildora crece hacia arriba de la pantalla (abajo/derecha del work area)
        public double Outward
        {
            get
            {
                switch (Cfg.Position)
                {
                    case "top": return -1.0;
                    case "left": return -1.0;
                    default: return 1.0;
                }
            }
        }
    }
}
