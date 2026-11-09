using System.Collections.Generic;
using TBD.Sim;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>
    /// Pila de zombis contra el muro (Teo, aprobada por Lucía el 8-nov; colocación: Bruno). SOLO PRESENTACIÓN: no toca la simulación.
    /// Cada tramo de muro (Solid Kind 0, cerrado; ni el tren ni los edificios) se parte en celdas de 0,5 m por su cara de FUERA
    /// (la contraria al centro del fuerte). En cada celda se cuentan los zombis vivos pegados a esa cara:
    ///   - a menos de 1,5 m de la cara (PileRadiusMm de Nacho), o
    ///   - más lejos, hasta 4 m, si están subidos (Pos.y > 0,25 m): los que trepan por la rampa siguen contando en su celda (nota de Nacho).
    /// Umbrales (Nacho): 3 → Altura1, 4 → 2, 5 → 3, 6 → 4, 7 o más → 5. Para que no parpadee: vuelve a sueltos solo por debajo de 2
    /// y cambia de altura como mucho una vez por segundo. Semilla = índice de celda % 3. Remates en los dos extremos de cada tramo seguido.
    /// Los zombis de una celda con pila se dejan de dibujar (DisableRendering) y vuelven a verse cuando la celda se vacía.
    /// Mallas en Resources/Models/PilaZombis/ (celda/, remate/, colision/). Si faltan, el sistema no hace nada.
    /// DIBUJO INSTANCIADO (Lucía, 09-11: el riesgo son las llamadas de dibujo): cada pieza es una entidad de Entities Graphics, como los zombis,
    /// con un solo RenderMeshArray (material TBD/Horda y las 45 mallas) y el tono por celda en ZombiePaleta (_HordaPack, DOTS instancing del shader).
    /// Así se agrupan por malla en lotes de BRG con SRP Batcher: como mucho una llamada por malla visible (15 de celda + 30 de remate) y por pasada,
    /// en vez de una por pieza (antes, GameObject con MeshRenderer y MaterialPropertyBlock, que saca al renderer del SRP Batcher).
    /// La colisión sigue en un GameObject sin renderer (MeshCollider), que es además la clave de la pieza.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PilaZombisSystem : SystemBase
    {
        const float Celda = 0.5f, Radio = 1.5f, RadioSubido = 4f, AlturaSubido = 0.25f, Periodo = 0.1f, CambioMin = 1f;

        struct EstadoCelda { public int Altura; public float Ultimo; public GameObject Pieza; }

        readonly Dictionary<long, EstadoCelda> _celdas = new Dictionary<long, EstadoCelda>();
        readonly Dictionary<long, int> _cuenta = new Dictionary<long, int>();
        readonly Dictionary<Entity, long> _celdaDe = new Dictionary<Entity, long>();
        readonly HashSet<Entity> _ocultos = new HashSet<Entity>();
        readonly List<GameObject> _remates = new List<GameObject>();
        Mesh[,] _mCelda, _mIzq, _mDer, _cCelda, _cIzq, _cDer;
        bool _cargado, _falta;
        float _t;
        Transform _raiz;
        RenderMeshArray _rma;
        readonly Dictionary<Mesh, int> _indice = new Dictionary<Mesh, int>();
        readonly Dictionary<GameObject, Entity> _ent = new Dictionary<GameObject, Entity>();

        public static bool Activa = true;       // -sinpila la apaga (para comparar en captura)
        public static float EscalaAlto = 1f;    // alto y fondo de la pieza; las piezas vienen a escala real
        int _firma;

        protected override void OnCreate()
        {
            var a = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(a, "-sinpila") >= 0) Activa = false;
            int i = System.Array.IndexOf(a, "-pilaalto");
            if (i >= 0 && i + 1 < a.Length && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) EscalaAlto = v;
        }

        static Mesh Cargar(string ruta)
        {
            var go = Resources.Load<GameObject>("Models/PilaZombis/" + ruta);
            var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
            return mf != null ? mf.sharedMesh : null;
        }

        bool CargarMallas()
        {
            _mCelda = new Mesh[6, 3]; _mIzq = new Mesh[6, 3]; _mDer = new Mesh[6, 3];
            _cCelda = new Mesh[6, 1]; _cIzq = new Mesh[6, 1]; _cDer = new Mesh[6, 1];
            for (int h = 1; h <= 5; h++)
            {
                for (int s = 0; s < 3; s++)
                {
                    _mCelda[h, s] = Cargar($"celda/SM_PilaZombi_Altura{h}_s{s}_LOD1");
                    _mIzq[h, s] = Cargar($"remate/SM_PilaZombi_RemateIzq_Altura{h}_s{s}_LOD1");
                    _mDer[h, s] = Cargar($"remate/SM_PilaZombi_RemateDer_Altura{h}_s{s}_LOD1");
                    if (_mCelda[h, s] == null) { Debug.LogWarning($"[Pila] falta Models/PilaZombis/celda/SM_PilaZombi_Altura{h}_s{s}_LOD1: sin pila"); return false; }
                }
                _cCelda[h, 0] = Cargar($"colision/SM_PilaZombi_Altura{h}_COL");          // opcionales: solo para clics y rayos de la vista
                _cIzq[h, 0] = Cargar($"colision/SM_PilaZombi_RemateIzq_Altura{h}_COL");
                _cDer[h, 0] = Cargar($"colision/SM_PilaZombi_RemateDer_Altura{h}_COL");
            }
            var mallas = new List<Mesh>();
            foreach (var arr in new[] { _mCelda, _mIzq, _mDer })
                foreach (var m in arr) if (m != null && !_indice.ContainsKey(m)) { _indice[m] = mallas.Count; mallas.Add(m); }
            _rma = new RenderMeshArray(new[] { RenderAssets.Materials[0] }, mallas.ToArray());
            _raiz = new GameObject("Pila de zombis").transform;
            Debug.Log("[Pila] mallas de la pila cargadas (5 alturas x 3 semillas, con remates)");
            return true;
        }

        static int AlturaPorCuenta(int n) => n >= 7 ? 5 : n >= 3 ? n - 2 : 0;

        // Tramo orientado: A → B a lo largo (u) y n hacia fuera (lejos del centro del fuerte).
        struct Tramo { public float2 A, U, N; public float Largo, Medio; public int Celdas; }

        static List<Tramo> Tramos(DynamicBuffer<Solid> solids)
        {
            var r = new List<Tramo>();
            var centro = new float2((Level.FortX0 + Level.FortX1) * 0.5f, (Level.FortY0 + Level.FortY1) * 0.5f);
            for (int i = 0; i < solids.Length; i++)
            {
                var s = solids[i];
                if (s.Kind != 0 || s.Open != 0 || s.Id == Level.TrainId) continue;
                float2 a = s.A, b = s.B, d = b - a;
                float len = math.length(d);
                if (len < Celda) continue;
                float2 u = d / len, n = new float2(u.y, -u.x);
                if (math.dot(centro - a, n) > 0f) { var t = a; a = b; b = t; u = -u; n = new float2(u.y, -u.x); }   // n tiene que apuntar hacia fuera
                r.Add(new Tramo { A = a, U = u, N = n, Largo = len, Medio = s.HalfThick, Celdas = (int)math.floor(len / Celda) });
            }
            return r;
        }

        protected override void OnUpdate()
        {
            if (!Activa || !RenderAssets.Ready || !RenderAssets.Horda) return;
            if (!_cargado) { _cargado = true; _falta = !CargarMallas(); }
            if (_falta) return;
            _t += SystemAPI.Time.DeltaTime;
            if (_t < Periodo) return;
            _t = 0f;
            float ahora = (float)SystemAPI.Time.ElapsedTime;

            if (!SystemAPI.TryGetSingletonEntity<ZoneTag>(out var root) || !EntityManager.HasBuffer<Solid>(root)) return;
            var tramos = Tramos(EntityManager.GetBuffer<Solid>(root));
            if (tramos.Count == 0) { Vaciar(); return; }
            int firma = tramos.Count;                                                   // si cambian los tramos (obra nueva, brecha), las claves ya no valen
            foreach (var tr in tramos) firma = firma * 31 + (int)(tr.A.x * 10f) * 7 + (int)(tr.A.y * 10f) * 13 + tr.Celdas;
            if (firma != _firma) { Vaciar(); _firma = firma; }

            // 1) Cuenta por celda (clave = tramo * 100000 + celda).
            _cuenta.Clear(); _celdaDe.Clear();
            foreach (var (z, e) in SystemAPI.Query<RefRO<Zombie>>().WithEntityAccess())
            {
                var zz = z.ValueRO;
                if (zz.State == ZState.Dead) continue;
                var p = new float2(zz.Pos.x, zz.Pos.z);
                for (int ti = 0; ti < tramos.Count; ti++)
                {
                    var tr = tramos[ti];
                    var rel = p - tr.A;
                    float t = math.dot(rel, tr.U);
                    if (t < 0f || t >= tr.Celdas * Celda) continue;
                    float s = math.dot(rel, tr.N) - tr.Medio;               // distancia a la cara de fuera
                    if (s < -0.3f) continue;
                    if (s > Radio && !(zz.Pos.y > AlturaSubido && s <= RadioSubido)) continue;
                    long k = ti * 100000L + (int)(t / Celda);
                    _cuenta.TryGetValue(k, out int c); _cuenta[k] = c + 1;
                    _celdaDe[e] = k;
                    break;
                }
            }

            // 2) Altura de cada celda con histéresis (sueltos por debajo de 2; un cambio por segundo como mucho).
            bool cambio = false;
            var claves = new List<long>(_celdas.Keys);
            foreach (var k in _cuenta.Keys) if (!_celdas.ContainsKey(k)) claves.Add(k);
            foreach (var k in claves)
            {
                _cuenta.TryGetValue(k, out int n);
                _celdas.TryGetValue(k, out var st);
                int objetivo = AlturaPorCuenta(n);
                if (objetivo == 0 && st.Altura > 0 && n >= 2) objetivo = st.Altura;    // no se deshace hasta bajar de 2
                if (objetivo == st.Altura) continue;
                if (st.Pieza != null && ahora - st.Ultimo < CambioMin) continue;      // como mucho un cambio por segundo
                if (st.Pieza != null) Quitar(st.Pieza);
                st.Pieza = null;
                st.Altura = objetivo; st.Ultimo = ahora;
                if (objetivo > 0)
                {
                    int ti = (int)(k / 100000L), ci = (int)(k % 100000L);
                    st.Pieza = Pieza(tramos[ti], ci, objetivo, _mCelda, _cCelda, 0f, "Pila");
                }
                if (st.Altura == 0) _celdas.Remove(k); else _celdas[k] = st;
                cambio = true;
            }

            // 3) Remates en los extremos de cada tramo seguido de celdas con pila (se rehacen solo si algo ha cambiado).
            if (cambio)
            {
                foreach (var g in _remates) if (g != null) Quitar(g);
                _remates.Clear();
                foreach (var kv in _celdas)
                {
                    int ti = (int)(kv.Key / 100000L), ci = (int)(kv.Key % 100000L);
                    if (ti >= tramos.Count) continue;
                    if (!_celdas.ContainsKey(kv.Key - 1)) _remates.Add(Pieza(tramos[ti], ci, kv.Value.Altura, _mIzq, _cIzq, -0.5f * Celda, "Remate izq"));
                    if (!_celdas.ContainsKey(kv.Key + 1)) _remates.Add(Pieza(tramos[ti], ci, kv.Value.Altura, _mDer, _cDer, 0.5f * Celda, "Remate der"));
                }
            }

            // 4) Los zombis de las celdas con pila no se dibujan (la pieza los sustituye).
            var ver = new List<Entity>();
            foreach (var e in _ocultos) if (!_celdaDe.TryGetValue(e, out var k) || !_celdas.ContainsKey(k)) ver.Add(e);
            foreach (var e in ver) { _ocultos.Remove(e); if (EntityManager.Exists(e)) EntityManager.RemoveComponent<DisableRendering>(e); }
            foreach (var kv in _celdaDe)
                if (_celdas.ContainsKey(kv.Value) && _ocultos.Add(kv.Key)) EntityManager.AddComponent<DisableRendering>(kv.Key);
        }

        /// <summary>Pieza de pila (o remate, con desplazamiento a lo largo) en la celda ci del tramo, con su semilla (ci % 3) y su tono por celda.</summary>
        GameObject Pieza(Tramo tr, int ci, int h, Mesh[,] mallas, Mesh[,] col, float desplaza, string nombre)
        {
            int s = ci % 3;
            var m = mallas[h, s];
            if (m == null) return null;
            float t = (ci + 0.5f) * Celda + desplaza;
            float2 p = tr.A + tr.U * t + tr.N * tr.Medio;                          // al pie de la cara de fuera
            var go = new GameObject($"{nombre} h{h} s{s}");
            go.transform.SetParent(_raiz, false);
            go.transform.position = new Vector3(p.x, 0f, p.y);
            // Teo: el muro corre por +X local y FUERA es −Z local. Con yaw θ de Unity, +X local = (cos θ, 0, −sin θ).
            float yaw = math.atan2(-tr.U.y, tr.U.x);
            go.transform.rotation = Quaternion.Euler(0f, math.degrees(yaw), 0f);
            go.transform.localScale = new Vector3(1f, EscalaAlto, EscalaAlto);   // a lo largo siempre 0,5 m (la celda); -pilaalto 1.3 la iguala a los sueltos a x1,3
            if (col != null && col[h, 0] != null) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = col[h, 0]; mc.convex = true; }

            // dibujo: entidad instanciada (ver el resumen de la clase)
            var em = EntityManager;
            var e = em.CreateEntity();
            var desc = new RenderMeshDescription(ShadowCastingMode.On, true);
            RenderMeshUtility.AddComponents(e, em, desc, _rma, MaterialMeshInfo.FromRenderMeshArrayIndices(0, _indice[m]));
            var lt = LocalTransform.FromPositionRotation(new float3(p.x, 0f, p.y), quaternion.RotateY(yaw));
            var post = float4x4.Scale(1f, EscalaAlto, EscalaAlto);
            em.AddComponentData(e, lt);
            em.AddComponentData(e, new PostTransformMatrix { Value = post });
            if (!em.HasComponent<LocalToWorld>(e)) em.AddComponentData(e, new LocalToWorld { Value = math.mul(lt.ToMatrix(), post) });
            else em.SetComponentData(e, new LocalToWorld { Value = math.mul(lt.ToMatrix(), post) });
            em.AddComponentData(e, new ZombiePaleta { Value = HordaPaleta.Pack((uint)(ci * 7919 + (int)(tr.A.x * 13f) + (int)(tr.A.y * 31f))) });
            _ent[go] = e;
            return go;
        }

        /// <summary>Quita una pieza: su entidad de dibujo y su GameObject de colisión.</summary>
        void Quitar(GameObject go)
        {
            if (_ent.TryGetValue(go, out var e)) { if (EntityManager.Exists(e)) EntityManager.DestroyEntity(e); _ent.Remove(go); }
            Object.Destroy(go);
        }

        void Vaciar()
        {
            foreach (var kv in _celdas) if (kv.Value.Pieza != null) Quitar(kv.Value.Pieza);
            foreach (var g in _remates) if (g != null) Quitar(g);
            _celdas.Clear(); _remates.Clear();
            foreach (var e in _ocultos) if (EntityManager.Exists(e)) EntityManager.RemoveComponent<DisableRendering>(e);
            _ocultos.Clear();
        }

        protected override void OnDestroy()
        {
            foreach (var kv in _ent) if (EntityManager.Exists(kv.Value)) EntityManager.DestroyEntity(kv.Value);
            _ent.Clear();
            if (_raiz != null) Object.Destroy(_raiz.gameObject);
        }
    }
}
