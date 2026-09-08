using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cubatis
{
    /// <summary>
    /// Gestiona la generación del tablero en espiral de 63 casillas,
    /// el registro de casillas y el desplazamiento animado de peones.
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        #region Campos Serializados - Sprites de Casillas

        [Header("Sprites de Casillas")]
        [Tooltip("Sprite para la casilla inicial (START). Tamaño original 1024x512.")]
        [SerializeField] private Sprite spriteStart;

        [Tooltip("Sprite para la casilla final (END). Tamaño original 1536x1536.")]
        [SerializeField] private Sprite spriteEnd;

        [Tooltip("Sprite para las 19 casillas de Beber. Tamaño original 512x512.")]
        [SerializeField] private Sprite spriteBeber;

        [Tooltip("Sprite para las 12 casillas de 'Yo nunca'. Tamaño original 512x512.")]
        [SerializeField] private Sprite spriteYoNunca;

        [Tooltip("Sprite para las 9 casillas de Verdad. Tamaño original 512x512.")]
        [SerializeField] private Sprite spriteVerdad;

        [Tooltip("Sprite para las 9 casillas de Reto. Tamaño original 512x512.")]
        [SerializeField] private Sprite spriteReto;

        [Tooltip("Sprite para las 6 casillas de Evento. Tamaño original 512x512.")]
        [SerializeField] private Sprite spriteEvento;

        [Tooltip("Sprite para las 6 casillas de Hot. Tamaño original 512x512.")]
        [SerializeField] private Sprite spriteHot;

        #endregion

        #region Campos Serializados - Configuración de Generación

        [Header("Configuración del Tablero")]
        [Tooltip("Tamaño en unidades del mundo de una casilla normal (512x512 px). Por defecto 1 unidad.")]
        [SerializeField] private float tamañoCasilla = 1.0f;

        [Tooltip("Separación adicional entre casillas.")]
        [SerializeField] private float separacionCasillas = 0f;

        [Tooltip("Si es verdadero, centra el tablero alineando la casilla final (END) en la posición de este objeto. Si es falso, centra todo el tablero geométrico.")]
        [SerializeField] private bool centrarEnFin = false;

        [Tooltip("Si es verdadero, genera el tablero automáticamente en Awake si no hay casillas.")]
        [SerializeField] private bool generarAlIniciar = false;

        [Tooltip("Añade un BoxCollider2D a cada casilla para interacción con ratón o raycasts.")]
        [SerializeField] private bool crearColliders2D = true;

        [Tooltip("Orden en la capa de sprites (Sorting Order).")]
        [SerializeField] private int sortingOrderBase = 0;

        [Tooltip("Nombre de la Sorting Layer de los sprites.")]
        [SerializeField] private string sortingLayerName = "Default";

        [Tooltip("Evita que se generen casillas de la misma categoría consecutivas al barajar.")]
        [SerializeField] private bool evitarConsecutivas = true;

        [Tooltip("Semilla aleatoria para la generación (0 = aleatoria cada vez).")]
        [SerializeField] private int semillaAleatoria = 0;

        #endregion

        #region Campos Serializados - Listas de Casillas

        [Header("Lista de Casillas")]
        [Tooltip("Lista ordenada de casillas (Transforms) que conforman el tablero.")]
        [SerializeField] private List<Transform> casillas = new List<Transform>();

        [Tooltip("Lista ordenada de componentes Casilla con sus datos tipados.")]
        [SerializeField] private List<Casilla> casillasData = new List<Casilla>();

        #endregion

        #region Campos Serializados - Configuración de Movimiento

        [Header("Configuración de Movimiento")]
        [Tooltip("Duración en segundos del movimiento entre casillas.")]
        [SerializeField] private float duracionMovimiento = 0.4f;

        [Tooltip("Curva de aceleración/desaceleración para el movimiento.")]
        [SerializeField] private AnimationCurve curvaMovimiento = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Altura del salto/arco parabólico durante el movimiento. Pon 0 para movimiento plano.")]
        [SerializeField] private float alturaArco = 0.5f;

        [Tooltip("Desplazamiento aplicado a la posición del peón respecto a la casilla.")]
        [SerializeField] private Vector3 offsetPeon = Vector3.zero;

        #endregion

        #region Campos Serializados - Gizmos / Depuración

        [Header("Gizmos / Depuración")]
        [SerializeField] private bool mostrarGizmos = true;
        [SerializeField] private Color colorCasilla = Color.cyan;
        [SerializeField] private Color colorCamino = Color.yellow;
        [SerializeField] private float radioGizmoCasilla = 0.25f;

        #endregion

        #region Eventos

        [Header("Eventos")]
        [Tooltip("Evento disparado al iniciar el movimiento de un peón. Parámetros: (Transform peon, int indiceDestino).")]
        public UnityEvent<Transform, int> onInicioMovimiento;

        [Tooltip("Evento disparado al llegar a una casilla. Parámetros: (Transform peon, int indiceCasilla).")]
        public UnityEvent<Transform, int> onLlegadaACasilla;

        [Tooltip("Evento disparado al llegar a una casilla proporcionando el componente Casilla completo. Parámetros: (Transform peon, Casilla casilla).")]
        public UnityEvent<Transform, Casilla> onLlegadaACasillaInfo;

        #endregion

        // Diccionario para controlar corrutinas activas por peón y evitar colisiones de animación
        private readonly Dictionary<Transform, Coroutine> movimientosActivos = new Dictionary<Transform, Coroutine>();

        #region Propiedades Públicas

        /// <summary>
        /// Acceso de solo lectura a la lista de casillas (Transforms).
        /// </summary>
        public IReadOnlyList<Transform> Casillas => casillas;

        /// <summary>
        /// Acceso de solo lectura a los datos tipados de las casillas.
        /// </summary>
        public IReadOnlyList<Casilla> CasillasData => casillasData;

        /// <summary>
        /// Cantidad total de casillas registradas.
        /// </summary>
        public int TotalCasillas => casillas != null ? casillas.Count : 0;

        public float TamañoCasilla
        {
            get => tamañoCasilla;
            set => tamañoCasilla = Mathf.Max(0.1f, value);
        }

        public const int Columnas = 9;
        public const int Filas = 8;

        public float SeparacionCasillas => separacionCasillas;

        /// <summary>
        /// Devuelve los límites del tablero en coordenadas del mundo.
        /// Si hay casillas instanciadas, calcula sus límites visuales exactos;
        /// de lo contrario, estima las dimensiones según columnas, filas y escala.
        /// </summary>
        public Bounds ObtenerLimitesTablero()
        {
            float paso = tamañoCasilla + separacionCasillas;
            if (casillas != null && casillas.Count > 0)
            {
                Bounds b = new Bounds(casillas[0].position, Vector3.zero);
                for (int i = 0; i < casillas.Count; i++)
                {
                    if (casillas[i] == null) continue;
                    SpriteRenderer sr = casillas[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        b.Encapsulate(sr.bounds);
                    }
                    else
                    {
                        b.Encapsulate(casillas[i].position);
                    }
                }
                return b;
            }

            Vector2 centroGrid = centrarEnFin ? new Vector2(4.0f, 3.0f) : new Vector2(4.0f, 3.5f);
            Vector2 puntoMedioGrid = new Vector2(4.0f, 3.5f);
            Vector3 centroMundo = transform.position + new Vector3(
                (puntoMedioGrid.x - centroGrid.x) * paso,
                (puntoMedioGrid.y - centroGrid.y) * paso,
                0f
            );
            Vector3 tamañoMundo = new Vector3(Columnas * paso, Filas * paso, 0f);
            return new Bounds(centroMundo, tamañoMundo);
        }

        /// <summary>
        /// Devuelve el punto central del tablero en coordenadas del mundo.
        /// </summary>
        public Vector3 ObtenerCentroTablero()
        {
            return ObtenerLimitesTablero().center;
        }

        /// <summary>
        /// Devuelve el ancho total del tablero en unidades del mundo.
        /// </summary>
        public float ObtenerAnchoTablero()
        {
            return Columnas * (tamañoCasilla + separacionCasillas);
        }

        #endregion

        #region Ciclo de Vida Unity

        private void Awake()
        {
            if (generarAlIniciar && (casillas == null || casillas.Count == 0))
            {
                GenerarTablero();
            }
        }

        #endregion

        #region Métodos de Gestión y Acceso a Casillas

        /// <summary>
        /// Comprueba si un índice de casilla es válido.
        /// </summary>
        public bool EsIndiceValido(int indice)
        {
            return casillas != null && indice >= 0 && indice < casillas.Count;
        }

        /// <summary>
        /// Obtiene el Transform de la casilla en el índice especificado.
        /// </summary>
        public Transform ObtenerCasilla(int indice)
        {
            if (!EsIndiceValido(indice))
            {
                Debug.LogWarning($"[BoardManager] Índice de casilla fuera de rango: {indice}");
                return null;
            }
            return casillas[indice];
        }

        /// <summary>
        /// Obtiene el componente Casilla (datos) en el índice especificado.
        /// </summary>
        public Casilla ObtenerCasillaData(int indice)
        {
            if (casillasData != null && indice >= 0 && indice < casillasData.Count)
            {
                if (casillasData[indice] != null) return casillasData[indice];
            }

            Transform t = ObtenerCasilla(indice);
            return t != null ? t.GetComponent<Casilla>() : null;
        }

        /// <summary>
        /// Devuelve la categoría de la casilla en el índice indicado.
        /// </summary>
        public TipoCasilla ObtenerTipoCasilla(int indice)
        {
            Casilla c = ObtenerCasillaData(indice);
            return c != null ? c.Categoria : TipoCasilla.Start;
        }

        /// <summary>
        /// Devuelve el índice de una casilla en la lista, o -1 si no existe.
        /// </summary>
        public int ObtenerIndiceCasilla(Transform casilla)
        {
            if (casillas == null || casilla == null) return -1;
            return casillas.IndexOf(casilla);
        }

        /// <summary>
        /// Devuelve la posición mundial objetivo para un peón en la casilla dada (incluyendo el offset).
        /// </summary>
        public Vector3 ObtenerPosicionCasilla(int indice)
        {
            Transform casilla = ObtenerCasilla(indice);
            if (casilla == null) return Vector3.zero;
            return casilla.position + offsetPeon;
        }

        /// <summary>
        /// Agrega una nueva casilla al final de la lista.
        /// </summary>
        public void AgregarCasilla(Transform nuevaCasilla)
        {
            if (nuevaCasilla == null) return;
            if (!casillas.Contains(nuevaCasilla))
            {
                casillas.Add(nuevaCasilla);
                Casilla c = nuevaCasilla.GetComponent<Casilla>();
                if (c != null && !casillasData.Contains(c))
                {
                    casillasData.Add(c);
                }
            }
        }

        /// <summary>
        /// Limpia todas las casillas registradas en las listas en memoria.
        /// </summary>
        public void LimpiarCasillas()
        {
            casillas.Clear();
            casillasData.Clear();
        }

        /// <summary>
        /// Utilidad de menú contextual para auto-rellenar las listas con todos los Transforms hijos de este GameObject.
        /// </summary>
        [ContextMenu("Buscar Casillas en Hijos")]
        public void CargarCasillasDesdeHijos()
        {
            casillas.Clear();
            casillasData.Clear();

            // Buscar en hijos recursivamente o directos
            Casilla[] encontradas = GetComponentsInChildren<Casilla>(true);
            if (encontradas != null && encontradas.Length > 0)
            {
                // Ordenar por índice
                var ordenadas = encontradas.OrderBy(c => c.Indice).ToList();
                foreach (Casilla c in ordenadas)
                {
                    casillas.Add(c.transform);
                    casillasData.Add(c);
                }
            }
            else
            {
                foreach (Transform hijo in transform)
                {
                    casillas.Add(hijo);
                    Casilla c = hijo.GetComponent<Casilla>();
                    if (c != null) casillasData.Add(c);
                }
            }

            Debug.Log($"[BoardManager] Se han cargado {casillas.Count} casillas desde los hijos de {gameObject.name}.");
        }

        #endregion

        #region Generación del Tablero

        private const string NOMBRE_CONTENEDOR = "ContenedorCasillas";
        private const float PPU_BASE = 100f;
        private const float ANCHO_BASE_PX = 512f;

        /// <summary>
        /// Genera el tablero completo de 63 casillas en espiral con las cantidades exactas requeridas.
        /// </summary>
        [ContextMenu("Generar Tablero")]
        public void GenerarTablero()
        {
#if UNITY_EDITOR
            VerificarYAutoCargarSprites();
#endif

            if (!ValidarSprites())
            {
                Debug.LogError("[BoardManager] Faltan sprites asignados. No se puede generar el tablero.");
                return;
            }

            LimpiarTablero();

            // Crear contenedor hijo
            GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
            contenedor.transform.SetParent(transform, false);
            contenedor.transform.localPosition = Vector3.zero;
            contenedor.transform.localRotation = Quaternion.identity;
            contenedor.transform.localScale = Vector3.one;

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(contenedor, "Generar Tablero Cubatis");
#endif

            // 1. Generar categorías barajadas para las 63 casillas
            List<TipoCasilla> categorias = GenerarDistribucionCategorias();

            // 2. Calcular posiciones en la cuadrícula de 9x8 para la espiral densa
            List<Vector2> posicionesGrid = CalcularCaminoEspiral9x8();

            // 3. Configuración de escala y centrado
            // Factor de escala: el sprite base es de 512x512 a 100 PPU (5.12 unidades).
            // Para que una casilla normal mida 'tamañoCasilla' unidades:
            float escalaFactor = tamañoCasilla / (ANCHO_BASE_PX / PPU_BASE);
            Vector3 escalaLocal = Vector3.one * escalaFactor;

            float paso = tamañoCasilla + separacionCasillas;

            // Centro del tablero:
            // Si centrarEnFin = true, el centro es la casilla END en grid (4, 3)
            // Si centrarEnFin = false, el centro es el punto medio geométrico del grid 9x8 en (4.0, 3.5)
            Vector2 centroGrid = centrarEnFin ? new Vector2(4.0f, 3.0f) : new Vector2(4.0f, 3.5f);

            casillas.Clear();
            casillasData.Clear();

            // 4. Instanciar y configurar cada casilla
            for (int i = 0; i < 63; i++)
            {
                TipoCasilla cat = categorias[i];
                Vector2 posGrid = posicionesGrid[i];

                Vector3 posMundo = transform.position + new Vector3(
                    (posGrid.x - centroGrid.x) * paso,
                    (posGrid.y - centroGrid.y) * paso,
                    0f
                );

                string nombreCasilla = $"Casilla_{i:D2}_{cat}";
                GameObject goCasilla = new GameObject(nombreCasilla);
                goCasilla.transform.SetParent(contenedor.transform, false);
                goCasilla.transform.position = posMundo;

                Sprite sprite = ObtenerSpriteParaCategoria(cat);

                Casilla compCasilla = goCasilla.AddComponent<Casilla>();
                compCasilla.Configurar(
                    nuevoIndice: i,
                    nuevaCategoria: cat,
                    sprite: sprite,
                    escalaVisual: escalaLocal,
                    agregarCollider: crearColliders2D,
                    sortingOrder: sortingOrderBase + i,
                    sortingLayerName: sortingLayerName
                );

                casillas.Add(goCasilla.transform);
                casillasData.Add(compCasilla);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif

            Debug.Log($"<color=green>[BoardManager] ¡Tablero generado con éxito! Total de casillas: {casillas.Count}.</color>");
        }

        /// <summary>
        /// Elimina todas las casillas instanciadas en la escena y limpia las listas.
        /// </summary>
        [ContextMenu("Limpiar Tablero")]
        public void LimpiarTablero()
        {
            Transform contenedor = transform.Find(NOMBRE_CONTENEDOR);
            if (contenedor != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    Destroy(contenedor.gameObject);
                }
                else
                {
                    Undo.DestroyObjectImmediate(contenedor.gameObject);
                }
#else
                Destroy(contenedor.gameObject);
#endif
            }

            // Eliminar cualquier casilla hija suelta
            List<GameObject> aDestruir = new List<GameObject>();
            foreach (Transform hijo in transform)
            {
                if (hijo.name.StartsWith("Casilla_") || hijo.GetComponent<Casilla>() != null)
                {
                    aDestruir.Add(hijo.gameObject);
                }
            }

            foreach (GameObject go in aDestruir)
            {
#if UNITY_EDITOR
                if (Application.isPlaying) Destroy(go);
                else Undo.DestroyObjectImmediate(go);
#else
                Destroy(go);
#endif
            }

            casillas.Clear();
            casillasData.Clear();

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            Debug.Log("[BoardManager] Tablero limpiado.");
        }

        #endregion

        #region Algoritmos de Generación de Espiral y Barajado

        /// <summary>
        /// Genera la lista ordenada de las 63 categorías de casillas:
        /// - Índice 0: START
        /// - Índices 1 a 61: 19 Beber, 12 Yo nunca, 9 Verdad, 9 Reto, 6 Evento, 6 Hot (barajadas sin agrupar)
        /// - Índice 62: END
        /// </summary>
        public List<TipoCasilla> GenerarDistribucionCategorias()
        {
            List<TipoCasilla> resultado = new List<TipoCasilla>(63);
            resultado.Add(TipoCasilla.Start);

            Dictionary<TipoCasilla, int> cantidades = new Dictionary<TipoCasilla, int>
            {
                { TipoCasilla.Beber, 19 },
                { TipoCasilla.YoNunca, 12 },
                { TipoCasilla.Verdad, 9 },
                { TipoCasilla.Reto, 9 },
                { TipoCasilla.Evento, 6 },
                { TipoCasilla.Hot, 6 }
            };

            System.Random rng = semillaAleatoria != 0
                ? new System.Random(semillaAleatoria)
                : new System.Random();

            List<TipoCasilla> intermedias = null;

            if (evitarConsecutivas)
            {
                intermedias = BarajarSinConsecutivas(cantidades, rng);
            }

            if (intermedias == null || intermedias.Count != 61)
            {
                intermedias = BarajarPuro(cantidades, rng);
            }

            resultado.AddRange(intermedias);
            resultado.Add(TipoCasilla.End);

            return resultado;
        }

        private List<TipoCasilla> BarajarSinConsecutivas(Dictionary<TipoCasilla, int> conteosOriginales, System.Random rng)
        {
            const int maxIntentos = 2000;

            for (int intento = 0; intento < maxIntentos; intento++)
            {
                Dictionary<TipoCasilla, int> restantes = new Dictionary<TipoCasilla, int>(conteosOriginales);
                List<TipoCasilla> lista = new List<TipoCasilla>(61);
                bool posible = true;

                for (int paso = 0; paso < 61; paso++)
                {
                    List<TipoCasilla> disponibles = restantes
                        .Where(kv => kv.Value > 0)
                        .Select(kv => kv.Key)
                        .ToList();

                    // Filtrar la categoría anterior para evitar consecutivas
                    if (lista.Count > 0)
                    {
                        TipoCasilla anterior = lista[lista.Count - 1];
                        List<TipoCasilla> sinRepetir = disponibles.Where(c => c != anterior).ToList();
                        if (sinRepetir.Count > 0)
                        {
                            disponibles = sinRepetir;
                        }
                        else
                        {
                            posible = false;
                            break;
                        }
                    }

                    // Selección ponderada según la cantidad restante (prioriza la categoría con más casillas)
                    int pesoTotal = disponibles.Sum(c => restantes[c]);
                    int tiro = rng.Next(pesoTotal);
                    int acumulado = 0;
                    TipoCasilla elegida = disponibles[0];

                    foreach (TipoCasilla c in disponibles)
                    {
                        acumulado += restantes[c];
                        if (tiro < acumulado)
                        {
                            elegida = c;
                            break;
                        }
                    }

                    lista.Add(elegida);
                    restantes[elegida]--;
                }

                if (posible && lista.Count == 61)
                {
                    return lista;
                }
            }

            return null;
        }

        private List<TipoCasilla> BarajarPuro(Dictionary<TipoCasilla, int> conteosOriginales, System.Random rng)
        {
            List<TipoCasilla> bolsa = new List<TipoCasilla>(61);
            foreach (var kv in conteosOriginales)
            {
                for (int i = 0; i < kv.Value; i++)
                {
                    bolsa.Add(kv.Key);
                }
            }

            // Fisher-Yates shuffle
            for (int i = bolsa.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                TipoCasilla temp = bolsa[i];
                bolsa[i] = bolsa[j];
                bolsa[j] = temp;
            }

            return bolsa;
        }

        /// <summary>
        /// Calcula las coordenadas en la cuadrícula de 9x8 para las 63 casillas:
        /// - Casilla 0 (START, ancho doble): centroide entre las celdas (0,0) y (1,0) -> (0.5, 0)
        /// - Casillas 1 a 61: celdas intermedias recorriendo la espiral hacia adentro
        /// - Casilla 62 (END, 3x3): centro del bloque central [3..5, 2..4] -> (4, 3)
        /// </summary>
        public static List<Vector2> CalcularCaminoEspiral9x8()
        {
            const int W = 9;
            const int H = 8;
            const int cx = 3;
            const int cy = 2;

            // Celdas ocupadas por el bloque 3x3 de END
            HashSet<Vector2Int> celdasCentro = new HashSet<Vector2Int>();
            for (int dy = 0; dy < 3; dy++)
            {
                for (int dx = 0; dx < 3; dx++)
                {
                    celdasCentro.Add(new Vector2Int(cx + dx, cy + dy));
                }
            }

            // Generar camino de 63 celdas por espiral rectangular
            List<Vector2Int> celdasCamino = new List<Vector2Int>(63);
            HashSet<Vector2Int> visitadas = new HashSet<Vector2Int>();

            Vector2Int posActual = new Vector2Int(0, 0);
            Vector2Int[] direcciones = new Vector2Int[]
            {
                new Vector2Int(1, 0),  // Derecha
                new Vector2Int(0, 1),  // Arriba
                new Vector2Int(-1, 0), // Izquierda
                new Vector2Int(0, -1)  // Abajo
            };

            int dirIdx = 0;

            while (true)
            {
                celdasCamino.Add(posActual);
                visitadas.Add(posActual);

                bool encontrada = false;
                for (int giro = 0; giro < 4; giro++)
                {
                    int nd = (dirIdx + giro) % 4;
                    Vector2Int dir = direcciones[nd];
                    Vector2Int siguiente = posActual + dir;

                    if (siguiente.x >= 0 && siguiente.x < W &&
                        siguiente.y >= 0 && siguiente.y < H &&
                        !visitadas.Contains(siguiente) &&
                        !celdasCentro.Contains(siguiente))
                    {
                        dirIdx = nd;
                        posActual = siguiente;
                        encontrada = true;
                        break;
                    }
                }

                if (!encontrada)
                {
                    break;
                }
            }

            // Mapeo a las 63 casillas del juego
            List<Vector2> posicionesCasillas = new List<Vector2>(63);

            // Casilla 0 (START): doble de ancho, centrada entre celda 0 y celda 1
            Vector2Int c0 = celdasCamino[0];
            Vector2Int c1 = celdasCamino[1];
            posicionesCasillas.Add(new Vector2((c0.x + c1.x) * 0.5f, (c0.y + c1.y) * 0.5f));

            // Casillas 1 a 61: celdas 2 a 62 del camino
            for (int i = 1; i <= 61; i++)
            {
                Vector2Int c = celdasCamino[i + 1];
                posicionesCasillas.Add(new Vector2(c.x, c.y));
            }

            // Casilla 62 (END): centro del bloque 3x3 (cx + 1, cy + 1)
            posicionesCasillas.Add(new Vector2(cx + 1f, cy + 1f));

            return posicionesCasillas;
        }

        #endregion

        #region Gestión de Sprites

        /// <summary>
        /// Obtiene el sprite correspondiente a una categoría.
        /// </summary>
        public Sprite ObtenerSpriteParaCategoria(TipoCasilla categoria)
        {
            return categoria switch
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
        }

        /// <summary>
        /// Comprueba si todos los 8 sprites requeridos están asignados.
        /// </summary>
        public bool ValidarSprites()
        {
            return spriteStart != null &&
                   spriteEnd != null &&
                   spriteBeber != null &&
                   spriteYoNunca != null &&
                   spriteVerdad != null &&
                   spriteReto != null &&
                   spriteEvento != null &&
                   spriteHot != null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Busca y asigna automáticamente los 8 sprites desde las carpetas del proyecto.
        /// </summary>
        [ContextMenu("Cargar Sprites Automáticamente")]
        public bool CargarSpritesAutomaticamente()
        {
            string[] carpetasBusqueda = new string[]
            {
                "Assets/Boards/Casillas",
                "Assets/Boards/Tiles"
            };

            bool asignadoAlguno = false;

            if (spriteStart == null) spriteStart = BuscarSprite("START", carpetasBusqueda);
            if (spriteEnd == null) spriteEnd = BuscarSprite("END", carpetasBusqueda);
            if (spriteBeber == null) spriteBeber = BuscarSprite("Beber", carpetasBusqueda);
            if (spriteYoNunca == null) spriteYoNunca = BuscarSprite("Yo nunca", carpetasBusqueda);
            if (spriteVerdad == null) spriteVerdad = BuscarSprite("Verdad", carpetasBusqueda);
            if (spriteReto == null) spriteReto = BuscarSprite("Reto", carpetasBusqueda);
            if (spriteEvento == null) spriteEvento = BuscarSprite("Evento", carpetasBusqueda);
            if (spriteHot == null) spriteHot = BuscarSprite("Hot", carpetasBusqueda);

            asignadoAlguno = ValidarSprites();
            if (asignadoAlguno)
            {
                EditorUtility.SetDirty(this);
                Debug.Log("<color=green>[BoardManager] Todos los sprites fueron cargados correctamente.</color>");
            }
            else
            {
                Debug.LogWarning("[BoardManager] Algunos sprites no pudieron ser localizados automáticamente.");
            }

            return asignadoAlguno;
        }

        private void VerificarYAutoCargarSprites()
        {
            if (!ValidarSprites())
            {
                CargarSpritesAutomaticamente();
            }
        }

        private Sprite BuscarSprite(string nombre, string[] carpetas)
        {
            foreach (string carpeta in carpetas)
            {
                string[] extensiones = new string[] { ".png", ".jpg", ".jpeg" };
                foreach (string ext in extensiones)
                {
                    string ruta = $"{carpeta}/{nombre}{ext}";
                    UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ruta);
                    if (assets != null)
                    {
                        foreach (var asset in assets)
                        {
                            if (asset is Sprite s)
                            {
                                return s;
                            }
                        }
                    }
                }
            }
            return null;
        }
#endif

        #endregion

        #region Métodos de Movimiento de Peón

        /// <summary>
        /// Consulta si un peón específico está actualmente en movimiento.
        /// </summary>
        public bool EstaMoviendose(Transform peon)
        {
            return peon != null && movimientosActivos.ContainsKey(peon);
        }

        /// <summary>
        /// Detiene el movimiento activo de un peón si existe.
        /// </summary>
        public void DetenerMovimiento(Transform peon)
        {
            if (peon != null && movimientosActivos.TryGetValue(peon, out Coroutine corrutina))
            {
                if (corrutina != null)
                {
                    StopCoroutine(corrutina);
                }
                movimientosActivos.Remove(peon);
            }
        }

        /// <summary>
        /// Coloca instantáneamente al peón en la casilla destino sin animación.
        /// </summary>
        public void ColocarPeonEnCasilla(Transform peon, int indiceCasilla)
        {
            if (peon == null)
            {
                Debug.LogWarning("[BoardManager] El peón proporcionado es nulo.");
                return;
            }

            if (!EsIndiceValido(indiceCasilla))
            {
                Debug.LogWarning($"[BoardManager] No se puede colocar el peón. Casilla inválida: {indiceCasilla}");
                return;
            }

            DetenerMovimiento(peon);
            peon.position = ObtenerPosicionCasilla(indiceCasilla);
            NotificarLlegadaACasilla(peon, indiceCasilla);
        }

        /// <summary>
        /// Mueve un peón directamente hacia la casilla destino.
        /// </summary>
        public void MoverPeon(Transform peon, int indiceDestino, bool instantaneo = false, Action alCompletar = null)
        {
            if (peon == null)
            {
                Debug.LogWarning("[BoardManager] El peón proporcionado es nulo.");
                return;
            }

            if (!EsIndiceValido(indiceDestino))
            {
                Debug.LogWarning($"[BoardManager] Índice destino no válido: {indiceDestino}");
                return;
            }

            if (instantaneo || duracionMovimiento <= 0f)
            {
                ColocarPeonEnCasilla(peon, indiceDestino);
                alCompletar?.Invoke();
                return;
            }

            DetenerMovimiento(peon);
            Coroutine corrutina = StartCoroutine(RutinaMoverDirecto(peon, indiceDestino, alCompletar));
            movimientosActivos[peon] = corrutina;
        }

        /// <summary>
        /// Mueve un peón a la casilla destino especificada por su Transform.
        /// </summary>
        public void MoverPeon(Transform peon, Transform casillaDestino, bool instantaneo = false, Action alCompletar = null)
        {
            int indice = ObtenerIndiceCasilla(casillaDestino);
            if (indice < 0)
            {
                Debug.LogWarning("[BoardManager] La casilla destino no pertenece a este tablero.");
                return;
            }

            MoverPeon(peon, indice, instantaneo, alCompletar);
        }

        /// <summary>
        /// Mueve un peón paso a paso recorriendo todas las casillas intermedias entre el origen y el destino.
        /// </summary>
        public void MoverPeonPorCamino(Transform peon, int indiceOrigen, int indiceDestino, Action alCompletar = null)
        {
            if (peon == null)
            {
                Debug.LogWarning("[BoardManager] El peón proporcionado es nulo.");
                return;
            }

            if (!EsIndiceValido(indiceOrigen) || !EsIndiceValido(indiceDestino))
            {
                Debug.LogWarning($"[BoardManager] Índices inválidos para recorrido: Origen={indiceOrigen}, Destino={indiceDestino}");
                return;
            }

            if (indiceOrigen == indiceDestino)
            {
                alCompletar?.Invoke();
                return;
            }

            DetenerMovimiento(peon);
            Coroutine corrutina = StartCoroutine(RutinaMoverPorCamino(peon, indiceOrigen, indiceDestino, alCompletar));
            movimientosActivos[peon] = corrutina;
        }

        /// <summary>
        /// Avanza un número determinado de casillas desde un índice actual.
        /// </summary>
        public void AvanzarPasos(Transform peon, int indiceActual, int pasos, Action<int> alCompletar = null)
        {
            int indiceDestino = Mathf.Clamp(indiceActual + pasos, 0, TotalCasillas - 1);
            MoverPeonPorCamino(peon, indiceActual, indiceDestino, () => alCompletar?.Invoke(indiceDestino));
        }

        private void NotificarLlegadaACasilla(Transform peon, int indice)
        {
            onLlegadaACasilla?.Invoke(peon, indice);
            if (EsIndiceValido(indice))
            {
                Casilla c = ObtenerCasillaData(indice);
                onLlegadaACasillaInfo?.Invoke(peon, c);
            }
        }

        #endregion

        #region Corrutinas de Movimiento

        private IEnumerator RutinaMoverDirecto(Transform peon, int indiceDestino, Action alCompletar)
        {
            onInicioMovimiento?.Invoke(peon, indiceDestino);

            Vector3 inicio = peon.position;
            Vector3 destino = ObtenerPosicionCasilla(indiceDestino);
            float tiempo = 0f;

            while (tiempo < duracionMovimiento)
            {
                if (peon == null) yield break;

                tiempo += Time.deltaTime;
                float t = Mathf.Clamp01(tiempo / duracionMovimiento);
                float evaluado = curvaMovimiento.Evaluate(t);

                Vector3 posInterpolada = Vector3.Lerp(inicio, destino, evaluado);

                if (alturaArco > 0f)
                {
                    float arco = Mathf.Sin(t * Mathf.PI) * alturaArco;
                    posInterpolada += Vector3.up * arco;
                }

                peon.position = posInterpolada;
                yield return null;
            }

            if (peon != null)
            {
                peon.position = destino;
            }

            movimientosActivos.Remove(peon);
            NotificarLlegadaACasilla(peon, indiceDestino);
            alCompletar?.Invoke();
        }

        private IEnumerator RutinaMoverPorCamino(Transform peon, int origen, int destino, Action alCompletar)
        {
            int direccion = origen < destino ? 1 : -1;
            int indicePaso = origen;

            while (indicePaso != destino)
            {
                if (peon == null) yield break;

                indicePaso += direccion;
                Vector3 posInicio = peon.position;
                Vector3 posPaso = ObtenerPosicionCasilla(indicePaso);

                onInicioMovimiento?.Invoke(peon, indicePaso);

                float tiempo = 0f;
                while (tiempo < duracionMovimiento)
                {
                    if (peon == null) yield break;

                    tiempo += Time.deltaTime;
                    float t = Mathf.Clamp01(tiempo / duracionMovimiento);
                    float evaluado = curvaMovimiento.Evaluate(t);

                    Vector3 posInterpolada = Vector3.Lerp(posInicio, posPaso, evaluado);

                    if (alturaArco > 0f)
                    {
                        float arco = Mathf.Sin(t * Mathf.PI) * alturaArco;
                        posInterpolada += Vector3.up * arco;
                    }

                    peon.position = posInterpolada;
                    yield return null;
                }

                if (peon != null)
                {
                    peon.position = posPaso;
                }

                NotificarLlegadaACasilla(peon, indicePaso);
            }

            movimientosActivos.Remove(peon);
            alCompletar?.Invoke();
        }

        #endregion

        #region Visualización en Editor (Gizmos)

        private void OnDrawGizmos()
        {
            if (!mostrarGizmos || casillas == null || casillas.Count == 0) return;

            for (int i = 0; i < casillas.Count; i++)
            {
                if (casillas[i] == null) continue;

                Vector3 pos = casillas[i].position;

                Gizmos.color = (i == 0) ? Color.green : (i == casillas.Count - 1) ? Color.red : colorCasilla;
                Gizmos.DrawWireSphere(pos, radioGizmoCasilla);

                if (i < casillas.Count - 1 && casillas[i + 1] != null)
                {
                    Gizmos.color = colorCamino;
                    Gizmos.DrawLine(pos, casillas[i + 1].position);
                }
            }
        }

        #endregion
    }
}
