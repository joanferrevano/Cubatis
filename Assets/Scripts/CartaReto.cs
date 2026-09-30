using System;
using System.Collections;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        [Tooltip("Desplazamiento vertical del texto respecto al centro de la carta (unidades de mundo).")]
        public float offsetVerticalTexto = 0f;

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
            texto.transform.localPosition = new Vector3(0f, offsetVerticalTexto, -0.01f);
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

        // Nombre completo de portada y plantilla: las cartas del modo Pareja usan
        // "_1"/"_2" en vez del " 1"/" 2" de las clasicas.
        private static readonly (TipoCasilla tipo, string portada, string plantilla)[] Mapa =
        {
            (TipoCasilla.Beber,        "beber 1",          "beber 2"),
            (TipoCasilla.Evento,       "evento 1",         "evento 2"),
            (TipoCasilla.Hot,          "hot 1",            "hot 2"),
            (TipoCasilla.Reto,         "reto 1",           "reto 2"),
            (TipoCasilla.Verdad,       "verdad 1",         "verdad 2"),
            (TipoCasilla.YoNunca,      "yo nunca 1",       "yo nunca 2"),
            (TipoCasilla.Conocimiento, "conocimiento_1",   "conocimiento_2"),
            (TipoCasilla.Conexion,     "conexion_1",       "conexion_2"),
            (TipoCasilla.Confesion,    "confesion_1",      "confesion_2"),
            (TipoCasilla.RetoPareja,   "reto_pareja_1",    "reto_pareja_2"),
        };

        [ContextMenu("Cargar imagenes + frases de ejemplo")]
        private void CargarDeDisco()
        {
            const string carpeta = "Assets/Art/Cartas";
            cartas = Mapa.Select(m =>
            {
                Entrada e = Obtener(m.tipo) ?? new Entrada { tipo = m.tipo };
                if (e.portada == null) e.portada = CargarSprite($"{carpeta}/{m.portada}.png");
                if (e.plantilla == null) e.plantilla = CargarSprite($"{carpeta}/{m.plantilla}.png");
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
            Debug.Log($"[CartaReto] Lista 'Cartas' rellenada con las {Mapa.Length} categorias.");
        }

        private static Sprite CargarSprite(string ruta) =>
            AssetDatabase.LoadAllAssetRepresentationsAtPath(ruta).OfType<Sprite>().FirstOrDefault()
            ?? AssetDatabase.LoadAssetAtPath<Sprite>(ruta);

        // ===================== IMPORTAR FRASES DESDE CSV =====================
        private const string RutaFrasesCsv = "Assets/Boards/Cartas/frases.csv";
        private const string RutaFrasesParejaCsv = "Assets/Boards/Cartas/modo_pareja.csv";

        // Texto de CATEGORIA del CSV (normalizado: mayusculas, sin espacios, sin
        // acentos) -> TipoCasilla. Hace falta un alias explicito porque "Bebe"
        // (CSV) no coincide con el nombre del enum "Beber"; el resto son 1:1
        // pero se listan igual para dejar claro cual es el mapeo completo.
        private static readonly Dictionary<string, TipoCasilla> AliasCategoriaCsv = new Dictionary<string, TipoCasilla>
        {
            ["BEBE"] = TipoCasilla.Beber,
            ["BEBER"] = TipoCasilla.Beber,
            ["YONUNCA"] = TipoCasilla.YoNunca,
            ["VERDAD"] = TipoCasilla.Verdad,
            ["RETO"] = TipoCasilla.Reto,
            ["EVENTO"] = TipoCasilla.Evento,
            ["HOT"] = TipoCasilla.Hot,
        };

        // Tabla aparte para el CSV del modo Pareja: ahi "Reto" significa
        // RetoPareja, y con una tabla comun acabaria en el Reto clasico.
        private static readonly Dictionary<string, TipoCasilla> AliasCategoriaParejaCsv = new Dictionary<string, TipoCasilla>
        {
            ["CONOCIMIENTO"] = TipoCasilla.Conocimiento,
            ["CONEXION"] = TipoCasilla.Conexion,
            ["CONFESION"] = TipoCasilla.Confesion,
            ["RETO"] = TipoCasilla.RetoPareja,
            ["RETOPAREJA"] = TipoCasilla.RetoPareja,
        };

        /// <summary>
        /// Lee Assets/Boards/Cartas/frases.csv (columnas CATEGORIA, COLOR, FRASE;
        /// COLOR se ignora), agrupa por CATEGORIA y rellena 'frases' del elemento
        /// de 'cartas' cuyo Tipo coincida. Si el array todavia tiene los
        /// placeholders de CargarDeDisco ("... frase de ejemplo N (sustituir)")
        /// los sustituye enteros; si ya hay frases reales, anade las del CSV al
        /// final sin duplicar. No crea elementos nuevos en 'cartas': una
        /// categoria del CSV sin Tipo coincidente solo se avisa por consola.
        /// Accion manual (no corre sola ni en build): boton derecho en el
        /// componente CartaReto del Inspector > este item del menu contextual.
        /// </summary>
        [ContextMenu("Importar frases desde CSV (Assets/Boards/Cartas/frases.csv)")]
        private void ImportarFrasesDesdeCSV() => ImportarFrases(RutaFrasesCsv, 3, AliasCategoriaCsv);

        /// <summary>
        /// Igual que <see cref="ImportarFrasesDesdeCSV"/> pero para
        /// Assets/Boards/Cartas/modo_pareja.csv (columnas CATEGORIA, FRASE), que
        /// rellena solo las 4 categorias del modo Pareja. Las clasicas no se
        /// tocan porque su tabla de alias no las incluye.
        /// </summary>
        [ContextMenu("Importar frases modo Pareja desde CSV (Assets/Boards/Cartas/modo_pareja.csv)")]
        private void ImportarFrasesParejaDesdeCSV() => ImportarFrases(RutaFrasesParejaCsv, 2, AliasCategoriaParejaCsv);

        // 'columnas': CATEGORIA es la primera y FRASE la ultima; las de en medio
        // se ignoran.
        private void ImportarFrases(string rutaCsv, int columnas, Dictionary<string, TipoCasilla> alias)
        {
            if (!File.Exists(rutaCsv))
            {
                Debug.LogError($"[CartaReto] No se encontro el CSV en '{rutaCsv}'.");
                return;
            }

            // 1. Leer y agrupar por categoria, conservando el orden de aparicion
            // en el CSV.
            var frasesPorCategoria = new List<(string categoria, List<string> frases)>();
            var indicePorClave = new Dictionary<string, int>();

            string[] lineas = File.ReadAllLines(rutaCsv, Encoding.UTF8);
            for (int i = 1; i < lineas.Length; i++)   // salta la cabecera
            {
                string linea = lineas[i];
                if (string.IsNullOrWhiteSpace(linea)) continue;

                string[] campos = LeerCamposCsv(linea, columnas);
                if (campos == null)
                {
                    Debug.LogWarning($"[CartaReto] Linea {i + 1} de '{rutaCsv}' ignorada (formato inesperado): {linea}");
                    continue;
                }

                string categoria = campos[0];
                string frase = campos[columnas - 1];
                if (categoria.Length == 0 || frase.Length == 0) continue;

                string clave = NormalizarClaveCategoria(categoria);
                if (!indicePorClave.TryGetValue(clave, out int idx))
                {
                    idx = frasesPorCategoria.Count;
                    indicePorClave[clave] = idx;
                    frasesPorCategoria.Add((categoria, new List<string>()));
                }
                frasesPorCategoria[idx].frases.Add(frase);
            }

            // 2. Volcar cada categoria del CSV sobre el elemento de 'cartas' con
            // el mismo Tipo.
            var resumen = new List<string>();
            var sinCoincidencia = new List<string>();

            foreach (var (categoria, frasesCsv) in frasesPorCategoria)
            {
                string clave = NormalizarClaveCategoria(categoria);
                if (!alias.TryGetValue(clave, out TipoCasilla tipo))
                {
                    sinCoincidencia.Add(categoria);
                    continue;
                }

                Entrada entrada = Obtener(tipo);
                if (entrada == null)
                {
                    sinCoincidencia.Add($"{categoria} ({tipo}: sin elemento en 'Cartas')");
                    continue;
                }

                bool teniaPlaceholders = SonPlaceholders(entrada.frases);
                var actuales = teniaPlaceholders ? new List<string>() : new List<string>(entrada.frases);

                int anadidas = 0;
                foreach (string frase in frasesCsv)
                    if (!actuales.Contains(frase)) { actuales.Add(frase); anadidas++; }

                entrada.frases = actuales.ToArray();
                resumen.Add($"{tipo}: {anadidas} frase(s) importada(s), total {actuales.Count}" +
                    (teniaPlaceholders ? " (placeholders sustituidos)" : ""));
            }

            EditorUtility.SetDirty(this);

            // 3. Informe en consola.
            Debug.Log($"[CartaReto] Import de frases desde '{rutaCsv}' completado:\n" +
                (resumen.Count > 0 ? string.Join("\n", resumen) : "(ninguna categoria importada)"));
            if (sinCoincidencia.Count > 0)
                Debug.LogWarning("[CartaReto] Categorias del CSV sin Tipo coincidente en 'Cartas' (revisar a mano): " +
                    string.Join(", ", sinCoincidencia));
        }

        // Parte una linea en 'columnas' campos. Los campos pueden ir entre
        // comillas con "" como comilla escapada (modo_pareja.csv). La ultima
        // columna se queda con todo el resto de la linea, para que una FRASE
        // sin comillas pueda llevar comas (frases.csv). Null si faltan campos.
        private static string[] LeerCamposCsv(string linea, int columnas)
        {
            var campos = new string[columnas];
            int pos = 0;
            for (int c = 0; c < columnas; c++)
            {
                if (pos > linea.Length) return null;
                bool ultima = c == columnas - 1;
                int inicio = pos;
                while (inicio < linea.Length && linea[inicio] == ' ') inicio++;

                if (inicio < linea.Length && linea[inicio] == '"')
                {
                    var sb = new StringBuilder();
                    int j = inicio + 1;
                    while (true)
                    {
                        if (j >= linea.Length) return null;   // comilla sin cerrar
                        if (linea[j] == '"')
                        {
                            if (j + 1 < linea.Length && linea[j + 1] == '"') { sb.Append('"'); j += 2; continue; }
                            j++;
                            break;
                        }
                        sb.Append(linea[j++]);
                    }
                    campos[c] = sb.ToString().Trim();
                    int coma = linea.IndexOf(',', j);
                    pos = coma < 0 ? linea.Length + 1 : coma + 1;
                }
                else
                {
                    int coma = ultima ? -1 : linea.IndexOf(',', pos);
                    if (!ultima && coma < 0) return null;
                    int fin = coma < 0 ? linea.Length : coma;
                    campos[c] = linea.Substring(pos, fin - pos).Trim();
                    pos = fin + 1;
                }
            }
            return campos;
        }

        // Coincide con el formato exacto que genera CargarDeDisco: cualquier
        // frase que no tenga ese formato ya se considera "real" y no se borra.
        private static bool SonPlaceholders(string[] frases)
        {
            if (frases == null || frases.Length == 0) return true;
            foreach (string f in frases)
                if (string.IsNullOrEmpty(f) || !f.Contains("frase de ejemplo") || !f.Contains("(sustituir)"))
                    return false;
            return true;
        }

        // Mayusculas + sin espacios + sin acentos, para que "Bebe", " bebe ",
        // "Yo Nunca" / "YoNunca" etc. del CSV encajen con las claves de
        // AliasCategoriaCsv sin depender de como se haya tecleado el CSV.
        private static string NormalizarClaveCategoria(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            string descompuesto = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (char c in descompuesto)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
#endif
    }
}
