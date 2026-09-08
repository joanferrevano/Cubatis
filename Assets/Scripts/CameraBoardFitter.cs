using UnityEngine;

namespace Cubatis
{
    /// <summary>
    /// Ajusta automáticamente el Orthographic Size y la posición de la cámara
    /// en función del ancho total del tablero y la relación de aspecto de la pantalla,
    /// garantizando que el tablero ocupe todo el ancho de la pantalla y quede perfectamente centrado.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CameraBoardFitter : MonoBehaviour
    {
        public enum ModoAjuste
        {
            /// <summary>
            /// El tablero siempre ocupa exactamente el ancho total de la pantalla (ideal para pantallas verticales).
            /// </summary>
            AjustarAlAncho,

            /// <summary>
            /// Garantiza que tanto el ancho como el alto queden visibles sin recortar nada.
            /// </summary>
            AjustarTodoElTablero
        }

        [Header("Referencias")]
        [Tooltip("Referencia al BoardManager. Si no se asigna, se busca automáticamente en la escena.")]
        [SerializeField] private BoardManager boardManager;

        [Tooltip("Cámara a ajustar. Si no se asigna, se utiliza el componente Camera en este objeto o Camera.main.")]
        [SerializeField] private Camera camara;

        [Header("Configuración de Encuadre")]
        [Tooltip("Modo de ajuste de la cámara.")]
        [SerializeField] private ModoAjuste modo = ModoAjuste.AjustarAlAncho;

        [Tooltip("Margen horizontal adicional en unidades del mundo a cada lado del tablero (0 = pegado al borde exacto).")]
        [SerializeField] private float margenHorizontal = 0.0f;

        [Tooltip("Margen adicional porcentual (ej. 0.05 = 5% de espacio extra a los lados).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float margenPorcentual = 0.0f;

        [Tooltip("Profundidad Z de la cámara.")]
        [SerializeField] private float distanciaZ = -10f;

        [Tooltip("Desplazamiento vertical adicional en el eje Y (positivo = hacia arriba, negativo = hacia abajo).")]
        public float offsetVertical = 0f;

        [Header("Comportamiento")]
        [Tooltip("Ajusta la cámara automáticamente al iniciar la escena.")]
        [SerializeField] private bool ajustarAlIniciar = true;

        [Tooltip("Actualiza continuamente si detecta cambios de resolución o rotación de pantalla.")]
        [SerializeField] private bool actualizarEnCambioResolucion = true;

        // Caché para detectar cambios de resolución y configuración
        private float ultimoAspecto = -1f;
        private int ultimoAnchoPantalla = -1;
        private int ultimoAltoPantalla = -1;
        private float ultimoTamañoCasilla = -1f;
        private float ultimoOffsetVertical = float.NaN;

        #region Ciclo de Vida Unity

        private void Awake()
        {
            InicializarReferencias();
        }

        private void Start()
        {
            if (ajustarAlIniciar)
            {
                AjustarCamara();
            }
        }

        private void OnEnable()
        {
            AjustarCamara();
        }

        private void LateUpdate()
        {
            if (!actualizarEnCambioResolucion) return;

            if (HaCambiadoResolucionOConfiguracion())
            {
                AjustarCamara();
            }
        }

        #endregion

        #region Lógica de Ajuste de Cámara

        private void InicializarReferencias()
        {
            if (camara == null)
            {
                camara = GetComponent<Camera>();
                if (camara == null)
                {
                    camara = Camera.main;
                }
            }

            if (boardManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                boardManager = Object.FindAnyObjectByType<BoardManager>();
#else
                boardManager = Object.FindObjectOfType<BoardManager>();
#endif
            }
        }

        private bool HaCambiadoResolucionOConfiguracion()
        {
            if (camara == null) return false;

            float aspectActual = camara.aspect;
            int anchoActual = Screen.width;
            int altoActual = Screen.height;
            float tamañoActual = boardManager != null ? boardManager.TamañoCasilla : -1f;

            bool cambio = Mathf.Abs(aspectActual - ultimoAspecto) > 0.001f ||
                          anchoActual != ultimoAnchoPantalla ||
                          altoActual != ultimoAltoPantalla ||
                          Mathf.Abs(tamañoActual - ultimoTamañoCasilla) > 0.001f ||
                          Mathf.Abs(offsetVertical - ultimoOffsetVertical) > 0.001f;

            return cambio;
        }

        /// <summary>
        /// Calcula el Orthographic Size exacto según el ancho del tablero y la relación de aspecto,
        /// y centra la posición de la cámara en el tablero.
        /// </summary>
        [ContextMenu("Ajustar Cámara Ahora")]
        public void AjustarCamara()
        {
            InicializarReferencias();

            if (camara == null)
            {
                Debug.LogWarning("[CameraBoardFitter] No se encontró ninguna Camera para ajustar.");
                return;
            }

            if (!camara.orthographic)
            {
                Debug.LogWarning("[CameraBoardFitter] La cámara no está en modo Ortográfico. Cambiando a ortográfica...");
                camara.orthographic = true;
            }

            // 1. Obtener dimensiones y centro del tablero
            float anchoTablero;
            float altoTablero;
            Vector3 centroTablero;

            if (boardManager != null)
            {
                Bounds limites = boardManager.ObtenerLimitesTablero();
                anchoTablero = limites.size.x;
                altoTablero = limites.size.y;
                centroTablero = limites.center;
            }
            else
            {
                // Dimensiones por defecto de 9 columnas x 8 filas (tamaño 1 unidad)
                anchoTablero = BoardManager.Columnas * 1.0f;
                altoTablero = BoardManager.Filas * 1.0f;
                centroTablero = Vector3.zero;
            }

            // 2. Aplicar márgenes configurados al ancho deseado
            float anchoDeseado = anchoTablero + (margenHorizontal * 2f);
            if (margenPorcentual > 0f)
            {
                anchoDeseado *= (1f + margenPorcentual);
            }

            // 3. Calcular Orthographic Size en función del aspect ratio
            // En Unity: anchoVisible = 2 * orthographicSize * aspect
            // Por tanto: orthographicSize = anchoVisible / (2 * aspect)
            float aspect = Mathf.Max(0.001f, camara.aspect);
            float orthoSizePorAncho = anchoDeseado / (2f * aspect);

            float nuevoOrthoSize = orthoSizePorAncho;

            if (modo == ModoAjuste.AjustarTodoElTablero)
            {
                float altoDeseado = altoTablero * (1f + margenPorcentual);
                float orthoSizePorAlto = altoDeseado / 2f;
                nuevoOrthoSize = Mathf.Max(orthoSizePorAncho, orthoSizePorAlto);
            }

            camara.orthographicSize = nuevoOrthoSize;

            // 4. Centrar la cámara en el centro geométrico del tablero aplicando el offset vertical
            camara.transform.position = new Vector3(centroTablero.x, centroTablero.y + offsetVertical, distanciaZ);

            // Registrar estado actual
            ultimoAspecto = aspect;
            ultimoAnchoPantalla = Screen.width;
            ultimoAltoPantalla = Screen.height;
            ultimoTamañoCasilla = boardManager != null ? boardManager.TamañoCasilla : 1f;
            ultimoOffsetVertical = offsetVertical;
        }

        #endregion
    }
}
