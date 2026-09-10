using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cubatis
{
    /// <summary>
    /// Dado tocable. Al pulsarlo hace una animacion de tirada (gira entre las 6
    /// caras desacelerando) y al parar expone un resultado 1..6. No mueve nada:
    /// solo publica el numero para que otro sistema lo use mas adelante.
    ///
    /// Funciona con un SpriteRenderer (mundo, necesita Collider2D) o con un
    /// Image de UI (Canvas). El click tambien se puede lanzar desde un Button
    /// (onClick -> Dado.Tirar) o por codigo llamando a <see cref="Tirar"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class Dado : MonoBehaviour, IPointerClickHandler
    {
        [Header("Caras (indice 0 = cara 1 ... indice 5 = cara 6)")]
        [SerializeField] private Sprite[] caras = new Sprite[6];

        [Header("Dibujo")]
        [Tooltip("Orden de dibujo del dado. Alto para que quede SIEMPRE por encima del fondo, el tablero y las fichas.")]
        [SerializeField] private int ordenDibujo = 1000;
        [SerializeField] private string sortingLayer = "Default";

        [Header("Animacion")]
        [Tooltip("Duracion total de la tirada en segundos.")]
        [SerializeField] private float duracion = 0.8f;
        [Tooltip("Segundos entre cambios de cara al principio (rapido).")]
        [SerializeField] private float intervaloInicial = 0.035f;
        [Tooltip("Segundos entre cambios de cara al final (lento).")]
        [SerializeField] private float intervaloFinal = 0.16f;

        [Header("Resultado")]
        [Tooltip("Se invoca al terminar la tirada con el numero 1..6.")]
        public UnityEvent<int> alSalirResultado;

        public int UltimoResultado { get; private set; }
        public bool Girando { get; private set; }

        private bool puedeTirar = true;
        /// <summary>
        /// Si es false, el dado ignora los toques (turno ajeno o ficha en
        /// movimiento). Lo controla <see cref="GestorPartida"/>. Ademas atenua
        /// el sprite para que se vea deshabilitado.
        /// </summary>
        public bool PuedeTirar
        {
            get => puedeTirar;
            set
            {
                puedeTirar = value;
                float a = value ? 1f : 0.4f;
                if (sr != null) { var c = sr.color; c.a = a; sr.color = c; }
                if (img != null) { var c = img.color; c.a = a; img.color = c; }
            }
        }

        private SpriteRenderer sr;
        private Image img;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            img = GetComponent<Image>();
            AplicarOrden();
        }

        private void AplicarOrden()
        {
            var r = sr != null ? sr : GetComponent<SpriteRenderer>();
            if (r == null) return;
            if (!string.IsNullOrEmpty(sortingLayer)) r.sortingLayerName = sortingLayer;
            r.sortingOrder = ordenDibujo;
        }

#if UNITY_EDITOR
        private void OnValidate() => AplicarOrden();
#endif

        // --- Entradas -----------------------------------------------------
        public void OnPointerClick(PointerEventData _) => Tirar();       // UI Image / con EventSystem
        private void OnMouseDown() => Tirar();                          // SpriteRenderer + Collider2D

        /// <summary>Lanza la tirada. Se ignora si ya esta girando.</summary>
        public void Tirar()
        {
            if (Girando || !puedeTirar) return;
            if (!CarasValidas()) { Debug.LogError("[Dado] Faltan sprites de caras (necesita 6)."); return; }
            StartCoroutine(RutinaTirada());
        }

        private IEnumerator RutinaTirada()
        {
            Girando = true;

            float t = 0f;
            int caraActual = -1;
            while (t < duracion)
            {
                int cara = Random.Range(0, 6);
                if (cara == caraActual) cara = (cara + 1) % 6;
                caraActual = cara;
                Pintar(cara);

                float k = t / duracion;                       // 0 -> 1
                float espera = Mathf.Lerp(intervaloInicial, intervaloFinal, k * k);
                t += espera;
                yield return new WaitForSeconds(espera);
            }

            UltimoResultado = Random.Range(1, 7);             // 1..6
            Pintar(UltimoResultado - 1);
            Girando = false;

            Debug.Log($"[Dado] Resultado: {UltimoResultado}");
            alSalirResultado?.Invoke(UltimoResultado);
        }

        private void Pintar(int indice)
        {
            if (sr != null) sr.sprite = caras[indice];
            if (img != null) img.sprite = caras[indice];
        }

        private bool CarasValidas()
        {
            if (caras == null || caras.Length != 6) return false;
            foreach (var c in caras) if (c == null) return false;
            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Cargar caras desde Assets/Boards/Dado")]
        private void CargarCaras()
        {
            caras = new Sprite[6];
            for (int i = 0; i < 6; i++)
            {
                string ruta = $"Assets/Boards/Dado/Dado {i + 1}.png";
                caras[i] = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
                if (caras[i] == null)
                    foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(ruta))
                        if (o is Sprite sp) { caras[i] = sp; break; }
            }
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
