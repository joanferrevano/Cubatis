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
    /// Los datos (2 imagenes + frases por categoria) se editan aqui mismo en el
    /// Inspector, en la lista "Cartas". Menu contextual del componente:
    /// "Cargar imagenes + frases de ejemplo" hace el primer relleno leyendo
    /// Assets/Art/Cartas.
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

        [Header("Tamano y colocacion")]
        [Range(0.3f, 0.95f)]
        [SerializeField] private float altoRelativoPantalla = 0.72f;
        [SerializeField] private Vector3 offsetMundo = Vector3.zero;

        [Header("Fondo oscuro")]
        [SerializeField] private Color colorFondo = new Color(0f, 0f, 0f, 0.78f);

        [Header("Texto del reto (sobre la plantilla)")]
        [Tooltip("Font Asset TMP para la frase del reto. Vacio = fuente por defecto de TMP.")]
        [SerializeField] private TMP_FontAsset fuenteReto;
        [SerializeField] private float tamanoTexto = 3.2f;
        [SerializeField] private Color colorTexto = Color.black;
        [Tooltip("Zona de texto (ancho, alto) como fraccion de la carta: el hueco blanco del bocadillo.")]
        [SerializeField] private Vector2 zonaTexto = new Vector2(0.82f, 0.48f);
        [Tooltip("Desplazamiento del centro del texto respecto al centro de la carta, como fraccion (x ancho, y alto). Positivo en Y = hacia arriba, para centrarlo en el bocadillo.")]
        [SerializeField] private Vector2 offsetTexto = new Vector2(0f, 0.14f);

        [Header("Animacion de giro")]
        [SerializeField] private float duracionFlip = 0.28f;

        [Header("Dibujo")]
        [Tooltip("Por encima del dado (1000) y de las fichas (500).")]
        [SerializeField] private int ordenDibujo = 2000;
        [SerializeField] private string sortingLayer = "Default";

        private enum Estado { Cerrada, Portada, Reto }

        private Estado estado = Estado.Cerrada;
        private bool animando;
        private TipoCasilla tipoActual;
        private Action alCerrar;

        private SpriteRenderer fondoSr;
        private SpriteRenderer cartaSr;
        private TextMeshPro texto;
        private BoxCollider2D toque;
        private float escalaBase = 1f;
        private static Sprite spriteBlanco;

        public bool Abierta => estado != Estado.Cerrada;

        private void Awake()
        {
            ConstruirVisuales();
            OcultarTodo();
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

            tipoActual = tipo;
            alCerrar = alCerrarCallback;
            estado = Estado.Portada;
            animando = false;

            Colocar();
            cartaSr.sprite = e.portada;
            AjustarEscalaCarta();
            texto.gameObject.SetActive(false);
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

            float m = Mathf.Max(0.01f, duracionFlip * 0.5f);
            yield return EscalarX(1f, 0f, m);

            cartaSr.sprite = Obtener(tipoActual).plantilla;
            AjustarEscalaCarta();
            cartaSr.transform.localScale = new Vector3(0f, escalaBase, 1f);
            texto.text = FraseAlAzar(tipoActual);
            ColocarTexto();
            texto.gameObject.SetActive(true);

            yield return EscalarX(0f, 1f, m);

            estado = Estado.Reto;
            animando = false;
        }

        private IEnumerator EscalarX(float desde, float hasta, float dur)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float f = Mathf.Lerp(desde, hasta, Mathf.Clamp01(t));
                cartaSr.transform.localScale = new Vector3(escalaBase * f, escalaBase, 1f);
                yield return null;
            }
            cartaSr.transform.localScale = new Vector3(escalaBase * hasta, escalaBase, 1f);
        }

        private void Cerrar()
        {
            estado = Estado.Cerrada;
            OcultarTodo();
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

        private string FraseAlAzar(TipoCasilla tipo)
        {
            Entrada e = Obtener(tipo);
            if (e == null || e.frases == null || e.frases.Length == 0)
                return $"(sin frases para {tipo})";
            return e.frases[UnityEngine.Random.Range(0, e.frases.Length)];
        }

        // ===================== Construccion / colocacion =====================
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
            if (fuenteReto != null) texto.font = fuenteReto;
            texto.alignment = TextAlignmentOptions.Center;   // centrado horizontal y vertical en el rect
            texto.fontStyle = FontStyles.Bold;
            texto.color = colorTexto;
            texto.fontSize = tamanoTexto;
            texto.margin = Vector4.zero;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;
            mr.sortingOrder = ordenDibujo + 2;
        }

        private SpriteRenderer CrearSpriteRenderer(string nombre, int orden)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;
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

        private void Colocar()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.position = cam.transform.position + cam.transform.forward * 9f + offsetMundo;
        }

        private void AjustarEscalaCarta()
        {
            Camera cam = Camera.main;
            float altoPantalla = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 10f;
            float altoCarta = altoPantalla * altoRelativoPantalla;

            Sprite s = cartaSr.sprite;
            float altoSpriteMundo = s.rect.height / s.pixelsPerUnit;
            escalaBase = altoCarta / Mathf.Max(0.0001f, altoSpriteMundo);
            cartaSr.transform.localScale = new Vector3(escalaBase, escalaBase, 1f);
            ColocarTexto();
        }

        private void ColocarTexto()
        {
            Sprite s = cartaSr.sprite;
            if (s == null) return;
            float anchoCarta = (s.rect.width / s.pixelsPerUnit) * escalaBase;
            float altoCarta = (s.rect.height / s.pixelsPerUnit) * escalaBase;

            texto.transform.localPosition = new Vector3(offsetTexto.x * anchoCarta, offsetTexto.y * altoCarta, -0.01f);
            texto.rectTransform.sizeDelta = new Vector2(anchoCarta * zonaTexto.x, altoCarta * zonaTexto.y);
            texto.alignment = TextAlignmentOptions.Center;
            texto.fontSize = tamanoTexto;
            texto.color = colorTexto;
            if (fuenteReto != null && texto.font != fuenteReto) texto.font = fuenteReto;
        }

        private void MostrarVisuales(bool v)
        {
            fondoSr.enabled = v;
            cartaSr.enabled = v;
            toque.enabled = v;
            if (!v && texto != null) texto.gameObject.SetActive(false);
        }

        private void OcultarTodo()
        {
            estado = Estado.Cerrada;
            MostrarVisuales(false);
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
