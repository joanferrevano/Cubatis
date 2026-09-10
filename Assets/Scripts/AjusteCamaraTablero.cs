using UnityEngine;

namespace Cubatis
{
    /// <summary>
    /// Encuadra una camara ortografica sobre el tablero generado. Pensado para
    /// movil: por defecto ajusta al ANCHO del tablero para que se vea completo
    /// de lado a lado en cualquier relacion de aspecto.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class AjusteCamaraTablero : MonoBehaviour
    {
        public enum Modo { AjustarAlAncho, AjustarTodo }

        [SerializeField] private GeneradorTablero tablero;
        [SerializeField] private Camera camara;

        [SerializeField] private Modo modo = Modo.AjustarAlAncho;
        [SerializeField] private float margenHorizontal = 0.3f;
        [Range(0f, 0.5f)]
        [SerializeField] private float margenPorcentual = 0f;
        [SerializeField] private float distanciaZ = -10f;
        [SerializeField] private float offsetVertical = 0f;
        [SerializeField] private bool ajustarAlIniciar = true;
        [SerializeField] private bool actualizarEnCambioResolucion = true;

        private float ultimoAspecto = -1f;

        private void Reset()
        {
            camara = GetComponent<Camera>();
            tablero = FindAnyObjectByType<GeneradorTablero>();
        }

        private void Awake()
        {
            if (camara == null) camara = GetComponent<Camera>();
            if (tablero == null) tablero = FindAnyObjectByType<GeneradorTablero>();
        }

        private void Start()
        {
            if (ajustarAlIniciar) Ajustar();
        }

        private void LateUpdate()
        {
            if (actualizarEnCambioResolucion && camara != null &&
                Mathf.Abs(camara.aspect - ultimoAspecto) > 0.001f)
                Ajustar();
        }

        [ContextMenu("Ajustar ahora")]
        public void Ajustar()
        {
            if (camara == null || tablero == null || tablero.Total == 0) return;

            Bounds b = Limites();
            camara.orthographic = true;
            camara.orthographicSize = TamanoOrtografico(camara.aspect, b);
            camara.transform.position = new Vector3(b.center.x, b.center.y + offsetVertical, distanciaZ);
            ultimoAspecto = camara.aspect;
        }

        /// <summary>
        /// orthographicSize que tendria la camara para un <paramref name="aspect"/>
        /// dado, con la MISMA logica que <see cref="Ajustar"/> y sin tocar la
        /// camara. Lo usa la vista previa de <see cref="CartaReto"/> para calcular
        /// exactamente lo que se vera en Play (aspect de la ventana Game).
        /// </summary>
        public float TamanoOrtografico(float aspect)
        {
            if (tablero == null || tablero.Total == 0)
                return camara != null ? camara.orthographicSize : 5f;
            return TamanoOrtografico(aspect, Limites());
        }

        private float TamanoOrtografico(float aspect, Bounds b)
        {
            float ancho = (b.size.x + margenHorizontal * 2f) * (1f + margenPorcentual);
            float size = ancho / (2f * Mathf.Max(0.001f, aspect));
            if (modo == Modo.AjustarTodo)
                size = Mathf.Max(size, b.size.y * (1f + margenPorcentual) / 2f);
            return size;
        }

        private Bounds Limites()
        {
            Bounds b = new Bounds(tablero.ObtenerPosicion(0), Vector3.zero);
            foreach (Casilla c in tablero.Casillas)
            {
                if (c == null) continue;
                if (c.Render != null) b.Encapsulate(c.Render.bounds);
                else b.Encapsulate(c.transform.position);
            }
            return b;
        }
    }
}
