namespace GamesDock
{
    // El imán de los bordes, suelto del dock: de las cuatro posiciones que hay en Ajustes, la que queda
    // más cerca del punto donde se suelta el ratón. Se juzga la mano y no el rectángulo de la barra por
    // una razón práctica: la barra ya puesta en "Abajo" está a 6 px del borde de abajo, así que con el
    // rectángulo arrastrarla hacia la derecha no la cambiaría de borde ni empujándola medio monitor fuera
    // de la pantalla. Con el punto de sueltado, llevar el cursor al extremo izquierdo vale.
    //
    // No toca WPF ni la config a propósito, para que el arnés (_dev/cs-iman.ps1) pueda compilar este
    // archivo solo y probar los cuatro bordes, las esquinas y los empates sin arrastrar la barra de Adri.
    public static class EdgeSnap
    {
        // (px,py) el punto de sueltado y (ax,ay,aw,ah) el área de trabajo, los dos en DIP de pantalla.
        // Los cuatro huecos salen por out, que es lo que luego se apunta en el log: sin ellos el arnés no
        // tiene manera de saber por qué ganó ese borde.
        public static string BordeMasCercano(double px, double py,
                                            double ax, double ay, double aw, double ah,
                                            out double arriba, out double abajo,
                                            out double izquierda, out double derecha)
        {
            arriba = py - ay;
            abajo = (ay + ah) - py;
            izquierda = px - ax;
            derecha = (ax + aw) - px;

            // el empate resuelve abajo: es la posición por defecto de la barra y un temblor de un pixel no
            // debe cambiarla de lado; a partir de ahí, arriba, izquierda y derecha en ese orden
            string mejor = "bottom";
            double d = abajo;
            if (arriba < d) { mejor = "top"; d = arriba; }
            if (izquierda < d) { mejor = "left"; d = izquierda; }
            if (derecha < d) { mejor = "right"; }
            return mejor;
        }
    }
}
