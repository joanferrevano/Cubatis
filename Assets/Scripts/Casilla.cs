using TMPro;
using UnityEngine;

namespace Cubatis
{
    /// <summary>
    /// Componente de una casilla ya colocada en el tablero. Guarda su indice
    /// en el recorrido (0 = START, 1..58 = numeradas, 59 = END), su numero
    /// visible y su <see cref="TipoCasilla"/> (el "tipo de reto" que usa la
    /// logica del juego). La construye <see cref="GeneradorTablero"/>.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class Casilla : MonoBehaviour
    {
        [SerializeField] private int indice;
        [SerializeField] private int numero;          // 0 para START y END
        [SerializeField] private TipoCasilla tipo;
        [SerializeField] private SpriteRenderer render;
        [SerializeField] private TextMeshPro texto;

        public int Indice => indice;
        public int Numero => numero;
        public TipoCasilla Tipo => tipo;
        public bool EsNumerada => numero > 0;
        public SpriteRenderer Render => render;

        /// <summary>
        /// Coloca el sprite de forma que su borde inferior-izquierdo quede en
        /// <paramref name="esquinaMundo"/>, sea cual sea el pivote del sprite.
        /// <paramref name="unidadesPorPixel"/> = ladoCasillaMundo / ladoSpritePx.
        /// </summary>
        public void Construir(Sprite sprite, Vector3 esquinaMundo, float unidadesPorPixel,
            int indice, int numero, TipoCasilla tipo,
            bool conCollider, string sortingLayer, int sortingOrder)
        {
            this.indice = indice;
            this.numero = numero;
            this.tipo = tipo;

            if (render == null) render = GetComponent<SpriteRenderer>();
            render.sprite = sprite;
            render.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayer)) render.sortingLayerName = sortingLayer;

            transform.localScale = Vector3.one * (unidadesPorPixel * sprite.pixelsPerUnit);
            transform.position = esquinaMundo + (Vector3)(sprite.pivot * unidadesPorPixel);

            if (conCollider)
            {
                if (!TryGetComponent(out BoxCollider2D box))
                    box = gameObject.AddComponent<BoxCollider2D>();
                box.size = sprite.rect.size / sprite.pixelsPerUnit;
                box.offset = CentroLocal(sprite);
            }
        }

        public void CrearNumero(float tamano, Color color, bool visible, string sortingLayer, int sortingOrder)
        {
            if (numero <= 0) return;

            var go = new GameObject("Numero");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)CentroLocal(render.sprite) + Vector3.back * 0.1f;

            texto = go.AddComponent<TextMeshPro>();
            texto.text = numero.ToString();
            texto.fontSize = tamano;
            texto.fontStyle = FontStyles.Bold;
            texto.color = color;
            texto.alignment = TextAlignmentOptions.Center;
            texto.outlineWidth = 0.2f;
            texto.outlineColor = Color.black;
            texto.rectTransform.sizeDelta = new Vector2(10f, 10f);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;
            mr.sortingOrder = sortingOrder;

            go.SetActive(visible);
        }

        public void MostrarNumero(bool visible)
        {
            if (texto != null) texto.gameObject.SetActive(visible);
        }

        // Centro geometrico del sprite en coordenadas locales (los sprites del
        // tablero tienen el pivote en una esquina, no en el centro).
        private static Vector2 CentroLocal(Sprite sprite) =>
            (sprite.rect.size * 0.5f - sprite.pivot) / sprite.pixelsPerUnit;

        public override string ToString() =>
            $"[{indice}] {(EsNumerada ? $"#{numero} " : "")}{tipo}";
    }
}
