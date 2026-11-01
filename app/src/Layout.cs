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

        // escala de la pantalla donde vive la pildora. WPF redondea cada elemento al pixel FISICO
        // (UseLayoutRounding en window.xaml), asi que una medida que no cae justo en pixel entero se
        // come medio pixel por un lado y medio por el otro: a 125 % los 9+9 de halo salen 84 px en vez
        // de 85 y la fila entera se va 1 px por tile. Lo que manda en la colocacion se cuantiza aqui
        // para que la cuenta de la pildora y la del colocador sean la misma.
        public double Scale = 1.0;

        public double Snap(double v)
        {
            if (Scale <= 0.0) return v;
            return Math.Round(v * Scale, MidpointRounding.ToEven) / Scale;
        }

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
        // Las dos medidas del hueco van cuantizadas al pixel fisico: son las que fijan el grosor de la
        // pildora por el eje transversal, y si caen a medio pixel el sobrante se va entero para abajo.
        public double TileBox { get { return Snap(IconSize + 2 * RingCross); } }
        public double Pitch { get { return IconSize + 2 * RingMain; } }

        // separacion de la fila respecto al borde de la pildora a lo largo de la fila: es el colchon de
        // "clic derecho en el hueco libre". Antes era PadH + 10 y, como cada tile pagaba ademas su halo
        // de RingMain por fuera, el primer y ultimo icono quedaban a 29 px del cristal contra 8 px por
        // arriba y por abajo: la fila se veia metida con calzos dentro de la pildora.
        public double RowInset { get { return Snap(PadH); } }

        // lo que se quita de halo en la cara de fuera de los dos tiles del extremo. RingMain existe para
        // separar iconos entre si; en la punta no hay vecino, y era padding transparente que solo hacia
        // de cristal de sobra. Se saca del ancho de la pildora (RowLen) y del centro de cada tile.
        public double EndTrim { get { return Math.Max(0.0, RingMain - RingCross); } }

        // largo de la fila: n huecos de Pitch menos el halo recortado por los dos extremos
        public double RowLen(int n) { return n <= 0 ? 0.0 : n * Pitch - 2 * EndTrim; }

        // centro del tile i dentro de la fila. En la rejilla uniforme seria inset + (i+0.5)*Pitch; con
        // las puntas recortadas el primero cae d/2 antes, los del medio d antes y el ultimo 3d/2. Sin
        // esto el magnes de una punta se encoge en el punto equivocado y el indice de reordenamiento
        // se desplaza al arrastrar contra el extremo.
        public double TileCenter(int i, int n, double off)
        {
            double d = EndTrim;
            double c = RowInset + (i + 0.5) * Pitch - off;
            if (n == 1) return c - d;
            if (i == 0) return c - d / 2.0;
            if (i == n - 1) return c - 1.5 * d;
            return c - d;
        }

        // el halo del tile: RingMain a lo largo de la fila (separa vecinos) y RingCross en transversal,
        // y RingCross tambien en la cara de fuera de los dos de extremo.
        public System.Windows.Thickness TilePadding(int i, int n)
        {
            double a = RingMain, c = RingCross;
            double l = a, r = a, t = c, b = c;
            if (Vert) { l = c; r = c; t = a; b = a; }
            if (n <= 1) { if (Vert) { t = c; b = c; } else { l = c; r = c; } }
            else if (i == 0) { if (Vert) t = c; else l = c; }
            else if (i == n - 1) { if (Vert) b = c; else r = c; }
            return new System.Windows.Thickness(l, t, r, b);
        }

        // La longitud del hueco A LO LARGO de la fila hay que imponerla, no dejarla salir de medir el
        // contenido: el icono son 50 DIP que a 125 % valen 62,5 px y WPF los deja en 62, asi que cada
        // tile salia 84 px en vez de 85 y la fila real se iba desviando de la rejilla ideal que usan el
        // magnes (Lay.TileCenter) y el indice de reordenamiento (SlotU). En una fila de 8 el ultimo
        // icono se magnificaba 7 px a la izquierda de su centro verdadero.
        public double TileMain(int i, int n)
        {
            if (n <= 1) return Snap(Pitch - 2 * EndTrim);
            if (i == 0 || i == n - 1) return Snap(Pitch - EndTrim);
            return Snap(Pitch);
        }


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
