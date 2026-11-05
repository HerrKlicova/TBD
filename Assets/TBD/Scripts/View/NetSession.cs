using TBD.Net;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Hito 3 «Unión»: la red (S0) vive aquí y sigue corriendo al cambiar entre el mapa y una zona (GDD §6: nunca se congela nada).
    /// Dos mundos (A, el que se ve, y B, la copia de la prueba de desincronización) reciben las mismas órdenes. Un único objeto
    /// que sobrevive a los cambios de escena avanza el reloj a 30 Hz con la velocidad x1/x2/x4.
    /// También guarda el último fotograma antes de cambiar de escena, para el fundido (sin pantalla de carga).
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        static NetSession _instance;
        public static NetWorld A { get; private set; }
        public static NetWorld B { get; private set; }
        public static bool Check = true;
        public static bool Desync;
        public static long DesyncTick;

        // paso entre escenas
        public static Texture2D TransitionFrame;       // último fotograma de la escena anterior (para el fundido)
        public static int PendingZoneNode = -1;        // nodo al que se entra (la zona espera a que la orden Materialize se aplique)
        public static int ReturnFromNode = -1;         // nodo del que se sale (el mapa coloca la cámara ahí, a 2,5 km)

        float _acc;

        public static void Ensure()
        {
            if (_instance != null) return;
            A = NetScenario.Mvp();
            B = NetScenario.Mvp();
            var go = new GameObject("TBD_NetSession");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetSession>();
        }

        public static void Push(NetCmdType type, int arg, long delay, int arg2 = 0)
        {
            Ensure();
            var c = new NetCommand { Tick = A.Tick + delay, Type = type, Arg = arg, Arg2 = arg2 };
            A.Push(c);
            B.Push(c);
        }

        void Update()
        {
            _acc += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            const float step = 1f / NetConst.TickRate;
            while (_acc >= step)
            {
                _acc -= step;
                int n = A.Paused ? 1 : A.Speed;                // en pausa se sigue llamando a Step para aplicar las órdenes
                for (int i = 0; i < n; i++)
                {
                    A.Step();
                    if (Check) B.Step();
                    if (Check && !Desync && A.Hash() != B.Hash()) { Desync = true; DesyncTick = A.Tick; Debug.LogError($"[TBD] Red: desincronización en el tick {A.Tick}"); }
                }
            }
        }

        /// <summary>Captura el fotograma actual (al final del fotograma) para fundirlo sobre la escena siguiente.</summary>
        public static System.Collections.IEnumerator CaptureTransition()
        {
            yield return new WaitForEndOfFrame();
            if (TransitionFrame != null) Destroy(TransitionFrame);
            TransitionFrame = ScreenCapture.CaptureScreenshotAsTexture();
        }

        /// <summary>Dibuja el último fotograma de la escena anterior desvaneciéndose (llamar desde OnGUI). Devuelve false al acabar.</summary>
        public static bool DrawFade(float since, float duration = 0.6f)
        {
            if (TransitionFrame == null) return false;
            float a = 1f - Mathf.Clamp01(since / duration);
            if (a <= 0f) { Destroy(TransitionFrame); TransitionFrame = null; return false; }
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), TransitionFrame, ScaleMode.StretchToFill, false);
            GUI.color = prev;
            return true;
        }
    }
}
