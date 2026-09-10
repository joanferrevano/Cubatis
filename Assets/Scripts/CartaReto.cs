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

            [Header("Ajuste propio de esta categoria (lo escribe el boton del Inspector)")]
            [Tooltip("Si esta marcado, esta categoria usa SUS valores; si no, los globales.")]
            public bool ajustePropio;
            [Tooltip("Desplazamiento de la carta como fraccion del alto de pantalla.")]
            public Vector2 offsetCarta;
            [Tooltip("Multiplicador sobre la escala base de la carta (1 = igual que las demas).")]
            public float escalaCarta = 1f;
            [Tooltip("Offset del texto respecto al centro de la carta, fraccion (ancho, alto).")]
            public Vector2 offsetTexto;
            [Tooltip("Zona de texto como fraccion (ancho, alto) de la carta.")]
            public Vector2 zonaTexto;
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
        [Tooltip("Zona de texto POR DEFECTO (fraccion ancho, alto de la carta). Cada categoria puede sobreescribirla con 'ajuste propio'.")]
        [SerializeField] private Vector2 zonaTexto = new Vector2(0.82f, 0.48f);
        [Tooltip("Offset de texto POR DEFECTO (fraccion). Positivo en Y = hacia arriba. Cada categoria puede sobreescribirlo.")]
        [SerializeField] private Vector2 offsetTexto = new Vector2(0f, 0.14f);

        [Header("Animacion de giro")]
        [SerializeField] private float duracionFlip = 0.28f;

        [Header("Dibujo")]
        [Tooltip("Por encima del dado (1000) y de las fichas (500).")]
        [SerializeField] private int ordenDibujo = 2000;
        [SerializeField] private string sortingLayer = "Default";

        [Header("Vista previa (solo Editor)")]
        [Tooltip("Categoria que se muestra con el boton 'Activar vista previa' del Inspector.")]
        [SerializeField] private TipoCasilla categoriaVistaPrevia = TipoCasilla.Beber;
        [SerializeField, HideInInspector] private bool vistaPreviaActiva;

        private enum Estado { Cerrada, Portada, Reto }

        private Estado estado = Estado.Cerrada;
        private bool animando;
        private TipoCasilla tipoActual;
        private Action alCerrar;

        private SpriteRenderer fondoSr;
        private SpriteRenderer cartaSr;
        private TextMeshPro texto;
        private BoxCollider2D toque;
        private float escalaBase = 1f;          // escala que ocuparia 'altoRelativoPantalla' (sin mult. de categoria)
        private float escalaCartaActual = 1f;   // escalaBase * escalaCarta de la categoria activa (para el flip)
        private static Sprite spriteBlanco;

        public bool Abierta => estado != Estado.Cerrada;

        private void Awake()
        {
            vistaPreviaActiva = false;   // la vista previa es solo de edicion
            if (!EnGameObjectDedicado()) { enabled = false; return; }
            GarantizarVisuales();
            OcultarTodo();
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

            tipoActual = tipo;
            alCerrar = alCerrarCallback;
            estado = Estado.Portada;
            animando = false;

            Colocar();
            cartaSr.sprite = e.portada;
            AplicarLayout(e);
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
            Entrada e = Obtener(tipoActual);

            float m = Mathf.Max(0.01f, duracionFlip * 0.5f);
            yield return EscalarX(1f, 0f, m);

            cartaSr.sprite = e.plantilla;
            AplicarLayout(e);
            cartaSr.transform.localScale = new Vector3(0f, escalaCartaActual, 1f);
            texto.text = FraseAlAzar(tipoActual);
            texto.gameObject.SetActive(true);

            yield return EscalarX(0f, 1f, m);

            estado = Estado.Reto;
            animando = false;
        }

        // Escala X de la carta entre 'desde'..'hasta' (el flip); Y fija a escalaCartaActual.
        private IEnumerator EscalarX(float desde, float hasta, float dur)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float f = Mathf.Lerp(desde, hasta, Mathf.Clamp01(t));
                cartaSr.transform.localScale = new Vector3(escalaCartaActual * f, escalaCartaActual, 1f);
                yield return null;
            }
            cartaSr.transform.localScale = new Vector3(escalaCartaActual * hasta, escalaCartaActual, 1f);
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
        // Reutiliza los hijos ya existentes (p.ej. tras recompilar con la vista
        // previa activa) o los reconstruye si falta alguno.
        private void GarantizarVisuales()
        {
            if (cartaSr != null && fondoSr != null && texto != null) return;

            fondoSr = HijoSpriteRenderer("Fondo");
            cartaSr = HijoSpriteRenderer("Carta");
            Transform t = transform.Find("TextoReto");
            texto = t != null ? t.GetComponent<TextMeshPro>() : null;
            TryGetComponent(out toque);

            if (fondoSr != null && cartaSr != null && texto != null && toque != null) return;

            DestruirVisuales();
            ConstruirVisuales();
        }

        private SpriteRenderer HijoSpriteRenderer(string nombre)
        {
            Transform t = transform.Find(nombre);
            return t != null ? t.GetComponent<SpriteRenderer>() : null;
        }

        private void DestruirVisuales()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform h = transform.GetChild(i);
                if (h.name == "Fondo" || h.name == "Carta" || h.name == "TextoReto")
                {
                    if (Application.isPlaying) Destroy(h.gameObject);
                    else DestroyImmediate(h.gameObject);
                }
            }
            fondoSr = null; cartaSr = null; texto = null;
            if (toque != null) toque.enabled = false;
        }

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

        // orthographicSize que la Main Camera tendra realmente en Play. En Play se
        // lee directo (AjusteCamaraTablero ya lo dejo bien). En el Editor sin Play
        // NO se puede fiar de camara.orthographicSize (la ventana Scene/editor
        // impone otro aspect): se recalcula con el aspect de la ventana Game y la
        // misma logica de AjusteCamaraTablero, para que vista previa == Play.
        private float OrtoEfectivo(Camera cam)
        {
            if (cam == null || !cam.orthographic) return 5f;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var aj = FindAnyObjectByType<AjusteCamaraTablero>();
                Vector2 gv = UnityEditor.Handles.GetMainGameViewSize();
                if (aj != null && gv.x > 0f && gv.y > 0f)
                    return aj.TamanoOrtografico(gv.x / gv.y);
            }
