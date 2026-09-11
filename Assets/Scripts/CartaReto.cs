using System;
using System.Collections;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Cubatis
{
    /// <summary>
    /// Popup de carta de reto, dibujado en el mundo por encima del tablero.
    /// Lo abre <see cref="GestorPartida"/> al caer una ficha en una casilla
    /// numerada, pasandole el <see cref="TipoCasilla"/> y un callback de cierre.
    ///
    /// Estados: Portada --(toque)--> Reto --(toque)--> cerrada.
    ///   - Portada: imagen "[categoria] 1" (carta cerrada, sin texto).
    ///   - Reto: imagen "[categoria] 2" (plantilla vacia) + una frase al azar
    ///     de esa categoria superpuesta con TMP.
    /// Se toca la CARTA directamente (toda la pantalla, de hecho); no hay boton.
    ///
    /// Al cerrarse invoca el callback -> GestorPartida pasa el turno. Mientras
    /// esta abierta, GestorPartida mantiene el dado bloqueado.
    ///
    /// Colocacion automatica e igual para todas las categorias: la carta sale
    /// centrada en pantalla con la proporcion nativa del sprite y solo se reduce
    /// (uniformemente) si no cabe en 'altoRelativoPantalla'. El texto va centrado
    /// sobre la carta, en su 75% central.
    /// </summary>
    [DisallowMultipleComponent]
    public class CartaReto : MonoBehaviour
    {
        [Serializable]
        public class Entrada
        {
            public TipoCasilla tipo;
            [Tooltip("Carta cerrada / portada, sin texto (\"[categoria] 1\").")]
            public Sprite portada;
            [Tooltip("Plantilla vacia del reto, sin texto (\"[categoria] 2\").")]
            public Sprite plantilla;
            [TextArea(2, 4)] public string[] frases;
        }

        [Header("Datos (editar a mano / menu contextual)")]
        [SerializeField] private Entrada[] cartas;

        [Header("Tamano")]
        [Tooltip("Techo: la carta ocupa como mucho esta fraccion del alto (y del ancho) de la pantalla. Si el sprite cabe, se ve a su tamano real.")]
        [Range(0.3f, 0.95f)]
        [SerializeField] private float altoRelativoPantalla = 0.72f;

        [Header("Fondo oscuro")]
        [SerializeField] private Color colorFondo = new Color(0f, 0f, 0f, 0.78f);

        [Header("Texto del reto")]
        [Tooltip("Font Asset TMP para la frase del reto. Vacio = fuente por defecto de TMP.")]
        [SerializeField] private TMP_FontAsset fuenteReto;
        [Tooltip("Tamano maximo; si la frase no cabe en la carta se reduce sola.")]
        [SerializeField] private float tamanoTexto = 3.2f;
        [SerializeField] private Color colorTexto = Color.black;

        [Header("Animacion de giro")]
        [SerializeField] private float duracionFlip = 0.28f;

        [Header("Dibujo")]
        [Tooltip("Por encima del dado (1000) y de las fichas (500).")]
        [SerializeField] private int ordenDibujo = 2000;
        [SerializeField] private string sortingLayer = "Default";

        private const float DistanciaCamara = 9f;   // delante de la camara, por encima del tablero
        private const float ZonaTexto = 0.75f;      // fraccion del ancho/alto de la carta para el texto

        private enum Estado { Cerrada, Portada, Reto }

        private Estado estado = Estado.Cerrada;
        private bool animando;
        private Entrada actual;
        private Action alCerrar;

        private SpriteRenderer fondoSr;
        private SpriteRenderer cartaSr;
        private TextMeshPro texto;
        private BoxCollider2D toque;
        private float escala = 1f;       // escala uniforme de la carta visible
        private Vector2 centroSprite;    // centro del sprite respecto a su pivot (unidades, escala 1)
        private static Sprite spriteBlanco;

        public bool Abierta => estado != Estado.Cerrada;

        private string CapaDibujo => string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;

        private void Awake()
        {
            if (!EnGameObjectDedicado()) { enabled = false; return; }
            ConstruirVisuales();
            MostrarVisuales(false);
        }

        // CartaReto crea un collider a pantalla completa para captar el toque. Si
        // comparte GameObject con otro script (p.ej. se anade por error a
        // GestorPartida), ese collider se traga TODOS los clics del juego (dado
        // incluido). Debe ir siempre en un GameObject propio.
        private bool EnGameObjectDedicado()
        {
            foreach (MonoBehaviour m in GetComponents<MonoBehaviour>())
                if (m != null && m != this)
                {
                    Debug.LogError("[CartaReto] Debe estar SOLO en su propio GameObject (sin otros " +
                        $"scripts; encontrado '{m.GetType().Name}'). Desactivado para no bloquear la entrada.", this);
                    return false;
                }
            return true;
        }

        // ===================== API para GestorPartida =====================
        public bool TieneCarta(TipoCasilla tipo)
        {
            Entrada e = Obtener(tipo);
            return e != null && e.portada != null && e.plantilla != null;
        }

        /// <summary>Abre el popup en la portada de <paramref name="tipo"/>.
        /// <paramref name="alCerrarCallback"/> se llama al cerrarse (turno).</summary>
        public void Abrir(TipoCasilla tipo, Action alCerrarCallback)
        {
            Entrada e = Obtener(tipo);
            if (e == null || e.portada == null || e.plantilla == null)
            {
                Debug.LogError($"[CartaReto] Sin carta configurada para {tipo}.", this);
                alCerrarCallback?.Invoke();
                return;
            }

            actual = e;
            alCerrar = alCerrarCallback;
            estado = Estado.Portada;
            animando = false;

            MostrarCarta(e.portada, false);
            MostrarVisuales(true);
        }

        // ===================== Toque =====================
        private void OnMouseDown()
        {
            if (animando || estado == Estado.Cerrada) return;

            if (estado == Estado.Portada) StartCoroutine(GirarAReto());
            else Cerrar();
        }

        private IEnumerator GirarAReto()
        {
            animando = true;
            float mitad = Mathf.Max(0.01f, duracionFlip * 0.5f);

            yield return Girar(1f, 0f, mitad);
            texto.text = FraseAlAzar(actual);
            MostrarCarta(actual.plantilla, true);
            AplicarGiro(0f);   // arranca de canto: sin un frame a tamano completo
            yield return Girar(0f, 1f, mitad);

            estado = Estado.Reto;
            animando = false;
        }

        // Estrecha/ensancha en X la carta (y el texto) sobre su centro: el "giro".
        private IEnumerator Girar(float desde, float hasta, float dur)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                AplicarGiro(Mathf.Lerp(desde, hasta, Mathf.Clamp01(t)));
                yield return null;
            }
        }

        private void Cerrar()
        {
            estado = Estado.Cerrada;
            MostrarVisuales(false);
            Action cb = alCerrar;
            alCerrar = null;
            cb?.Invoke();
        }

        // ===================== Datos =====================
        private Entrada Obtener(TipoCasilla tipo)
        {
            if (cartas == null) return null;
            foreach (Entrada e in cartas)
                if (e != null && e.tipo == tipo) return e;
            return null;
        }

        private static string FraseAlAzar(Entrada e) =>
            e.frases == null || e.frases.Length == 0
                ? $"(sin frases para {e.tipo})"
                : e.frases[UnityEngine.Random.Range(0, e.frases.Length)];

        // ===================== Colocacion (automatica) =====================
        // Muestra 's' centrado en pantalla con su proporcion nativa. Escala
        // uniforme = 1 (tamano real) salvo que no quepa en 'altoRelativoPantalla'
        // del alto o del ancho visibles; entonces se reduce lo justo. El texto
        // (solo plantilla) queda centrado sobre la carta, en su 75% central.
        private void MostrarCarta(Sprite s, bool conTexto)
        {
            float altoPantalla = 10f, anchoPantalla = 10f;
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.SetPositionAndRotation(
                    cam.transform.position + cam.transform.forward * DistanciaCamara, cam.transform.rotation);
                altoPantalla = cam.orthographic
                    ? cam.orthographicSize * 2f
                    : 2f * DistanciaCamara * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                anchoPantalla = altoPantalla * cam.aspect;
            }

            Vector2 tamReal = s.bounds.size;   // unidades de mundo a escala 1
            centroSprite = s.bounds.center;    // != 0 solo si el pivot no esta centrado
            escala = Mathf.Min(1f,
                altoPantalla * altoRelativoPantalla / Mathf.Max(tamReal.y, 0.0001f),
                anchoPantalla * altoRelativoPantalla / Mathf.Max(tamReal.x, 0.0001f));
            cartaSr.sprite = s;

            texto.gameObject.SetActive(conTexto);
            texto.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            texto.rectTransform.sizeDelta = tamReal * (escala * ZonaTexto);
            if (fuenteReto != null) texto.font = fuenteReto;
            texto.color = colorTexto;
            texto.fontSizeMax = tamanoTexto;
            texto.fontSizeMin = tamanoTexto * 0.5f;

            AplicarGiro(1f);
        }

        // f: ancho relativo durante el giro (1 = de frente, 0 = de canto).
        private void AplicarGiro(float f)
        {
            cartaSr.transform.localScale = new Vector3(escala * f, escala, 1f);
            cartaSr.transform.localPosition = new Vector3(-centroSprite.x * escala * f, -centroSprite.y * escala, 0f);
            texto.transform.localScale = new Vector3(f, 1f, 1f);
        }

        // ===================== Construccion =====================
        private void ConstruirVisuales()
        {
            if (!TryGetComponent(out toque)) toque = gameObject.AddComponent<BoxCollider2D>();
            toque.size = new Vector2(500f, 500f);   // cubre toda la pantalla

            fondoSr = CrearSpriteRenderer("Fondo", ordenDibujo);
            fondoSr.sprite = SpriteBlanco();
            fondoSr.color = colorFondo;
            fondoSr.transform.localScale = new Vector3(500f, 500f, 1f);

            cartaSr = CrearSpriteRenderer("Carta", ordenDibujo + 1);

            var go = new GameObject("TextoReto");
            go.transform.SetParent(transform, false);
            texto = go.AddComponent<TextMeshPro>();
            texto.alignment = TextAlignmentOptions.Center;   // centrado horizontal y vertical en el rect
            texto.textWrappingMode = TextWrappingModes.Normal;
            texto.enableAutoSizing = true;                    // frases largas: se reduce hasta caber
            texto.fontStyle = FontStyles.Bold;
            texto.margin = Vector4.zero;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = CapaDibujo;
            mr.sortingOrder = ordenDibujo + 2;
        }

        private SpriteRenderer CrearSpriteRenderer(string nombre, int orden)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = CapaDibujo;
            sr.sortingOrder = orden;
            return sr;
        }

        private static Sprite SpriteBlanco()
        {
            if (spriteBlanco == null)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                spriteBlanco = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return spriteBlanco;
        }

        private void MostrarVisuales(bool v)
        {
            fondoSr.enabled = v;
            cartaSr.enabled = v;
            toque.enabled = v;
            if (!v) texto.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        private const string RutaFuenteReto = "Assets/TextMesh Pro/Fonts/LuckiestGuy-Regular SDF.asset";

        private void Reset()
        {
            if (fuenteReto == null)
                fuenteReto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuenteReto);
        }

        private static readonly (TipoCasilla tipo, string archivo)[] Mapa =
        {
            (TipoCasilla.Beber,   "beber"),
            (TipoCasilla.Evento,  "evento"),
            (TipoCasilla.Hot,     "hot"),
            (TipoCasilla.Reto,    "reto"),
            (TipoCasilla.Verdad,  "verdad"),
            (TipoCasilla.YoNunca, "yo nunca"),
        };

        [ContextMenu("Cargar imagenes + frases de ejemplo")]
        private void CargarDeDisco()
        {
            const string carpeta = "Assets/Art/Cartas";
            cartas = Mapa.Select(m =>
            {
                Entrada e = Obtener(m.tipo) ?? new Entrada { tipo = m.tipo };
                if (e.portada == null) e.portada = CargarSprite($"{carpeta}/{m.archivo} 1.png");
                if (e.plantilla == null) e.plantilla = CargarSprite($"{carpeta}/{m.archivo} 2.png");
                if (e.frases == null || e.frases.Length == 0)
                    e.frases = new[]
                    {
                        $"{m.tipo} · frase de ejemplo 1 (sustituir)",
                        $"{m.tipo} · frase de ejemplo 2 (sustituir)",
                        $"{m.tipo} · frase de ejemplo 3 (sustituir)",
                    };
                return e;
            }).ToArray();

            EditorUtility.SetDirty(this);
            Debug.Log("[CartaReto] Lista 'Cartas' rellenada con las 6 categorias.");
        }

        private static Sprite CargarSprite(string ruta) =>
            AssetDatabase.LoadAllAssetRepresentationsAtPath(ruta).OfType<Sprite>().FirstOrDefault()
            ?? AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
#endif
    }
}
