using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Boton generico para volver a una escena anterior. Mismo patron que
    /// <see cref="RankingUI.VolverAlMenu"/> y <see cref="BotonSalirTablero.VolverAlMenu"/>:
    /// un Button que carga 'escenaDestino' con SceneManager.LoadScene.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotonVolver : MonoBehaviour
    {
        [SerializeField] private Button boton;
        [Tooltip("Escena a la que vuelve. Debe estar anadida a Build Settings.")]
        [SerializeField] private string escenaDestino = "SeleccionJugadores";

        private void Reset() => boton = GetComponent<Button>();

        private void Awake()
        {
            if (boton == null) boton = GetComponent<Button>();
            if (boton != null) boton.onClick.AddListener(Volver);
        }

        public void Volver() => SceneManager.LoadScene(escenaDestino);
    }
}
