using UnityEngine;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Boton de depuracion: fuerza la victoria instantanea del jugador en
    /// turno llamando a <see cref="GestorPartida.ForzarVictoriaDebug"/>, que
    /// reutiliza exactamente la misma logica de fin de partida que una tirada
    /// real. Solo para desarrollo: se autodesactiva si el build no es de
    /// depuracion (Debug.isDebugBuild), pero aun asi hay que borrar este
    /// GameObject antes de publicar.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotonDebugGanar : MonoBehaviour
    {
        [SerializeField] private Button boton;
        [SerializeField] private GestorPartida gestor;

        private void Reset()
        {
            boton = GetComponent<Button>();
            gestor = FindAnyObjectByType<GestorPartida>();
        }

        private void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                gameObject.SetActive(false);
                return;
            }

            if (boton == null) boton = GetComponent<Button>();
            if (gestor == null) gestor = FindAnyObjectByType<GestorPartida>();
            if (boton != null) boton.onClick.AddListener(Pulsar);
        }

        private void Pulsar()
        {
            if (gestor != null) gestor.ForzarVictoriaDebug();
        }
    }
}
