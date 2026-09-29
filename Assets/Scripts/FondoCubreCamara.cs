using UnityEngine;

namespace Cubatis
{
    /// <summary>
    /// Centra un fondo de mundo (SpriteRenderer) en la camara y lo escala para
    /// cubrir toda la vista sin deformarlo (modo "cover": puede recortar un poco
    /// por los lados o arriba/abajo segun el aspecto).
    ///
    /// Existe porque en las escenas de UI el fondo antes era un Image del Canvas
    /// Overlay, y un Canvas Overlay se pinta encima de todo lo del mundo: asi no
    /// habia forma de meter las burbujas (ParticleSystem) entre el fondo y la
    /// UI. Ahora el fondo es un sprite de mundo como en el Tablero (con
    /// <see cref="FondoJuego"/> para el sorting) y esto hace lo que antes hacia
    /// el anclaje estirado del RectTransform.
    ///
    /// Se ajusta en Awake para que <see cref="FondoParallax"/> (que lee escala y
    /// posicion en Start) calcule su ampliacion sobre el tamano ya ajustado.
    /// No sirve para el Tablero, cuya camara se reencuadra despues en Start.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class FondoCubreCamara : MonoBehaviour
    {
        [Tooltip("Camara a cubrir. Vacio = Camera.main.")]
        [SerializeField] private Camera camara;

        private void Awake()
        {
            if (camara == null) camara = Camera.main;
            var sr = GetComponent<SpriteRenderer>();
            if (camara == null || !camara.orthographic || sr.sprite == null) return;

            float alto = camara.orthographicSize * 2f;
            float ancho = alto * camara.aspect;
            Vector2 tamSprite = sr.sprite.bounds.size;
            float escala = Mathf.Max(ancho / tamSprite.x, alto / tamSprite.y);

            transform.localScale = new Vector3(escala, escala, 1f);
            // Centro del sprite (no su pivot) sobre el centro de la camara.
            Vector3 c = camara.transform.position;
            Vector3 centroSprite = sr.sprite.bounds.center * escala;
            transform.position = new Vector3(c.x - centroSprite.x, c.y - centroSprite.y, 0f);
        }
    }
}
