using UnityEngine;

namespace Cubatis
{
    /// <summary>
    /// Va en el GameObject del sprite de fondo. Lo fuerza a dibujarse DETRAS de
    /// todo (fondo -> tablero -> fichas -> dado) con un orden de dibujo muy bajo,
    /// sin depender del eje Z ni del orden en la jerarquia.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class FondoJuego : MonoBehaviour
    {
        [Tooltip("Orden de dibujo del fondo. Muy negativo para que quede detras del tablero, las fichas y el dado.")]
        [SerializeField] private int ordenDibujo = -1000;
        [SerializeField] private string sortingLayer = "Default";

        private void Awake() => Aplicar();
#if UNITY_EDITOR
        private void OnValidate() => Aplicar();
#endif

        private void Aplicar()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            if (!string.IsNullOrEmpty(sortingLayer)) sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = ordenDibujo;
        }
    }
}
