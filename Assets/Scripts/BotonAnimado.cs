using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Micro-animacion de "squash" al pulsar: encoge el objeto al pulsar y lo
    /// devuelve a su escala original al soltar. Solo toca localScale, no sabe
    /// nada del onClick ni de la logica del boton, asi que se puede anadir a
    /// cualquier Button (o al Dado, que es un Image con IPointerClickHandler).
    ///
    /// Usa IPointerDown/Up en vez de onClick porque los botones que cambian de
    /// escena lo hacen en el mismo frame del click: la respuesta visual tiene
    /// que llegar mientras el dedo sigue apoyado. No implementa ningun handler
    /// de drag a proposito: si lo hiciera, robaria el arrastre al ScrollRect
    /// padre (tarjetas de ModosJuegos). Al empezar un scroll el input module ya
    /// manda un PointerUp al boton pulsado, y eso basta para que vuelva.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotonAnimado : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Escala relativa mientras esta pulsado (0.9-0.95 recomendado).")]
        [Range(0.5f, 1f)]
        [SerializeField] private float escalaPulsado = 0.92f;
        [Tooltip("Segundos en encoger al pulsar.")]
        [SerializeField] private float duracionBajada = 0.07f;
        [Tooltip("Segundos en volver a la escala original al soltar.")]
        [SerializeField] private float duracionSubida = 0.12f;
        [Tooltip("Encoge hacia el centro visual aunque el pivot este en un borde (tarjetas, EMPEZAR, botones X).")]
        [SerializeField] private bool desdeCentro = true;

        private Vector3 escalaBase;
        private Selectable selectable;
        private Dado dado;
        private RectTransform rt;
        private Vector3 desplazamientoAplicado;

        private float progreso;         // 0 = reposo, 1 = encogido del todo
        private bool pulsado;
        private bool dentro;
        /// <summary>
        /// Un toque rapido puede bajar y subir en el mismo frame y la animacion
        /// no llegaria a verse. Mientras esto este activo se termina de encoger
        /// antes de volver, aunque el dedo ya se haya levantado.
        /// </summary>
        private bool completarBajada;

        private void Awake()
        {
            escalaBase = transform.localScale;
            selectable = GetComponent<Selectable>();
            dado = GetComponent<Dado>();
            rt = transform as RectTransform;
        }

        private void OnDisable()
        {
            // Si se oculta a media pulsacion (popup que se cierra, cambio de
            // panel) no debe reaparecer encogido.
            pulsado = dentro = completarBajada = false;
            progreso = 0f;
            AplicarEscala(1f);
        }

        public void OnPointerDown(PointerEventData datos)
        {
            if (datos.button != PointerEventData.InputButton.Left || !AceptaPulsacion()) return;
            pulsado = dentro = completarBajada = true;
        }

        public void OnPointerUp(PointerEventData datos)
        {
            if (datos.button != PointerEventData.InputButton.Left) return;
            pulsado = false;
        }

        // Mismo criterio que el tinte del Button: si el dedo sale del boton sin
        // soltar, deja de verse pulsado; si vuelve a entrar, se vuelve a encoger.
        // El exit no cancela completarBajada: en tactil llega en el mismo frame
        // que el PointerUp al levantar el dedo y se comeria los toques rapidos.
        public void OnPointerEnter(PointerEventData _) => dentro = true;
        public void OnPointerExit(PointerEventData _) => dentro = false;

        /// <summary>
        /// No encoge si el boton no responderia: Button no interactuable (o
        /// CanvasGroup que lo bloquea) o dado fuera de turno / girando.
        /// </summary>
        private bool AceptaPulsacion()
        {
            if (selectable != null && !selectable.IsInteractable()) return false;
            if (dado != null && (!dado.PuedeTirar || dado.Girando)) return false;
            return true;
        }

        private void Update()
        {
            float objetivo = (pulsado && dentro) || completarBajada ? 1f : 0f;
            if (Mathf.Approximately(progreso, objetivo)) return;

            float duracion = objetivo > progreso ? duracionBajada : duracionSubida;
            // Tiempo sin escalar: la animacion no debe depender de Time.timeScale.
            progreso = Mathf.MoveTowards(progreso, objetivo, Time.unscaledDeltaTime / Mathf.Max(0.0001f, duracion));
            if (progreso >= 1f) completarBajada = false;

            float k = Mathf.SmoothStep(0f, 1f, progreso);
            AplicarEscala(Mathf.Lerp(1f, escalaPulsado, k));
        }

        /// <summary>
        /// localScale siempre escala respecto al pivot. Para encoger desde el
        /// centro sin tocar pivots (moverian el layout de la escena) se corrige
        /// la posicion lo justo para que el centro no se mueva. Se aplica como
        /// delta sobre lo ya desplazado, asi con factor 1 vuelve exacto a su sitio.
        /// </summary>
        private void AplicarEscala(float factor)
        {
            transform.localScale = escalaBase * factor;
            if (rt == null || !desdeCentro) return;

            Vector2 centroLocal = (new Vector2(0.5f, 0.5f) - rt.pivot) * rt.rect.size;
            Vector3 nuevo = Vector3.Scale(escalaBase, centroLocal) * (1f - factor);
            rt.localPosition += nuevo - desplazamientoAplicado;
            desplazamientoAplicado = nuevo;
        }
    }
}
