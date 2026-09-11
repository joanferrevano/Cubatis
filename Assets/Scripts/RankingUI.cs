using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Cubatis
{
    /// <summary>
    /// Logica de la escena Ranking: pinta el avatar de los 3 primeros de
    /// <see cref="DatosPartida.Ranking"/> (orden de llegada al ganar la
    /// partida) en 'JugadorTop1/2/3' (Image de UI, igual que 'Top1/2/3' y el
    /// resto de la escena: un Canvas Screen Space - Overlay no depende de la
    /// camara para dibujar sus Image/Text/Button, pero un SpriteRenderer si;
    /// por eso antes salian invisibles aunque el sprite se asignara bien) y el
    /// boton para volver al menu.
    ///
    /// Si hay menos de 3 jugadores en el ranking (hoy siempre es asi: la
    /// partida termina con el primer ganador, ver <see cref="GestorPartida"/>)
    /// los puestos sin jugador se ocultan en vez de dejar el avatar placeholder
    /// que trae la escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class RankingUI : MonoBehaviour
    {
        [Header("Avatares (mismo indice que en Seleccion de Jugadores y en el Tablero)")]
        [Tooltip("Mismos sprites que la pantalla de seleccion; Jugador.avatar indexa aqui.")]
        [SerializeField] private Sprite[] avatares;

        [Header("Puestos")]
        [SerializeField] private Image jugadorTop1;
        [SerializeField] private Image jugadorTop2;
        [SerializeField] private Image jugadorTop3;

        [Header("Navegacion")]
        [SerializeField] private Button botonVolverMenu;
        [Tooltip("Escena del menu principal. Debe estar anadida a Build Settings.")]
        [SerializeField] private string escenaMenu = "MenuPrincipal";

        private void Awake()
        {
            AsegurarAvatares();
            if (botonVolverMenu != null) botonVolverMenu.onClick.AddListener(VolverAlMenu);
        }

        private void Start() => MostrarRanking();

        private void MostrarRanking()
        {
            IReadOnlyList<Jugador> ranking = DatosPartida.Ranking;
            AsignarPuesto(jugadorTop1, ranking, 0);
            AsignarPuesto(jugadorTop2, ranking, 1);
            AsignarPuesto(jugadorTop3, ranking, 2);
        }

        private void AsignarPuesto(Image img, IReadOnlyList<Jugador> ranking, int puesto)
        {
            if (img == null) return;
            bool hayJugador = ranking != null && puesto < ranking.Count;
            img.gameObject.SetActive(hayJugador);
            if (hayJugador) img.sprite = AvatarDe(ranking[puesto].avatar);
        }

        private Sprite AvatarDe(int i)
        {
            if (avatares == null || avatares.Length == 0) return null;
            return avatares[((i % avatares.Length) + avatares.Length) % avatares.Length];
        }

        public void VolverAlMenu() => SceneManager.LoadScene(escenaMenu);

        // Si el array esta vacio, lo rellena leyendo Assets/Boards/Avatares en el
        // editor (mismo mecanismo que GestorPartida.AsegurarAvatares). Para el
        // build hay que asignar el array a mano en el Inspector.
        private void AsegurarAvatares()
        {
#if UNITY_EDITOR
            if (avatares != null && avatares.Length > 0) return;
            avatares = CargarAvataresDeDisco();
            if (avatares.Length > 0)
                Debug.LogWarning($"[RankingUI] Avatares auto-cargados en editor ({avatares.Length}). " +
                    "Asignalos en el Inspector para que funcionen en el build.");
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Cargar avatares desde Assets/Boards/Avatares")]
        private void CargarAvatares()
        {
            avatares = CargarAvataresDeDisco();
            EditorUtility.SetDirty(this);
            Debug.Log($"[RankingUI] {avatares.Length} avatares cargados.");
        }

        private static Sprite[] CargarAvataresDeDisco()
        {
            const string carpeta = "Assets/Boards/Avatares";
            var lista = new List<Sprite>();
            for (int i = 0; i < 64; i++)
            {
                string ruta = $"{carpeta}/avatar_{i:00}.png";
                if (AssetDatabase.LoadMainAssetAtPath(ruta) == null) continue;   // hueco: sigue mirando
                Sprite sp = AssetDatabase.LoadAllAssetRepresentationsAtPath(ruta).OfType<Sprite>().FirstOrDefault()
                            ?? AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
                if (sp != null) lista.Add(sp);
            }
            return lista.ToArray();
        }
#endif
    }
}