#endif
            return cam.orthographicSize;
        }

        // Escala uniforme que hace que la carta ocupe 'altoRelativoPantalla' de la
        // pantalla real de Play (mismo orthographicSize que en Play, via OrtoEfectivo).
        private float CalcularEscalaBase()
        {
            Sprite s = cartaSr != null ? cartaSr.sprite : null;
            if (s == null) return escalaBase;
            float altoCartaObjetivo = OrtoEfectivo(Camera.main) * 2f * altoRelativoPantalla;
            return altoCartaObjetivo / Mathf.Max(0.0001f, s.rect.height / s.pixelsPerUnit);
        }

        // Valores a aplicar para 'e': los suyos si tiene 'ajuste propio', si no los globales.
        private void ValoresDe(Entrada e, out Vector2 offCarta, out float mulCarta,
                               out Vector2 offTexto, out Vector2 zonTexto)
        {
            bool propio = e != null && e.ajustePropio;
            offCarta = propio ? e.offsetCarta : Vector2.zero;
            mulCarta = propio && e.escalaCarta > 0.001f ? e.escalaCarta : 1f;
            offTexto = propio ? e.offsetTexto : offsetTexto;
            zonTexto = propio && e.zonaTexto.x > 0.001f && e.zonaTexto.y > 0.001f ? e.zonaTexto : zonaTexto;
        }

        // Coloca y escala la Carta y el TextoReto para 'e'. NO toca la raiz (eso es Colocar()).
        // Fuente unica de la geometria: la usan Play y la vista previa por igual.
        private void AplicarLayout(Entrada e)
        {
            Sprite s = cartaSr != null ? cartaSr.sprite : null;
            if (s == null || texto == null) return;

            ValoresDe(e, out Vector2 offCarta, out float mulCarta, out Vector2 offTexto, out Vector2 zonTexto);

            float altoPantalla = OrtoEfectivo(Camera.main) * 2f;
            escalaBase = CalcularEscalaBase();
            escalaCartaActual = escalaBase * mulCarta;

            float anchoCarta = (s.rect.width / s.pixelsPerUnit) * escalaCartaActual;
            float altoCarta = (s.rect.height / s.pixelsPerUnit) * escalaCartaActual;

            Vector3 cartaLP = new Vector3(offCarta.x * altoPantalla, offCarta.y * altoPantalla, 0f);
            cartaSr.transform.localPosition = cartaLP;
            cartaSr.transform.localScale = new Vector3(escalaCartaActual, escalaCartaActual, 1f);

            texto.transform.localPosition = cartaLP + new Vector3(offTexto.x * anchoCarta, offTexto.y * altoCarta, -0.01f);
            texto.rectTransform.sizeDelta = new Vector2(anchoCarta * zonTexto.x, altoCarta * zonTexto.y);
            texto.alignment = TextAlignmentOptions.Center;
            texto.fontSize = tamanoTexto;
            texto.color = colorTexto;
            if (fuenteReto != null && texto.font != fuenteReto) texto.font = fuenteReto;
            texto.ForceMeshUpdate();   // geometria/centrado identicos en Editor y en Play
        }

        // Vista Scene: recuadro blanco = borde de la carta, cian = zona de texto.
        private void OnDrawGizmos()
        {
            // Carta visible (Play o vista previa): dibuja lo que hay AHORA, sin recalcular.
            if (cartaSr != null && cartaSr.enabled && cartaSr.sprite != null)
            {
                Bounds cb = cartaSr.bounds;
                Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
                Gizmos.DrawWireCube(cb.center, cb.size);

                if (texto != null && texto.gameObject.activeInHierarchy)
                {
                    Vector2 sd = texto.rectTransform.sizeDelta;
                    Vector3 tam = new Vector3(sd.x, sd.y, 0f);
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(texto.transform.position, tam);
                    Gizmos.color = new Color(0f, 1f, 1f, 0.12f);
                    Gizmos.DrawCube(texto.transform.position, tam);
                }
                return;
            }

            // Sin visuales: dibuja desde los datos de la categoria de vista previa.
            Sprite s = SpritePreview();
            if (s == null) return;
            ValoresDe(EntradaVistaPrevia(), out Vector2 offCarta, out float mulCarta,
                      out Vector2 offTexto, out Vector2 zonTexto);

            Camera cam = Camera.main;
            float altoPantalla = OrtoEfectivo(cam) * 2f;
            float esc = altoPantalla * altoRelativoPantalla
                        / Mathf.Max(0.0001f, s.rect.height / s.pixelsPerUnit) * mulCarta;
            float anchoCarta = (s.rect.width / s.pixelsPerUnit) * esc;
            float altoCarta = (s.rect.height / s.pixelsPerUnit) * esc;
            Vector3 raiz = (cam != null ? cam.transform.position + cam.transform.forward * 9f : transform.position) + offsetMundo;
            Vector3 centroCarta = raiz + new Vector3(offCarta.x * altoPantalla, offCarta.y * altoPantalla, 0f);
            Vector3 centroTexto = centroCarta + new Vector3(offTexto.x * anchoCarta, offTexto.y * altoCarta, -0.01f);
            Vector3 tamTexto = new Vector3(anchoCarta * zonTexto.x, altoCarta * zonTexto.y, 0f);

            Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
            Gizmos.DrawWireCube(centroCarta, new Vector3(anchoCarta, altoCarta, 0f));
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(centroTexto, tamTexto);
            Gizmos.color = new Color(0f, 1f, 1f, 0.12f);
            Gizmos.DrawCube(centroTexto, tamTexto);
        }

        private Sprite SpritePreview()
        {
            Entrada e = EntradaVistaPrevia();
            if (e != null && e.plantilla != null) return e.plantilla;
            if (e != null && e.portada != null) return e.portada;
            if (cartas != null)
                foreach (Entrada x in cartas)
                    if (x != null && (x.plantilla != null || x.portada != null))
                        return x.plantilla != null ? x.plantilla : x.portada;
            return null;
        }

        private Entrada EntradaVistaPrevia()
        {
            Entrada e = Obtener(categoriaVistaPrevia);
            if (e != null && e.plantilla != null) return e;
            if (cartas != null)
                foreach (Entrada x in cartas)
                    if (x != null && x.plantilla != null) return x;
            return e;
        }

