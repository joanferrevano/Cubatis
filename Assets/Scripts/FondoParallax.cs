using UnityEngine;

namespace Cubatis
{
    /// <summary>
    /// Deriva lenta y continua del fondo para dar sensacion de profundidad.
    /// Vale para el fondo de mundo (SpriteRenderer, junto a FondoJuego) y para
    /// los fondos de UI (Image estirado a todo el Canvas).
    ///
    /// No es un scroll infinito por offset de textura porque "Fondo Personajes"
    /// no es un tile: trae el degradado pintado y los vasos colocados a mano,
    /// asi que repetirlo dejaria cortes. En su lugar se amplia el fondo lo justo
    /// y se mueve dentro de ese margen en una curva suave (vaiven en
    /// <see cref="direccion"/> mas un balanceo perpendicular mas lento, tipo
    /// Lissajous), de modo que nunca asoma un borde.
    ///
    /// Usa Time.unscaledTime, que cuenta desde el arranque del juego y no se
    /// reinicia al cambiar de escena: las escenas de UI que comparten fondo lo
    /// retoman en el mismo punto en vez de saltar al centro.
    /// </summary>
    [DisallowMultipleComponent]
    public class FondoParallax : MonoBehaviour
    {
        [Header("Direccion")]
        [Tooltip("Eje principal del vaiven. (1,1) = diagonal, (0,1) = vertical.")]
        [SerializeField] private Vector2 direccion = new Vector2(1f, 1f);
        [Tooltip("Balanceo perpendicular, relativo al recorrido. 0 = vaiven recto.")]
        [Range(0f, 1f)]
        [SerializeField] private float balanceo = 0.35f;

        [Header("Velocidad")]
        [Tooltip("Segundos en completar una ida y vuelta. Mas alto = mas lento.")]
        [SerializeField] private float duracionCiclo = 30f;

        [Header("Recorrido")]
        [Tooltip("Desplazamiento maximo, en fraccion del ancho del fondo. La ampliacion necesaria para no ver bordes se calcula sola.")]
        [Range(0f, 0.1f)]
        [SerializeField] private float recorrido = 0.035f;

        private Vector3 posicionBase;
        private Vector3 escalaBase;
        private Vector2 tamano;          // tamano del fondo sin escalar, en unidades locales del padre
        private Vector2 centroLocal;     // centro del fondo respecto a su pivot, sin escalar
        private float ampliacion = 1f;
        private Vector2 eje, perpendicular;
        private bool iniciado;

        private void Start()
        {
            // En Start y no en Awake: el Canvas ya ha resuelto el tamano del
            // RectTransform estirado.
            posicionBase = transform.localPosition;
            escalaBase = transform.localScale;

            if (transform is RectTransform rt)
            {
                tamano = rt.rect.size;
                centroLocal = (new Vector2(0.5f, 0.5f) - rt.pivot) * rt.rect.size;
            }
            else if (TryGetComponent(out SpriteRenderer sr) && sr.sprite != null)
            {
                tamano = sr.sprite.bounds.size;
                centroLocal = sr.sprite.bounds.center;
            }
            tamano = Vector2.Scale(tamano, escalaBase);
            centroLocal = Vector2.Scale(centroLocal, escalaBase);

            eje = direccion.sqrMagnitude > 0f ? direccion.normalized : Vector2.up;
            perpendicular = new Vector2(-eje.y, eje.x);

            // Maximo desplazamiento posible por eje -> ampliacion minima para
            // que el borde ampliado cubra siempre el hueco que deja al moverse.
            float a = recorrido * tamano.x;
            Vector2 maximo = new Vector2(
                a * (Mathf.Abs(eje.x) + balanceo * Mathf.Abs(perpendicular.x)),
                a * (Mathf.Abs(eje.y) + balanceo * Mathf.Abs(perpendicular.y)));
            ampliacion = 1f;
            if (tamano.x > 0f) ampliacion = Mathf.Max(ampliacion, 1f + 2f * maximo.x / tamano.x);
            if (tamano.y > 0f) ampliacion = Mathf.Max(ampliacion, 1f + 2f * maximo.y / tamano.y);

            iniciado = true;
            transform.localScale = escalaBase * ampliacion;
            Actualizar();
        }

        private void OnDisable()
        {
            if (!iniciado) return;
            transform.localPosition = posicionBase;
            transform.localScale = escalaBase;
        }

        private void OnEnable()
        {
            if (iniciado) transform.localScale = escalaBase * ampliacion;
        }

        private void Update()
        {
            if (iniciado) Actualizar();
        }

        private void Actualizar()
        {
            float fase = Time.unscaledTime * (2f * Mathf.PI) / Mathf.Max(1f, duracionCiclo);
            float a = recorrido * tamano.x;
            // El balanceo usa un periodo "irracional" respecto al principal
            // para que la trayectoria no se repita a simple vista.
            Vector2 desplazamiento = eje * (Mathf.Sin(fase) * a)
                                   + perpendicular * (Mathf.Sin(fase / 1.618f) * a * balanceo);

            // localScale escala respecto al pivot: se corrige para ampliar
            // desde el centro del fondo y no descubrir el lado del pivot.
            Vector2 correccion = -centroLocal * (ampliacion - 1f);
            transform.localPosition = posicionBase + (Vector3)(desplazamiento + correccion);
        }
    }
}
