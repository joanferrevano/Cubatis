using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Cubatis
{
    /// <summary>
    /// Mueve una ficha por las casillas del tablero. Va en el mismo GameObject
    /// que <see cref="GeneradorTablero"/> y le pregunta las posiciones; no hay
    /// ninguna capa intermedia.
    /// </summary>
    [RequireComponent(typeof(GeneradorTablero))]
    public class MovimientoFicha : MonoBehaviour
    {
        [Header("Animacion")]
        [SerializeField] private float duracionPorCasilla = 0.25f;
        [SerializeField] private float alturaSalto = 0.4f;
        [SerializeField] private Vector3 offsetFicha = Vector3.zero;

        [Header("Eventos")]
        public UnityEvent<Transform, Casilla> alLlegarACasilla;

        private GeneradorTablero tablero;
        private Coroutine rutina;

        private void Awake() => tablero = GetComponent<GeneradorTablero>();

        public bool SeEstaMoviendo => rutina != null;

        /// <summary>Coloca la ficha en una casilla al instante.</summary>
        public void Colocar(Transform ficha, int indice)
        {
            if (ficha == null || !tablero.EsIndiceValido(indice)) return;
            ficha.position = tablero.ObtenerPosicion(indice) + offsetFicha;
            alLlegarACasilla?.Invoke(ficha, tablero.ObtenerCasilla(indice));
        }

        /// <summary>Avanza la ficha casilla a casilla desde 'origen' hasta 'destino'.</summary>
        public void Mover(Transform ficha, int origen, int destino, Action alTerminar = null)
        {
            if (ficha == null) return;
            destino = Mathf.Clamp(destino, 0, tablero.Total - 1);
            if (rutina != null) StopCoroutine(rutina);
            rutina = StartCoroutine(RutinaMover(ficha, origen, destino, alTerminar));
        }

        private IEnumerator RutinaMover(Transform ficha, int origen, int destino, Action alTerminar)
        {
            int paso = origen < destino ? 1 : -1;
            for (int i = origen; i != destino; i += paso)
            {
                yield return SaltarA(ficha, i + paso);
                if (tablero.EsIndiceValido(i + paso))
                    alLlegarACasilla?.Invoke(ficha, tablero.ObtenerCasilla(i + paso));
            }
            rutina = null;
            alTerminar?.Invoke();
        }

        private IEnumerator SaltarA(Transform ficha, int indice)
        {
            Vector3 desde = ficha.position;
            Vector3 hasta = tablero.ObtenerPosicion(indice) + offsetFicha;
            float t = 0f;

            while (t < 1f)
            {
                if (ficha == null) yield break;
                t += Time.deltaTime / Mathf.Max(0.01f, duracionPorCasilla);
                float k = Mathf.Clamp01(t);
                ficha.position = Vector3.Lerp(desde, hasta, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * alturaSalto);
                yield return null;
            }
            if (ficha != null) ficha.position = hasta;
        }
    }
}
