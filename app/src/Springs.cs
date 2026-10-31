using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;

namespace GamesDock
{
    public class Spring
    {
        public string Kind;
        public object Owner;
        public double X, V, Target, Wait;
        public double K, C;
        public double Eps, VEps;

        // que se escribe cada cuadro. En el .ps1 esto era un switch sobre Kind; aqui cada dueño
        // engancha su lambda al crear el muelle y asi el motor no necesita conocer la UI.
        public Action<Spring> Apply;
        public Action Settled;

        public static Spring Make(string kind, double k, double zeta, object owner)
        {
            bool dip = kind == "ptr" || kind == "slide" || kind == "ghostx" || kind == "ghosty";
            return new Spring
            {
                Kind = kind,
                Owner = owner,
                K = k,
                C = 2.0 * Math.Sqrt(k) * zeta,
                Eps = dip ? 0.02 : 0.0008,
                VEps = dip ? 0.8 : 0.006
            };
        }
    }

    // Un solo bucle que solo vive mientras hay transiciones y se desconoce al asentar: en reposo no
    // queda nada programado, que es lo que pide el dock. Todo lo que se mueve pasa por aqui, asi que
    // comparte inercia y caracter en lugar de solaparse con animaciones sueltas.
    public static class Anim
    {
        static readonly List<Spring> Springs = new List<Spring>();
        static readonly Stopwatch Clock = new Stopwatch();
        static bool _on;
        static double _lastFrame;
        static readonly EventHandler Handler = delegate { Frame(); };

        // el contorno solo se rehace UNA vez por cuadro: los muelles pill, ptr y slide lo piden cada
        // uno y, sin esta cola, el segundo se comería el trabajo del primero dentro del mismo frame
        public static bool ShapeDirty;
        public static Action RepaintShape;

        public static void Add(Spring s) { Springs.Add(s); }

        // los muelles de los tiles viejos apuntan a botones que ya no estan en el arbol: fuera o se
        // acumulan en cada reconstruccion de la fila
        public static void RemoveAll(Predicate<Spring> pred) { Springs.RemoveAll(pred); }

        public static void Move(Spring s, double to)
        {
            if (s == null) return;
            if (Math.Abs(s.Target - to) < 0.0005) return;
            s.Target = to;
            s.Wait = 0.0;   // si el cursor pide algo durante la entrada, ese tile deja de esperar
            Start();
        }

        public static void Start()
        {
            if (_on) return;
            _on = true;
            Clock.Restart();
            _lastFrame = Clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += Handler;
        }

        public static void Stop()
        {
            if (!_on) return;
            _on = false;
            CompositionTarget.Rendering -= Handler;
            Clock.Stop();
        }

        // dt real entre cuadros: con el mismo k/zeta la animacion tarda lo mismo a 60, 120 o 144 Hz
        static void Frame()
        {
            // RenderingTime es un TimeSpan privativo de cada MediaContext y Rendering es un evento
            // ESTATICO que dispara una vez por target de composicion: con el fantasma en pantalla hay
            // dos relojes alternandose. El unico fiable es un cronometro del proceso.
            double now = Clock.Elapsed.TotalSeconds;
            double dt = now - _lastFrame;
            // el mismo vsync puede disparar el evento por las dos ventanas: ese segundo golpe no es un cuadro
            if (dt < 0.002) return;
            _lastFrame = now;
            // un corte largo (ventana tapada, cambio de tema) no debe teletransportar los muelles
            if (dt > 0.0333) dt = 0.0333;
            // subpasos de 4 ms: con la rigidez que pide la respuesta al cursor, un cuadro de 16 ms
            // integrado de golpe se pasa del objetivo y el muelle se lee "goma" en lugar de pegado
            // al dedo. Integrando en trozos fijos el recorrido es igual a 60, 120 y 165 Hz.
            int n = (int)Math.Ceiling(dt / 0.004);
            if (n < 1) n = 1;
            else if (n > 8) n = 8;
            double h = dt / n;

            bool alive = false;
            for (int i = 0; i < n; i++)
                if (StepAll(h)) alive = true;

            if (ShapeDirty)
            {
                ShapeDirty = false;
                if (RepaintShape != null) RepaintShape();
            }
            if (!alive) Stop();
        }

        static bool StepAll(double dt)
        {
            bool alive = false;
            for (int i = 0; i < Springs.Count; i++)
                if (Step(Springs[i], dt)) alive = true;
            return alive;
        }

        // un paso de integracion: aceleracion = rigidez * error - amortiguacion * velocidad
        static bool Step(Spring s, double dt)
        {
            if (s.Wait > 0.0)
            {
                s.Wait -= dt;
                return true;
            }
            double d = s.Target - s.X;
            if (Math.Abs(d) < s.Eps && Math.Abs(s.V) < s.VEps)
            {
                if (s.X != s.Target)
                {
                    s.X = s.Target;
                    s.V = 0.0;
                    if (s.Apply != null) s.Apply(s);
                }
                if (s.Settled != null)
                {
                    var c = s.Settled;
                    s.Settled = null;
                    c();
                }
                return false;
            }
            s.V += (s.K * d - s.C * s.V) * dt;
            s.X += s.V * dt;
            if (s.Apply != null) s.Apply(s);
            return true;
        }
    }
}
