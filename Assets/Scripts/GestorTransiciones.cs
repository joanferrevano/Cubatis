using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Transicion de escena con fundido cruzado: la escena actual se desvanece
    /// sobre la nueva ya viva, sin pantalla intermedia. Punto unico para
    /// cambiar de escena en el juego: <see cref="CargarEscenaConFade"/>
    /// sustituye a SceneManager.LoadScene.
    ///
    /// Como funciona: se hace una foto de la pantalla y se pone encima de todo
    /// a opacidad 100%; se carga la escena nueva de forma aditiva por debajo, y
    /// la foto baja a 0% revelandola. La foto se saca con ScreenCapture (el
    /// backbuffer ya compuesto) y no con una camara a RenderTexture porque toda
    /// la UI del juego esta en Canvas Overlay, que ninguna camara renderiza.
    ///
    /// Tener dos escenas cargadas a la vez choca con como estan montadas (cada
    /// una trae su Main Camera, EventSystem, AudioListener y Canvas), asi que
    /// en cuanto la foto tapa la pantalla se desactivan las raices de la escena
    /// vieja: no hay duplicados, Camera.main en los Awake de la nueva
    /// (FondoCubreCamara, BurbujasAmbiente) encuentra la camara correcta y la
    /// vieja deja de ejecutar logica. Ademas la nueva pasa a ser la escena
    /// activa antes de sus Start: los objetos raiz creados por codigo (p. ej.
    /// las fichas de GestorPartida) van a la escena activa, y si fuera la vieja
    /// desaparecerian al descargarla.
    ///
    /// A diferencia de <see cref="Jugadores"/> y <see cref="DatosPartida"/>
    /// (clases estaticas: solo datos que leer), esto SI necesita un GameObject
    /// con DontDestroyOnLoad: la foto y la corrutina tienen que seguir vivas
    /// durante la carga y despues de ella. Es justo la excepcion que describe
    /// DatosPartida. Aun asi se usa como si fuera estatico: el objeto se crea
    /// solo la primera vez que hace falta, sin montar nada en ninguna escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class GestorTransiciones : MonoBehaviour
    {
        private const float Duracion = 0.28f;
        /// <summary>Por encima de cualquier otro Canvas del juego.</summary>
        private const int OrdenCanvas = 32000;

        private static GestorTransiciones instancia;

        private Canvas canvas;
        private CanvasGroup grupo;
        private RawImage captura;
        private bool enCurso;

        /// <summary>
        /// Foto de la escena actual, carga aditiva de 'nombreEscena', fundido
        /// cruzado y descarga de la anterior. Mientras dura, la foto bloquea
        /// los toques (no se puede pulsar dos veces un boton ni tirar el dado)
        /// y se ignoran otras peticiones.
        /// </summary>
        public static void CargarEscenaConFade(string nombreEscena)
        {
            if (!Application.CanStreamedLevelBeLoaded(nombreEscena))
            {
                Debug.LogError($"[GestorTransiciones] La escena '{nombreEscena}' no existe o no esta en Build Settings.");
                return;
            }
            var gestor = Instancia();
            if (gestor.enCurso) return;
            gestor.StartCoroutine(gestor.Transicion(nombreEscena));
        }

        private static GestorTransiciones Instancia()
        {
            if (instancia != null) return instancia;

            var go = new GameObject("GestorTransiciones");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<GestorTransiciones>();
            instancia.Construir();
            return instancia;
        }

        private void Construir()
        {
            // Mismo montaje de Canvas que el resto de la UI del proyecto.
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OrdenCanvas;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            // El raycaster hace que la foto se trague los toques mientras se ve.
            gameObject.AddComponent<GraphicRaycaster>();
            grupo = gameObject.AddComponent<CanvasGroup>();

            var go = new GameObject("Captura", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            captura = go.GetComponent<RawImage>();

            Mostrar(false);
        }

        private IEnumerator Transicion(string nombreEscena)
        {
            enCurso = true;

            // 1. Foto de lo que se ve ahora mismo (con la UI Overlay incluida).
            yield return new WaitForEndOfFrame();
            Texture2D foto = ScreenCapture.CaptureScreenshotAsTexture();
            captura.texture = foto;
            Mostrar(true);
            grupo.alpha = 1f;

            // 2. La foto ya tapa la pantalla: se apaga la escena vieja entera
            //    para que no haya dos camaras/EventSystems/AudioListeners ni
            //    logica corriendo por debajo.
            Scene vieja = SceneManager.GetActiveScene();
            foreach (var raiz in vieja.GetRootGameObjects()) raiz.SetActive(false);

            // 3. Carga aditiva. sceneLoaded llega despues de los Awake/OnEnable
            //    de la escena nueva pero antes de sus Start: ahi se hace activa.
            Scene nueva = default;
            UnityAction<Scene, LoadSceneMode> alCargar = (escena, modo) =>
            {
                if (modo != LoadSceneMode.Additive || nueva.IsValid()) return;
                if (escena.name != nombreEscena && escena.path != nombreEscena) return;
                nueva = escena;
                SceneManager.SetActiveScene(escena);
            };
            SceneManager.sceneLoaded += alCargar;
            var carga = SceneManager.LoadSceneAsync(nombreEscena, LoadSceneMode.Additive);
            while (!carga.isDone) yield return null;
            SceneManager.sceneLoaded -= alCargar;
            if (!nueva.IsValid())
            {
                nueva = SceneManager.GetSceneByName(nombreEscena);
                if (nueva.IsValid()) SceneManager.SetActiveScene(nueva);
            }

            // Un frame mas bajo la foto: deja pasar los Start de la escena
            // nueva (encuadre de camara, prewarm de burbujas...) antes de verla.
            yield return null;

            // 4. Fundido cruzado: la foto de la vieja baja a 0% sobre la nueva.
            yield return Fundido(1f, 0f);
            Mostrar(false);
            captura.texture = null;
            Destroy(foto);

            // 5. Descarga de la vieja. Un LoadScene normal liberaba ademas los
            //    assets que ya no usa nadie; con carga aditiva hay que pedirlo.
            if (vieja.IsValid() && vieja.isLoaded) yield return SceneManager.UnloadSceneAsync(vieja);
            yield return Resources.UnloadUnusedAssets();

            enCurso = false;
        }

        private IEnumerator Fundido(float desde, float hasta)
        {
            for (float t = 0f; t < Duracion; t += Time.unscaledDeltaTime)
            {
                grupo.alpha = Mathf.Lerp(desde, hasta, EaseInOut(t / Duracion));
                yield return null;
            }
            grupo.alpha = hasta;
        }

        /// <summary>Cubica ease-in-out: arranca y frena suave.</summary>
        private static float EaseInOut(float x) =>
            x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

        /// <summary>
        /// Oculto se desactiva el Canvas entero: no se dibuja ni intercepta
        /// toques entre transiciones.
        /// </summary>
        private void Mostrar(bool visible)
        {
            canvas.enabled = visible;
            grupo.blocksRaycasts = visible;
            if (!visible) grupo.alpha = 0f;
        }
    }
}
