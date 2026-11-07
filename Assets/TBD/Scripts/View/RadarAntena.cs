using UnityEngine;

namespace TBD.View
{
    /// <summary>Cabeza del radar de Teo: gira sobre su Y local (la vertical), una vuelta cada 4 s, mientras está encendido (hay combustible). Solo aspecto.</summary>
    public sealed class RadarAntena : MonoBehaviour
    {
        public bool Encendido = true;
        public float SegundosPorVuelta = 4f;

        void Update()
        {
            if (!Encendido || SegundosPorVuelta <= 0f) return;
            transform.Rotate(0f, 360f / SegundosPorVuelta * Time.deltaTime, 0f, Space.Self);
        }
    }
}
