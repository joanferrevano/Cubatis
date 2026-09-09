using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Logica de la pantalla de seleccion de jugadores. Toda la UI vive en la
    /// escena como GameObjects reales; este script solo se engancha a ellos por
    /// referencia y controla anadir/eliminar jugadores, el popup, los limites y
    /// el paso a la escena del tablero.
    ///
    /// Para construir/regenerar la escena y los prefabs:
    ///   menu  Cubatis > Construir escena Seleccion de Jugadores
    /// </summary>
    [DisallowMultipleComponent]
    public class SeleccionJugadores : MonoBehaviour
    {
        [Header("Grid de jugadores")]
        [Tooltip("Objeto con Grid Layout Group. El boton '+' es su primer hijo fijo.")]
        [SerializeField] private Transform contenedorSlots;
        [SerializeField] private SlotJugador prefabSlot;
        [SerializeField] private GameObject botonMas;       // slot "+" fijo
        [SerializeField] private Button botonEmpezar;

        [Header("Popup anadir jugador")]
        [SerializeField] private GameObject popup;
        [SerializeField] private Transform contenedorAvatares;
        [SerializeField] private BotonAvatar prefabBotonAvatar;
        [SerializeField] private TMP_InputField campoNombre;
        [SerializeField] private Button botonAceptar;
        [SerializeField] private Button botonCerrarPopup;
        [SerializeField] private Button botonAbrirPopup;    // el Button del slot "+"

        [Header("Avatares (sprites placeholder o definitivos)")]
        [SerializeField] private Sprite[] avatares;

        [Header("Navegacion")]
        [SerializeField] private string escenaTablero = "Tablero";
        [SerializeField] private bool empezarConListaVacia = true;

        private readonly List<SlotJugador> slots = new List<SlotJugador>();
        private readonly List<BotonAvatar> botonesAvatar = new List<BotonAvatar>();
        private int avatarElegido = -1;

        private void Awake()
        {
            if (!ReferenciasCompletas())
            {
                Debug.LogError("[SeleccionJugadores] Faltan referencias en el Inspector. " +
                    "Ejecuta el menu 'Cubatis > Construir escena Seleccion de Jugadores' " +
                    "(o 'Reasignar prefabs a SeleccionJugadores').", this);
                enabled = false;
                return;
            }

            if (empezarConListaVacia) Jugadores.Limpiar();

            campoNombre.characterLimit = 15;
            campoNombre.onValueChanged.AddListener(_ => ActualizarAceptar());

            botonAbrirPopup.onClick.AddListener(AbrirPopup);
            botonCerrarPopup.onClick.AddListener(CerrarPopup);
            botonAceptar.onClick.AddListener(Aceptar);
            botonEmpezar.onClick.AddListener(Empezar);

            ConstruirGridAvatares();
        }

        private void Start()
        {
            if (!enabled) return;
            popup.SetActive(false);
            RefrescarGrid();
        }

        private bool ReferenciasCompletas() =>
            contenedorSlots && prefabSlot && botonMas && botonEmpezar && popup &&
            contenedorAvatares && prefabBotonAvatar && campoNombre && botonAceptar &&
            botonCerrarPopup && botonAbrirPopup;

        // ===================== GRID DE JUGADORES =====================
        public void RefrescarGrid()
        {
            foreach (var s in slots) if (s != null) Destroy(s.gameObject);
            slots.Clear();

            botonMas.transform.SetAsFirstSibling();
            botonMas.SetActive(Jugadores.HaySitio);

            int visibles = Mathf.Min(Jugadores.VisiblesMax, Jugadores.Cuenta);
            for (int i = 0; i < visibles; i++)
            {
                int indice = i;
                var slot = Instantiate(prefabSlot, contenedorSlots);
                slot.Configurar(Jugadores.Lista[i].nombre, ObtenerAvatar(Jugadores.Lista[i].avatar),
                    () => { Jugadores.Quitar(indice); RefrescarGrid(); });
                slots.Add(slot);
            }

            botonEmpezar.interactable = Jugadores.SuficientesParaJugar;
        }

        // ===================== POPUP =====================
        private void ConstruirGridAvatares()
        {
            int n = avatares != null ? avatares.Length : 0;
            for (int i = 0; i < n; i++)
            {
                int indice = i;
                var b = Instantiate(prefabBotonAvatar, contenedorAvatares);
                b.Configurar(avatares[i], () => ElegirAvatar(indice));
                botonesAvatar.Add(b);
            }
        }

        public void AbrirPopup()
        {
            avatarElegido = -1;
            campoNombre.text = "";
            foreach (var b in botonesAvatar) b.MarcarSeleccion(false);
            ActualizarAceptar();
            popup.SetActive(true);
        }

        public void CerrarPopup() => popup.SetActive(false);

        private void ElegirAvatar(int i)
        {
            avatarElegido = i;
            for (int k = 0; k < botonesAvatar.Count; k++) botonesAvatar[k].MarcarSeleccion(k == i);
            ActualizarAceptar();
        }

        private void ActualizarAceptar()
        {
            botonAceptar.interactable = avatarElegido >= 0 && !string.IsNullOrWhiteSpace(campoNombre.text);
        }

        public void Aceptar()
        {
            if (avatarElegido < 0 || string.IsNullOrWhiteSpace(campoNombre.text)) return;
            Jugadores.Anadir(campoNombre.text.Trim(), avatarElegido);
            CerrarPopup();
            RefrescarGrid();
        }

        // ===================== NAVEGACION =====================
        public void Empezar()
        {
            if (Jugadores.SuficientesParaJugar) SceneManager.LoadScene(escenaTablero);
        }

        private Sprite ObtenerAvatar(int i)
        {
            if (avatares == null || avatares.Length == 0) return null;
            return avatares[((i % avatares.Length) + avatares.Length) % avatares.Length];
        }
    }
}