#if UNITY_EDITOR
        // ===================== Vista previa en Editor =====================
        // Muestra la carta (Fondo + Carta + TextoReto) sin entrar en Play para
        // poder ajustar 'Zona Texto' y 'Offset Texto' viendo el resultado real.
        // Los hijos se marcan DontSave: no ensucian la escena; lo unico que se
        // guarda son los campos serializados (que es justo lo que se ajusta).
        public bool VistaPreviaActiva => vistaPreviaActiva;

        public void ActivarVistaPrevia()
        {
            if (Application.isPlaying) return;
            if (!EnGameObjectDedicado()) { vistaPreviaActiva = false; return; }
            vistaPreviaActiva = true;
            RefrescarVistaPrevia(true);
        }

        public void DesactivarVistaPrevia()
        {
            vistaPreviaActiva = false;
            if (!Application.isPlaying)
            {
                DestruirVisuales();
                UnityEditor.SceneView.RepaintAll();
            }
        }

        public void RefrescarVistaPrevia() => RefrescarVistaPrevia(true);

        /// <param name="reposicionar">
        /// true = recalcula posicion/escala de Carta y TextoReto desde los valores
        /// de la categoria (al cambiar de categoria o de campo). false = solo
        /// re-muestra SIN tocar transforms, para no pisar un arrastre manual del
        /// gizmo (p.ej. al re-seleccionar el objeto en el Inspector).</param>
        public void RefrescarVistaPrevia(bool reposicionar)
        {
            if (Application.isPlaying || !vistaPreviaActiva) return;

            GarantizarVisuales();

            Entrada e = EntradaVistaPrevia();
            if (e == null || e.plantilla == null) { DestruirVisuales(); return; }

            // DontSaveInEditor (no DontSave completo): asi NO lleva NotEditable y
            // Carta/TextoReto se pueden mover a mano con el gizmo en la vista Scene.
            HideFlags f = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            fondoSr.gameObject.hideFlags = f;
            cartaSr.gameObject.hideFlags = f;
            texto.gameObject.hideFlags = f;

            cartaSr.sprite = e.plantilla;
            if (string.IsNullOrEmpty(texto.text))
                texto.text = (e.frases != null && e.frases.Length > 0 && !string.IsNullOrEmpty(e.frases[0]))
                    ? e.frases[0]
                    : "Frase de ejemplo del reto para ajustar la zona de texto.";

            // Recoloca si se pide (cambio de campo/categoria) o si acaba de construirse.
            if (reposicionar || texto.transform.localPosition == Vector3.zero)
            {
                Colocar();
                AplicarLayout(e);
            }
            else
            {
                escalaBase = CalcularEscalaBase();
                escalaCartaActual = cartaSr.transform.localScale.x;
            }

            texto.gameObject.SetActive(true);
            MostrarVisuales(true);
            estado = Estado.Reto;

            UnityEditor.SceneView.RepaintAll();
        }

        // Invierte AplicarLayout: lee donde/como estan AHORA la Carta y el TextoReto
        // (tras moverlos a mano con el gizmo) y escribe en la ENTRADA de la categoria
        // de vista previa los valores que reproducen exactamente eso. A partir de ahi
        // calculado == visual, y esa categoria usa su ajuste propio.
        public void FijarAjusteDeCategoria()
        {
            if (Application.isPlaying || !vistaPreviaActiva ||
                cartaSr == null || cartaSr.sprite == null || texto == null)
            {
                Debug.LogWarning("[CartaReto] Activa la vista previa (con la carta visible) antes de fijar.", this);
                return;
            }
            Entrada e = EntradaVistaPrevia();
            if (e == null) { Debug.LogWarning("[CartaReto] 'Categoria Vista Previa' no tiene una entrada valida.", this); return; }

            Sprite s = cartaSr.sprite;
            float altoPantalla = OrtoEfectivo(Camera.main) * 2f;
            escalaBase = CalcularEscalaBase();
            if (escalaBase < 1e-6f || altoPantalla < 1e-4f) return;

            // --- Carta: tal como la dejo el usuario ---
            float escCartaAhora = cartaSr.transform.localScale.x;
            float mulCarta = escCartaAhora / escalaBase;
            Vector3 cartaLP = cartaSr.transform.localPosition;
            Vector2 offCarta = new Vector2(cartaLP.x / altoPantalla, cartaLP.y / altoPantalla);

            float anchoCarta = (s.rect.width / s.pixelsPerUnit) * escCartaAhora;
            float altoCarta = (s.rect.height / s.pixelsPerUnit) * escCartaAhora;
            if (anchoCarta < 1e-4f || altoCarta < 1e-4f) return;

            // --- Texto: relativo al centro real de la carta ---
            Vector3 relTexto = texto.transform.position - cartaSr.transform.position;
            Vector2 sd = texto.rectTransform.sizeDelta;
            if (sd.x < 1e-4f || sd.y < 1e-4f) sd = new Vector2(anchoCarta * 0.8f, altoCarta * 0.5f);

            UnityEditor.Undo.RecordObject(this, "Fijar ajuste de carta de la categoria");
            e.ajustePropio = true;
            e.escalaCarta = Mathf.Max(0.01f, mulCarta);
            e.offsetCarta = offCarta;
            e.offsetTexto = new Vector2(relTexto.x / anchoCarta, relTexto.y / altoCarta);
            e.zonaTexto = new Vector2(Mathf.Abs(sd.x) / anchoCarta, Mathf.Abs(sd.y) / altoCarta);
            UnityEditor.EditorUtility.SetDirty(this);

            AplicarLayout(e);   // reasienta con los valores nuevos: queda clavado donde estaba
            UnityEditor.SceneView.RepaintAll();
            Debug.Log($"[CartaReto] {e.tipo}: escalaCarta {e.escalaCarta:0.###}, offsetCarta {e.offsetCarta}, " +
                      $"offsetTexto {e.offsetTexto}, zonaTexto {e.zonaTexto}");
        }

        private void OnValidate()
        {
            if (Application.isPlaying || !vistaPreviaActiva) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) RefrescarVistaPrevia();
            };
        }
#endif

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
                if (e.escalaCarta <= 0f) e.escalaCarta = 1f;
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
