using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Tarjeta de un modo de juego en ModosJuegos. Si <see cref="implementado"/>
    /// esta activo, al pulsarla carga <see cref="escenaDestino"/> con
    /// SceneManager.LoadScene, sin tocar Jugadores.Lista ni ningun otro dato de
    /// partida (son estaticos, sobreviven solos al cambio de escena). Si no,
    /// solo avisa por consola: la tarjeta queda clicable pero sin efecto.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotonModoJuego : MonoBehaviour
    {
        [SerializeField] private Button boton;

        [Header("Modo")]
        [Tooltip("Solo para el aviso de consola cuando el modo no esta implementado.")]
        [SerializeField] private string nombreModo = "Modo";
        [Tooltip("Si esta desactivado, pulsar la tarjeta no hace nada mas que avisar por consola.")]
        [SerializeField] private bool implementado = false;
        [Tooltip("Escena a cargar cuando 'implementado' esta activo.")]
        [SerializeField] private string escenaDestino = "Tablero";

        private void Reset() => boton = GetComponent<Button>();

        private void Awake()
        {
            if (boton == null) boton = GetComponent<Button>();
            if (boton != null) boton.onClick.AddListener(Seleccionar);
        }

        public void Seleccionar()
        {
            if (implementado) SceneManager.LoadScene(escenaDestino);
            else Debug.LogWarning($"[BotonModoJuego] Modo '{nombreModo}' aun no implementado.");
        }
    }
}
