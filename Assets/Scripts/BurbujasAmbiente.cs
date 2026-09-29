using UnityEngine;

namespace Cubatis
{
    /// <summary>
    /// Va en la raiz del prefab BurbujasAmbiente (un ParticleSystem de
    /// Shuriken). El efecto en si (subida, wobble, tamanos, opacidad) esta
    /// entero en el ParticleSystem; esto solo lo encaja a la camara, porque
    /// cada escena tiene un encuadre distinto (las de UI usan tamano
    /// ortografico 5 y el Tablero lo recalcula en runtime segun el ancho del
    /// tablero y el aspecto).
    ///
    /// El prefab esta disenado para <see cref="tamanoReferencia"/>: se escala
    /// la raiz entera (simulacion Local + escalado Hierarchy, asi escalan a la
    /// vez tamano, velocidad y wobble) y el emisor se coloca justo bajo el
    /// borde inferior de la camara, con el ancho de la vista.
    ///
    /// El ParticleSystem viene con Play On Awake desactivado: se arranca aqui
    /// despues del primer ajuste para que el prewarm reparta las burbujas por
    /// toda la pantalla con el ancho correcto, en vez de empezar vacia.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public class BurbujasAmbiente : MonoBehaviour
    {
        [Tooltip("Camara a cubrir. Vacio = Camera.main.")]
        [SerializeField] private Camera camara;
        [Tooltip("Tamano ortografico para el que estan pensadas velocidad, tamano y vida de las particulas.")]
        [SerializeField] private float tamanoReferencia = 5f;
        [Tooltip("Distancia bajo el borde inferior a la que nacen (unidades de referencia), para que entren ya formadas.")]
        [SerializeField] private float margenInferior = 0.5f;
        [Tooltip("Ancho extra del emisor a cada lado (unidades de referencia), para que el wobble no deje los laterales vacios.")]
        [SerializeField] private float margenLateral = 0.5f;

        private ParticleSystem ps;
        private float ultimoTamano = -1f, ultimoAspecto = -1f;
        private Vector3 ultimaPosCamara;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            if (camara == null) camara = Camera.main;
        }

        private void Start()
        {
            Ajustar();
            if (!ps.isPlaying) ps.Play();
        }

        // En LateUpdate: AjusteCamaraTablero reencuadra en Start/LateUpdate y
        // hay que seguirle sin depender del orden de ejecucion.
        private void LateUpdate()
        {
            if (camara == null) return;
            if (!Mathf.Approximately(camara.orthographicSize, ultimoTamano) ||
                !Mathf.Approximately(camara.aspect, ultimoAspecto) ||
                camara.transform.position != ultimaPosCamara)
                Ajustar();
        }

        private void Ajustar()
        {
            if (camara == null || !camara.orthographic) return;

            float tam = camara.orthographicSize;
            float escala = tam / Mathf.Max(0.001f, tamanoReferencia);
            Vector3 c = camara.transform.position;

            transform.localScale = Vector3.one * escala;
            transform.position = new Vector3(c.x, c.y - tam - margenInferior * escala, 0f);

            // El ancho del emisor va en unidades locales (ya escaladas), asi que
            // solo depende del aspecto, no del tamano de la camara.
            var forma = ps.shape;
            Vector3 s = forma.scale;
            s.x = 2f * (tamanoReferencia * camara.aspect + margenLateral);
            forma.scale = s;

            ultimoTamano = tam;
            ultimoAspecto = camara.aspect;
            ultimaPosCamara = c;
        }
    }
}
