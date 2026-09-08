using UnityEngine;

namespace Cubatis
{
    /// <summary>
    /// Categorías disponibles para las casillas del juego Cubatis.
    /// </summary>
    public enum TipoCasilla
    {
        Start,
        Beber,
        YoNunca,
        Verdad,
        Reto,
        Evento,
        Hot,
        End
    }

    /// <summary>
    /// Componente adjunto a cada GameObject de casilla en el tablero.
    /// Almacena su índice en la ruta, su categoría tipada y sus referencias visuales.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class Casilla : MonoBehaviour
    {
        [Header("Datos de Casilla")]
        [Tooltip("Índice de la casilla en el tablero (0 = START, 62 = END).")]
        [SerializeField] private int indice;

        [Tooltip("Categoría o tipo de acción de esta casilla.")]
        [SerializeField] private TipoCasilla categoria;

        [Header("Referencias Visuales")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        #region Propiedades Públicas

        /// <summary>
        /// Índice de la casilla en la ruta (0 a 62).
        /// </summary>
        public int Indice => indice;

        /// <summary>
        /// Categoría tipada de la casilla.
        /// </summary>
        public TipoCasilla Categoria => categoria;

        /// <summary>
        /// Nombre legible de la categoría en español.
        /// </summary>
        public string NombreCategoria
        {
            get
            {
                return categoria switch
                {
                    TipoCasilla.Start => "START",
                    TipoCasilla.Beber => "Beber",
                    TipoCasilla.YoNunca => "Yo nunca",
                    TipoCasilla.Verdad => "Verdad",
                    TipoCasilla.Reto => "Reto",
                    TipoCasilla.Evento => "Evento",
                    TipoCasilla.Hot => "Hot",
                    TipoCasilla.End => "END",
                    _ => categoria.ToString()
                };
            }
        }

        /// <summary>
        /// Componente SpriteRenderer de la casilla.
        /// </summary>
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        #endregion

        #region Inicialización

        /// <summary>
        /// Inicializa los datos y la configuración visual de la casilla.
        /// </summary>
        /// <param name="nuevoIndice">Índice en el tablero (0..62).</param>
        /// <param name="nuevaCategoria">Categoría asignada.</param>
        /// <param name="sprite">Sprite correspondiente a la categoría.</param>
        /// <param name="escalaVisual">Escala local a aplicar en el Transform.</param>
        /// <param name="agregarCollider">Si es verdadero, asegura un BoxCollider2D ajustado al sprite.</param>
        /// <param name="sortingOrder">Orden de renderizado en la capa de sprites.</param>
        /// <param name="sortingLayerName">Nombre de la capa de renderizado.</param>
        public void Configurar(
            int nuevoIndice,
            TipoCasilla nuevaCategoria,
            Sprite sprite,
            Vector3 escalaVisual,
            bool agregarCollider = true,
            int sortingOrder = 0,
            string sortingLayerName = "Default")
        {
            indice = nuevoIndice;
            categoria = nuevaCategoria;

            transform.localScale = escalaVisual;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                spriteRenderer.sortingLayerName = sortingLayerName;
            }

            if (agregarCollider)
            {
                BoxCollider2D collider = GetComponent<BoxCollider2D>();
                if (collider == null)
                {
                    collider = gameObject.AddComponent<BoxCollider2D>();
                }
                if (sprite != null)
                {
                    collider.size = sprite.rect.size / sprite.pixelsPerUnit;
                    collider.offset = Vector2.zero;
                }
            }
        }

        #endregion

        public override string ToString()
        {
            return $"Casilla [{indice}]: {NombreCategoria} ({gameObject.name})";
        }
    }
}
