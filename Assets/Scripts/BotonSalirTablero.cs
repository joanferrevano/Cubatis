using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Boton icono (esquina inferior izquierda, a la altura del Dado) para
    /// abandonar la partida en curso. Mismo patron que
    /// <see cref="RankingUI.VolverAlMenu"/> (Button que carga "MenuPrincipal"
    /// con SceneManager.LoadScene), pero aqui con un panel de confirmacion de
    /// por medio para no perder la partida por un toque accidental: el dado y
    /// el resto de la logica de GestorPartida no se tocan, esto solo corta la
    /// partida cargando el menu.
    ///
    /// Mientras <see cref="CartaReto"/> esta abierta (su fondo oscuro tapa el
    /// tablero y el dado) este boton se desactiva con
    /// <see cref="Button.interactable"/> = false, igual que GestorPartida hace
    /// con <see cref="Dado.PuedeTirar"/>: el propio Button ya sabe atenuarse
    /// (usa el mismo m_DisabledColor que el resto de botones del proyecto), asi
    /// que no hace falta montar un overlay ni tocar sorting order.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotonSalirTablero : MonoBehaviour
    {
        [Header("Icono (arrastra aqui el sprite: flecha atras, casa, etc.)")]
        [SerializeField] private Image icono;

        [Header("Botones")]
        [SerializeField] private Button botonSalir;
        [SerializeField] private Button botonConfirmarSalir;
        [SerializeField] private Button botonCancelar;
        [SerializeField] private GameObject panelConfirmacion;

        [Header("Navegacion")]
        [Tooltip("Escena del menu principal. Debe estar anadida a Build Settings.")]
        [SerializeField] private string escenaMenu = "MenuPrincipal";

        [Header("Carta de reto")]
        [Tooltip("Mientras esta abierta, el boton se desactiva (Button.interactable = false) y queda con el mismo aspecto atenuado que cualquier boton deshabilitado.")]
        [SerializeField] private CartaReto carta;

        private bool bloqueadoPorCarta;

        private void Reset()
        {
            carta = FindAnyObjectByType<CartaReto>();
        }

        private void Awake()
        {
            if (carta == null) carta = FindAnyObjectByType<CartaReto>();
            if (panelConfirmacion != null) panelConfirmacion.SetActive(false);
            if (botonSalir != null) botonSalir.onClick.AddListener(AbrirConfirmacion);
            if (botonConfirmarSalir != null) botonConfirmarSalir.onClick.AddListener(VolverAlMenu);
            if (botonCancelar != null) botonCancelar.onClick.AddListener(CerrarConfirmacion);
        }

        private void Update()
        {
            bool debeBloquearse = carta != null && carta.Abierta;
            if (debeBloquearse == bloqueadoPorCarta) return;

            bloqueadoPorCarta = debeBloquearse;
            if (botonSalir != null) botonSalir.interactable = !bloqueadoPorCarta;
        }

        private void AbrirConfirmacion()
        {
            if (panelConfirmacion != null) panelConfirmacion.SetActive(true);
        }

        private void CerrarConfirmacion()
        {
            if (panelConfirmacion != null) panelConfirmacion.SetActive(false);
        }

        public void VolverAlMenu() => SceneManager.LoadScene(escenaMenu);
    }
}
