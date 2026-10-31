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

        // la holgura de la pildora ya no se ajusta: 4 px era el punto donde la fila se ve asentada sin
        // que el cristal coma halo, y dejarla fija quita un ajuste cuyo efecto casi nadie veia
        public const double PadFix = 4.0;

        public double PadV { get { return PadFix; } }
        public double PadH { get { return PadV + 6; } }

        // Las tres formas de Ajustes son solo el recorte con el que se pinta el hueco del icono: el png
        // no se toca. "defecto" es lo que la barra ha hecho siempre.
        public double TileRadius(double lado)
        {
            switch (Cfg.IconShape)
            {
                case "ios": return Math.Max(6.0, lado * 0.22);
                case "esfera": return 999.0;
                default: return 0.0;
            }
        }

        // la letra de respaldo: en "defecto" va en circulo, que es como se ha visto siempre
        public double BadgeRadius(double lado)
        {
            double r = TileRadius(lado);
            return r <= 0 ? 999.0 : r;
        }

        // la carpeta ya venia con la esquina suavizada, asi que en "defecto" la conserva
        public double FolderRadius(double lado)
        {
            double r = TileRadius(lado);
            return r <= 0 ? Math.Max(6.0, lado * 0.22) : r;
        }

        // El mosaico se redondea celda a celda. WPF recorta el fondo de un Border a su CornerRadius,
        // pero al hijo no lo recorta: si el radio solo va en el borde de fuera, las imagenes del grupo
        // salen cuadradas por encima de la forma. Y un Clip geometrico en el contenedor manda el tile
        // a la via lenta justo cuando el magnes lo esta moviendo cuadro a cuadro, asi que se recorta
        // desde dentro: cada celda de una esquina lleva el radio en SU esquina de fuera.
        //   esfera: el radio de medio tile puesto en la esquina de la celda sale justo el arco del
        //           circulo, y las cuatro esquineras cierran la esfera entera.
        //   ios y defecto: la misma curva del contenedor, para que la carpeta no se lea un cuadrado
        //           metido dentro de un redondeado.
        public double MosaicRadius(double ladoTile, double margen)
        {
            double r = Cfg.IconShape == "esfera" ? ladoTile / 2.0 : FolderRadius(ladoTile);
            r -= margen;
            return r > 0.0 ? r : 0.0;
        }

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
