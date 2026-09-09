using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cubatis
{
    /// <summary>
    /// Unico componente del sistema de tablero. Genera un tablero tipo "oca" en
    /// espiral cuadrada sobre una rejilla de 8x8 = 64 celdas, usando los sprites
    /// de Assets/Boards/Casillas (no crea arte nuevo).
    ///
    /// Reparto FIJO de las 64 celdas (se comprueba en <see cref="VerificarCamino"/>):
    ///   START  : 2 celdas  -> esquina inferior izquierda   (sprite 1024x512)
    ///   Camino : 58 celdas -> numeradas 1..58 en orden      (sprites 512x512)
    ///   END    : 4 celdas  -> bloque 2x2 EN EL CENTRO       (sprite 1024x1024)
    ///   64 - 2 - 4 = 58.
    ///
    /// El bloque END es 2x2 (par) para que quede centrado de verdad en un grid
    /// par 8x8: ocupa columnas 3-4 y filas 3-4, con 3 celdas libres por cada uno
    /// de los cuatro lados. Un bloque impar 3x3 nunca puede centrarse en 8x8.
    ///
    /// Recorrido: se empieza en (columna 0, fila 0), se avanza a la derecha por
    /// la fila inferior y se gira 90 grados hacia el interior en cada esquina
    /// (arriba, izquierda, abajo, ...), cerrando anillos de fuera hacia dentro.
    /// La espiral recorre 3 anillos completos (26 + 20 + 12 celdas numeradas
    /// tras descontar START) y se detiene justo cuando el hueco restante es el
    /// cuadrado 2x2 central: esas 4 celdas son END.
    ///
    /// Generacion EN EL EDITOR (boton del inspector), no en runtime: el layout
    /// es siempre el mismo y solo cambia el reparto aleatorio de categorias, asi
    /// que instanciar ~60 GameObjects en cada arranque en el movil no aporta
    /// nada. Se genera una vez, queda guardado en la escena y listo.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeneradorTablero : MonoBehaviour
    {
        // --- Geometria fija de la espiral ------------------------------------
        public const int Columnas = 8;
        public const int Filas = 8;
        public const int CeldasStart = 2;
        public const int CeldasEnd = 4;                                              // bloque 2x2 central
        public const int CeldasNumeradas = Columnas * Filas - CeldasStart - CeldasEnd; // 58

        private const float LadoSpritePx = 512f;   // lado de una casilla base en px
        private const string NombreContenedor = "Casillas";

        [Header("Tamano")]
        [Tooltip("Lado en unidades de mundo de una casilla base (512 px).")]
        [SerializeField] private float tamanoCasilla = 1f;
        [SerializeField] private float separacion = 0f;

        [Header("Render")]
        [SerializeField] private bool crearColliders = true;
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int ordenBase = 0;

        [Header("Numeracion")]
        [SerializeField] private bool mostrarNumeros = true;
        [Tooltip("Tamano de fuente del numero (relativo a la casilla).")]
        [SerializeField] private float tamanoNumero = 9f;
        [SerializeField] private Color colorNumero = Color.white;

        [Header("Reparto aleatorio de categorias")]
        [SerializeField] private bool evitarConsecutivas = true;
        [Tooltip("0 = semilla aleatoria en cada generacion.")]
        [SerializeField] private int semilla = 0;

        [Header("Sprites (Assets/Boards/Casillas)")]
        [SerializeField] private Sprite spriteStart;
        [SerializeField] private Sprite spriteEnd;
        [SerializeField] private Sprite spriteBeber;
        [SerializeField] private Sprite spriteYoNunca;
        [SerializeField] private Sprite spriteVerdad;
        [SerializeField] private Sprite spriteReto;
        [SerializeField] private Sprite spriteEvento;
        [SerializeField] private Sprite spriteHot;

        [SerializeField, HideInInspector] private List<Casilla> casillas = new List<Casilla>();

        // Reparto de las 58 casillas numeradas. La suma DEBE ser CeldasNumeradas.
        private static readonly (TipoCasilla tipo, int cantidad)[] Reparto =
        {
            (TipoCasilla.Beber,   18),
            (TipoCasilla.YoNunca, 12),
            (TipoCasilla.Verdad,   9),
            (TipoCasilla.Reto,     9),
            (TipoCasilla.Evento,   5),
            (TipoCasilla.Hot,      5),
        };

        // --- Consulta del tablero ya generado -------------------------------
        public IReadOnlyList<Casilla> Casillas => casillas;
        public int Total => casillas.Count;
        public bool EsIndiceValido(int i) => i >= 0 && i < casillas.Count;
        public Casilla ObtenerCasilla(int i) => EsIndiceValido(i) ? casillas[i] : null;
        public Vector3 ObtenerPosicion(int i) => EsIndiceValido(i) ? casillas[i].transform.position : transform.position;

        public bool SpritesAsignados =>
            spriteStart && spriteEnd && spriteBeber && spriteYoNunca &&
            spriteVerdad && spriteReto && spriteEvento && spriteHot;

        public bool MostrarNumeros
        {
            get => mostrarNumeros;
            set
            {
                mostrarNumeros = value;
                foreach (var c in casillas)
                    if (c != null) c.MostrarNumero(value);
            }
        }

        // --- Generacion -----------------------------------------------------
        [ContextMenu("Generar tablero")]
        public void GenerarTablero()
        {
            if (!SpritesAsignados)
            {
                Debug.LogError("[Tablero] Faltan sprites por asignar en el inspector.");
                return;
            }

            List<TipoCasilla> categorias = RepartirCategorias();
            if (categorias == null) return;

            LimpiarTablero();

            Vector2Int[] espiral = CalcularEspiral();
            var contenedor = new GameObject(NombreContenedor);
            contenedor.transform.SetParent(transform, false);

            float unidadesPorPixel = tamanoCasilla / LadoSpritePx;

            // START: celdas 0..1  (ancla = celda 0, la de menor x,y)
            CrearCasilla(contenedor.transform, 0, espiral[0], TipoCasilla.Start, spriteStart, unidadesPorPixel);

            // Numeradas 1..58: celdas 2..59
            for (int n = 1; n <= CeldasNumeradas; n++)
            {
                TipoCasilla t = categorias[n - 1];
                CrearCasilla(contenedor.transform, n, espiral[CeldasStart + n - 1], t, SpriteDe(t), unidadesPorPixel);
            }

            // END: celdas 60..63  (ancla = esquina inf-izq del bloque 2x2 central)
            Vector2Int anclaEnd = EsquinaMinima(espiral, CeldasStart + CeldasNumeradas, CeldasEnd);
            CrearCasilla(contenedor.transform, CeldasNumeradas + 1, anclaEnd, TipoCasilla.End, spriteEnd, unidadesPorPixel);

            Debug.Log($"[Tablero] Generado: 1 START + {CeldasNumeradas} numeradas + 1 END = {casillas.Count} casillas.");
            VerificarCamino();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
                EditorUtility.SetDirty(gameObject);
            }
#endif
        }

        [ContextMenu("Limpiar tablero")]
        public void LimpiarTablero()
        {
            var previo = transform.Find(NombreContenedor);
            if (previo != null) Destruir(previo.gameObject);

            // Restos de generaciones antiguas colgando directamente del tablero.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform h = transform.GetChild(i);
                if (h.GetComponent<Casilla>() != null) Destruir(h.gameObject);
            }

            casillas.Clear();
        }

        private void CrearCasilla(Transform padre, int indice, Vector2Int ancla,
            TipoCasilla tipo, Sprite sprite, float unidadesPorPixel)
        {
            float paso = tamanoCasilla + separacion;
            Vector3 origen = transform.position
                - new Vector3(Columnas * paso, Filas * paso, 0f) * 0.5f;   // esquina inf-izq del tablero
            Vector3 esquina = origen + new Vector3(ancla.x * paso, ancla.y * paso, 0f);

            int numero = (tipo == TipoCasilla.Start || tipo == TipoCasilla.End) ? 0 : indice;

            var go = new GameObject($"Casilla_{indice:D2}_{tipo}");
            go.transform.SetParent(padre, false);
            var casilla = go.AddComponent<Casilla>();
            casilla.Construir(sprite, esquina, unidadesPorPixel, indice, numero, tipo,
                crearColliders, sortingLayer, ordenBase + indice * 2);
            casilla.CrearNumero(tamanoNumero, colorNumero, mostrarNumeros, sortingLayer, ordenBase + indice * 2 + 1);

            casillas.Add(casilla);
        }

        private void Destruir(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // --- Espiral (funcion pura) ----------------------------------------
        /// <summary>
        /// Recorrido en espiral cuadrada de las 64 celdas. x = columna,
        /// y = fila (0 = fila inferior). Sin huecos ni solapes.
        ///
        /// La espiral visita los 3 anillos exteriores (28 + 20 + 12 celdas) y
        /// termina por el cuadrado 2x2 central (indices 60..63 = columnas 3-4,
        /// filas 3-4). Por eso el reparto START(0..1) / numeradas(2..59) /
        /// END(60..63) deja el hueco central 2x2 exacto y centrado: no hay que
        /// detener el bucle antes, basta con partir el recorrido por CeldasEnd.
        /// </summary>
        private static Vector2Int[] CalcularEspiral()
        {
            var celdas = new Vector2Int[Columnas * Filas];
            int x = 0, y = 0, dx = 1, dy = 0;
            int minX = 0, maxX = Columnas - 1, minY = 0, maxY = Filas - 1;

            for (int i = 0; i < celdas.Length; i++)
            {
                celdas[i] = new Vector2Int(x, y);

                if (x == maxX && dx == 1) { dx = 0; dy = 1; minY++; }
                else if (y == maxY && dy == 1) { dx = -1; dy = 0; maxX--; }
                else if (x == minX && dx == -1) { dx = 0; dy = -1; maxY--; }
                else if (y == minY && dy == -1) { dx = 1; dy = 0; minX++; }

                x += dx;
                y += dy;
            }
            return celdas;
        }

        private static Vector2Int EsquinaMinima(Vector2Int[] celdas, int inicio, int cuenta)
        {
            Vector2Int min = celdas[inicio];
            for (int i = inicio + 1; i < inicio + cuenta; i++)
            {
                if (celdas[i].x < min.x) min.x = celdas[i].x;
                if (celdas[i].y < min.y) min.y = celdas[i].y;
            }
            return min;
        }

        private Sprite SpriteDe(TipoCasilla t) => t switch
        {
            TipoCasilla.Start => spriteStart,
            TipoCasilla.End => spriteEnd,
            TipoCasilla.Beber => spriteBeber,
            TipoCasilla.YoNunca => spriteYoNunca,
            TipoCasilla.Verdad => spriteVerdad,
            TipoCasilla.Reto => spriteReto,
            TipoCasilla.Evento => spriteEvento,
            TipoCasilla.Hot => spriteHot,
            _ => null
        };

        // --- Reparto aleatorio de categorias -----------------------------
        private List<TipoCasilla> RepartirCategorias()
        {
            var conteo = new Dictionary<TipoCasilla, int>();
            int suma = 0;
            foreach (var (tipo, cantidad) in Reparto) { conteo[tipo] = cantidad; suma += cantidad; }

            if (suma != CeldasNumeradas)
            {
                Debug.LogError($"[Tablero] El reparto suma {suma} y deben ser {CeldasNumeradas}. Ajusta 'Reparto'.");
                return null;
            }

            var rng = semilla != 0 ? new System.Random(semilla) : new System.Random();
            return evitarConsecutivas
                ? BarajarSinConsecutivas(conteo, rng)
                : BarajarBolsa(conteo, rng);
        }

        private static List<TipoCasilla> BarajarBolsa(Dictionary<TipoCasilla, int> conteo, System.Random rng)
        {
            var bolsa = new List<TipoCasilla>(CeldasNumeradas);
            foreach (var kv in conteo)
                for (int i = 0; i < kv.Value; i++) bolsa.Add(kv.Key);

            for (int i = bolsa.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (bolsa[i], bolsa[j]) = (bolsa[j], bolsa[i]);
            }
            return bolsa;
        }

        // Coloca las categorias en orden eligiendo cada paso una categoria al
        // azar (ponderada por las que quedan) distinta de la anterior. El bucle
        // externo reintenta si en un paso solo queda la categoria recien puesta.
        private static List<TipoCasilla> BarajarSinConsecutivas(Dictionary<TipoCasilla, int> conteo, System.Random rng)
        {
            for (int intento = 0; intento < 200; intento++)
            {
                var quedan = new Dictionary<TipoCasilla, int>(conteo);
                var lista = new List<TipoCasilla>(CeldasNumeradas);
                bool ok = true;

                while (lista.Count < CeldasNumeradas)
                {
                    var opciones = quedan.Where(kv => kv.Value > 0 &&
                        (lista.Count == 0 || kv.Key != lista[lista.Count - 1])).ToList();
                    if (opciones.Count == 0) { ok = false; break; }

                    int total = opciones.Sum(kv => kv.Value);
                    int tiro = rng.Next(total);
                    TipoCasilla elegida = opciones[0].Key;
                    foreach (var kv in opciones)
                    {
                        tiro -= kv.Value;
                        if (tiro < 0) { elegida = kv.Key; break; }
                    }

                    lista.Add(elegida);
                    quedan[elegida]--;
                }

                if (ok) return lista;
            }

            Debug.LogWarning("[Tablero] No se pudo evitar categorias consecutivas; se usa un barajado simple.");
            return BarajarBolsa(conteo, rng);
        }

        // --- Depuracion ---------------------------------------------------
        /// <summary>
        /// Imprime en consola la (fila, columna) de cada casilla 1..58 y
        /// comprueba que el recorrido no tiene duplicados ni huecos, que START
        /// y END no se solapan con el camino, que END es un bloque 2x2 y que
        /// queda centrado de forma simetrica (misma distancia a los 4 bordes).
        /// </summary>
        [ContextMenu("Verificar camino en espiral")]
        public bool VerificarCamino()
        {
            Vector2Int[] e = CalcularEspiral();
            var start = e.Take(CeldasStart).ToArray();
            var numeradas = e.Skip(CeldasStart).Take(CeldasNumeradas).ToArray();
            var end = e.Skip(CeldasStart + CeldasNumeradas).Take(CeldasEnd).ToArray();

            bool ok = true;
            Debug.Log($"[Tablero] 64 - {CeldasStart} (START) - {CeldasEnd} (END) = {CeldasNumeradas} numeradas");

            var vistas = new HashSet<Vector2Int>();
            for (int i = 0; i < numeradas.Length; i++)
            {
                Vector2Int c = numeradas[i];
                if (!vistas.Add(c)) { Debug.LogError($"[Tablero] Casilla {i + 1} DUPLICADA en fila {c.y}, columna {c.x}"); ok = false; }
                Debug.Log($"Casilla {i + 1:00}: fila {c.y}, columna {c.x}");
            }

            var todo = new HashSet<Vector2Int>(start);
            foreach (var c in numeradas) todo.Add(c);
            int sinEnd = todo.Count;
            foreach (var c in end) todo.Add(c);
            if (sinEnd != CeldasStart + CeldasNumeradas || todo.Count != Columnas * Filas)
            { Debug.LogError("[Tablero] START, camino y END dejan huecos o se solapan."); ok = false; }

            // END debe ser un bloque 2x2 contiguo.
            int minX = end.Min(c => c.x), maxX = end.Max(c => c.x);
            int minY = end.Min(c => c.y), maxY = end.Max(c => c.y);
            if (end.Length != 4 || maxX - minX != 1 || maxY - minY != 1 ||
                end.Distinct().Count() != 4)
            { Debug.LogError("[Tablero] END no forma un bloque 2x2 contiguo."); ok = false; }

            // END debe quedar centrado: misma cantidad de celdas libres por lado.
            int izq = minX, der = Columnas - 1 - maxX;
            int abajo = minY, arriba = Filas - 1 - maxY;
            bool centrado = izq == der && abajo == arriba && izq == abajo;
            if (!centrado)
            { Debug.LogError($"[Tablero] END NO esta centrado (izq {izq}, der {der}, abajo {abajo}, arriba {arriba})."); ok = false; }
            Debug.Log($"[Tablero] END en columnas {minX}-{maxX}, filas {minY}-{maxY} | celdas libres por lado: izq {izq}, der {der}, abajo {abajo}, arriba {arriba} -> {(centrado ? "SIMETRICO" : "descentrado")}");

            // Reparto de numeradas por anillo concentrico y por lado del bloque END.
            int[] anillo = new int[Columnas / 2];
            foreach (var c in numeradas)
                anillo[Mathf.Min(Mathf.Min(c.x, Columnas - 1 - c.x), Mathf.Min(c.y, Filas - 1 - c.y))]++;
            int nIzq = numeradas.Count(c => c.x < minX), nDer = numeradas.Count(c => c.x > maxX);
            int nAbajo = numeradas.Count(c => c.y < minY), nArriba = numeradas.Count(c => c.y > maxY);
            Debug.Log($"[Tablero] Numeradas por anillo (exterior->interior): {string.Join(" + ", anillo)} = {anillo.Sum()}");
            Debug.Log($"[Tablero] Numeradas a cada lado de END: izq {nIzq}, der {nDer}, abajo {nAbajo}, arriba {nArriba} " +
                      $"(izq/der y abajo/arriba difieren en las 2 celdas que ocupa START)");

            Debug.Log(ok
                ? $"[Tablero] Verificacion OK: 64 celdas = START(2) + 1..{CeldasNumeradas} + END 2x2(4) centrado, sin huecos ni duplicados."
                : "[Tablero] Verificacion CON ERRORES (ver arriba).");
            return ok;
        }

        private void OnDrawGizmosSelected()
        {
            if (casillas == null || casillas.Count < 2) return;
            for (int i = 0; i < casillas.Count - 1; i++)
            {
                if (casillas[i] == null || casillas[i + 1] == null) continue;
                Gizmos.color = i == 0 ? Color.green : i == casillas.Count - 2 ? Color.red : Color.yellow;
                Gizmos.DrawLine(casillas[i].transform.position, casillas[i + 1].transform.position);
            }
        }
    }
}
