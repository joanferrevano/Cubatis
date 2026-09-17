using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Cubatis
{
    /// <summary>
    /// Orquesta la partida en la escena del Tablero:
    ///   - Crea una ficha por cada jugador de <see cref="Jugadores.Lista"/> en
    ///     la casilla START (indice 0), con el avatar que cada uno eligio.
    ///   - Turnos por orden de la lista (0, 1, 2, ... y vuelta al 0).
    ///   - Solo se puede tirar el dado en el turno actual y con nada en
    ///     movimiento; el resto del tiempo el dado queda bloqueado.
    ///   - Al salir resultado, mueve la ficha del jugador de turno casilla a
    ///     casilla (via <see cref="MovimientoFicha"/>); ver "Victoria" abajo
    ///     para que pasa si la tirada se pasa de END.
    ///
    ///   - Al caer en una casilla numerada, si hay <see cref="CartaReto"/>
    ///     asignada abre su popup y el turno queda en pausa hasta que el jugador
    ///     cierre la carta (<see cref="FinalizarTurno"/>). El evento suelto
    ///     <see cref="alCaerEnCasilla"/> se sigue emitiendo por si algo mas lo usa.
    ///
    ///   - Victoria: si la tirada se pasa de la casilla final (END), la ficha
    ///     avanza hasta el final y RETROCEDE el sobrante en vez de salirse del
    ///     tablero (regla clasica del juego de la oca). Solo gana si cae EXACTO
    ///     en END (sobrante = 0); ese jugador dispara <see cref="alTerminarJuego"/>,
    ///     se guarda en <see cref="DatosPartida"/> y se carga la escena de
    ///     Ranking. La partida queda bloqueada (no se pasa turno ni se
    ///     desbloquea el dado). El resto de jugadores no sigue jugando, asi
    ///     que el 2o/3o puesto (y siguientes) se completan con su casilla
    ///     actual en ese momento (el mas avanzado, mejor puesto) en vez de
    ///     dejarlos fuera del ranking (ver <see cref="Ganar"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class GestorPartida : MonoBehaviour
    {
        [Header("Referencias de escena")]
        [SerializeField] private GeneradorTablero tablero;
        [SerializeField] private MovimientoFicha movimiento;
        [SerializeField] private Dado dado;
        [Tooltip("Popup de carta de reto. Si esta asignado, al caer en una casilla numerada se abre y el turno no pasa hasta cerrarla.")]
        [SerializeField] private CartaReto carta;

        [Header("Fichas")]
        [Tooltip("Mismos sprites que la pantalla de seleccion; Jugador.avatar indexa aqui.")]
        [SerializeField] private Sprite[] avatares;
        [Tooltip("Lado aproximado de la ficha en unidades de mundo.")]
        [SerializeField] private float tamanoFicha = 0.45f;
        [Tooltip("Separacion entre fichas que comparten casilla (unidades de mundo).")]
        [SerializeField] private float separacionFichas = 0.18f;
        [SerializeField] private string sortingLayerFichas = "Default";
        [Tooltip("Orden de dibujo base de las fichas (debe quedar por encima de las casillas).")]
        [SerializeField] private int ordenFichas = 500;

        [Header("Pruebas")]
        [Tooltip("Solo en el editor: si entras directo a la escena Tablero sin pasar por Seleccion, crea jugadores de prueba para poder testear.")]
        [SerializeField] private bool jugadoresDePruebaSiVacio = true;
        [SerializeField] private int jugadoresDePrueba = 3;

        [Header("Navegacion")]
        [Tooltip("Escena a la que se salta al terminar la partida (ranking final).")]
        [SerializeField] private string escenaRanking = "Ranking";

        [Header("UI: turno actual")]
        [Tooltip("Texto (TMP) 'NombreJugador': solo el nombre, la etiqueta 'Turno del Jugador' es otro texto aparte. Se actualiza al empezar la partida y en cada cambio de turno.")]
        [SerializeField] private TMP_Text nombreJugadorTexto;

        [Header("Gancho: cambio de turno")]
        [Tooltip("Se invoca al empezar la partida (primer jugador) y cada vez que pasa el turno, con el jugador que le toca jugar.")]
        public UnityEvent<Jugador> alCambiarTurno;

        [Header("Gancho: al caer en una casilla")]
        [Tooltip("Se invoca al terminar el movimiento con (indiceJugador, casillaDestino). La carta de reto se abre aparte via la referencia 'carta'; este evento es para logica extra opcional.")]
        public UnityEvent<int, Casilla> alCaerEnCasilla;

        [Header("Gancho: fin de partida")]
        [Tooltip("Se invoca cuando un jugador gana (cae EXACTO en la casilla final), con la lista de jugadores en orden de llegada: el ganador primero, seguido del resto ordenado por su casilla actual (el mas avanzado, mejor puesto).")]
        public UnityEvent<List<Jugador>> alTerminarJuego;

        private readonly List<Transform> fichas = new List<Transform>();
        private readonly List<int> posiciones = new List<int>();   // indice de casilla de cada ficha
        private readonly List<Jugador> ordenLlegada = new List<Jugador>();
        private int turno;
        private bool ocupado;     // tirada o movimiento en curso
        private bool terminado;   // ya gano alguien: partida bloqueada

        public int TurnoActual => turno;

        private void Reset()
        {
            tablero = FindAnyObjectByType<GeneradorTablero>();
            movimiento = FindAnyObjectByType<MovimientoFicha>();
            dado = FindAnyObjectByType<Dado>();
            carta = FindAnyObjectByType<CartaReto>();
        }

        private void Awake()
        {
            if (tablero == null) tablero = FindAnyObjectByType<GeneradorTablero>();
            if (movimiento == null) movimiento = FindAnyObjectByType<MovimientoFicha>();
            if (dado == null) dado = FindAnyObjectByType<Dado>();
            if (carta == null) carta = FindAnyObjectByType<CartaReto>();
        }

        private void OnEnable()
        {
            if (dado != null) dado.alSalirResultado.AddListener(OnResultadoDado);
        }

        private void OnDisable()
        {
            if (dado != null) dado.alSalirResultado.RemoveListener(OnResultadoDado);
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (jugadoresDePruebaSiVacio && Jugadores.Cuenta < 2)
            {
                Jugadores.Limpiar();
                for (int i = 0; i < Mathf.Max(2, jugadoresDePrueba); i++)
                    Jugadores.Anadir($"Test {i + 1}", i);
                Debug.LogWarning($"[GestorPartida] Sin jugadores reales: creados {Jugadores.Cuenta} de prueba.");
            }
#endif
            AsegurarAvatares();
            if (!Validar()) { enabled = false; return; }
            CrearFichas();
            turno = 0;
            ActualizarDado();
            ActualizarNombreJugador();
        }

        private bool Validar()
        {
            if (tablero == null || movimiento == null || dado == null)
            { Debug.LogError("[GestorPartida] Faltan referencias: tablero / movimiento / dado.", this); return false; }
            if (tablero.Total < 2)
            { Debug.LogError("[GestorPartida] El tablero no esta generado (usa 'Generar tablero').", this); return false; }
            if (Jugadores.Cuenta < 2)
            { Debug.LogError("[GestorPartida] Hacen falta minimo 2 jugadores en Jugadores.Lista.", this); return false; }
            if (avatares == null || avatares.Length == 0)
            { Debug.LogError("[GestorPartida] Asigna los sprites de avatar (menu contextual 'Cargar avatares...').", this); return false; }
            return true;
        }

        // ===================== FICHAS =====================
        private void CrearFichas()
        {
            var contenedor = new GameObject("Fichas").transform;
            contenedor.SetParent(tablero.transform, false);

            for (int i = 0; i < Jugadores.Cuenta; i++)
            {
                Jugador j = Jugadores.Lista[i];
                var go = new GameObject($"Ficha_{i:00}_{j.nombre}", typeof(SpriteRenderer));
                go.transform.SetParent(contenedor, false);

                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = AvatarDe(j.avatar);
                sr.sortingLayerName = string.IsNullOrEmpty(sortingLayerFichas) ? "Default" : sortingLayerFichas;
                sr.sortingOrder = ordenFichas + i;
                if (sr.sprite != null)
                {
                    float lado = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;
                    go.transform.localScale = Vector3.one * (tamanoFicha / Mathf.Max(0.0001f, lado));
                }

                fichas.Add(go.transform);
                posiciones.Add(0);   // TODOS arrancan en START
            }

            SepararFichasEn(0);
        }

        private Sprite AvatarDe(int i)
        {
            if (avatares == null || avatares.Length == 0) return null;
            return avatares[((i % avatares.Length) + avatares.Length) % avatares.Length];
        }

        // Coloca en rejilla centrada las fichas que comparten una casilla para
        // que no se solapen del todo (incluye el caso inicial: todas en START).
        private void SepararFichasEn(int casilla)
        {
            var enCasilla = new List<int>();
            for (int i = 0; i < posiciones.Count; i++)
                if (posiciones[i] == casilla) enCasilla.Add(i);

            Vector3 centro = tablero.ObtenerPosicion(casilla);
            int n = enCasilla.Count;
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
            int filas = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));

            for (int k = 0; k < n; k++)
            {
                Vector3 off = n <= 1
                    ? Vector3.zero
                    : new Vector3((k % cols) - (cols - 1) / 2f, (k / cols) - (filas - 1) / 2f, 0f) * separacionFichas;
                fichas[enCasilla[k]].position = centro + off;
            }
        }

        // ===================== TURNOS + DADO =====================
        private void OnResultadoDado(int resultado)
        {
            if (ocupado || terminado || !enabled) return;

            ocupado = true;
            ActualizarDado();

            int jugador = turno;
            int origen = posiciones[jugador];
            int casillaFinal = tablero.Total - 1;   // END
            int suma = origen + resultado;

            // Si la tirada se pasa de la casilla final, la ficha avanza hasta el
            // final y retrocede el sobrante en vez de salirse del tablero (regla
            // del rebote). Solo gana si cae EXACTO en END (sobrante == 0).
            int destino;
            bool gana;
            if (suma >= casillaFinal)
            {
                int sobrante = suma - casillaFinal;
                destino = Mathf.Clamp(casillaFinal - sobrante, 0, casillaFinal);
                gana = sobrante == 0;
            }
            else
            {
                destino = suma;
                gana = false;
            }

            Transform ficha = fichas[jugador];

            Action alLlegar = () =>
            {
                posiciones[jugador] = destino;
                SepararFichasEn(origen);
                SepararFichasEn(destino);

                Casilla casilla = tablero.ObtenerCasilla(destino);
                AlCaerEnCasilla(jugador, casilla);

                if (gana)
                {
                    // Partida terminada: se deja 'ocupado' a true (dado bloqueado)
                    // y no se pasa turno.
                    Ganar(jugador);
                    return;
                }

                // Casilla numerada con carta -> abre el popup y NO pasa el turno
                // (ni desbloquea el dado) hasta que el jugador la cierre.
                if (carta != null && casilla != null && casilla.EsNumerada && carta.TieneCarta(casilla.Tipo))
                    carta.Abrir(casilla.Tipo, FinalizarTurno);
                else
                    FinalizarTurno();
            };

            // Rebote real (la tirada se paso de END) -> animacion en dos tramos
            // visibles: ida hasta la casilla final y vuelta hasta 'destino'.
            if (suma > casillaFinal)
                movimiento.MoverConRebote(ficha, origen, casillaFinal, destino, alLlegar);
            else
                movimiento.Mover(ficha, origen, destino, alLlegar);
        }

        // Pasa el turno al siguiente jugador y vuelve a habilitar el dado.
        private void FinalizarTurno()
        {
            turno = (turno + 1) % Jugadores.Cuenta;   // vuelve al 0 tras el ultimo
            ocupado = false;
            ActualizarDado();
            ActualizarNombreJugador();
        }

        // Refleja el jugador en turno en el texto de UI y avisa via 'alCambiarTurno'
        // (por si algun otro sistema quiere reaccionar, igual que con alCaerEnCasilla).
        private void ActualizarNombreJugador()
        {
            Jugador j = Jugadores.Lista[turno];
            if (nombreJugadorTexto != null) nombreJugadorTexto.text = j.nombre;
            alCambiarTurno?.Invoke(j);
        }

        /// <summary>
        /// PUNTO DE ENGANCHE para la logica de retos (pendiente). De momento solo
        /// propaga el evento; aqui ira el "que pasa segun casilla.Tipo".
        /// </summary>
        private void AlCaerEnCasilla(int jugador, Casilla casilla)
        {
            alCaerEnCasilla?.Invoke(jugador, casilla);
        }

        // Punto unico de victoria: lo dispara tanto una tirada real que cae
        // EXACTO en END (ver OnResultadoDado) como ForzarVictoriaDebug. Deja
        // 'ocupado' a true (dado bloqueado) y no pasa turno.
        private void Ganar(int jugador)
        {
            terminado = true;
            ordenLlegada.Add(Jugadores.Lista[jugador]);

            // El resto de jugadores no sigue jugando (la partida se bloquea
            // en cuanto alguien llega a END), asi que 2o/3o puesto salen de
            // su casilla actual en este momento: el mas avanzado queda 2o,
            // el siguiente 3o... En empate de casilla, gana el que tenga
            // turno mas bajo (orden estable).
            var restantes = new List<int>();
            for (int i = 0; i < Jugadores.Cuenta; i++)
                if (i != jugador) restantes.Add(i);
            restantes.Sort((a, b) =>
            {
                int porCasilla = posiciones[b].CompareTo(posiciones[a]);
                return porCasilla != 0 ? porCasilla : a.CompareTo(b);
            });
            foreach (int i in restantes) ordenLlegada.Add(Jugadores.Lista[i]);

            alTerminarJuego?.Invoke(ordenLlegada);

            DatosPartida.GuardarRanking(ordenLlegada);
            SceneManager.LoadScene(escenaRanking);
        }

        /// <summary>
        /// SOLO DEPURACION: mueve instantaneamente al jugador en turno hasta
        /// END y dispara <see cref="Ganar"/> - la misma logica de victoria que
        /// una tirada real (alTerminarJuego, DatosPartida, carga de Ranking),
        /// sin jugar la partida entera. Pensado para un boton de debug en la
        /// escena; no lo llama nada mas.
        /// </summary>
        public void ForzarVictoriaDebug()
        {
            if (ocupado || terminado || !enabled) return;

            int jugador = turno;
            int origen = posiciones[jugador];
            int destino = tablero.Total - 1;   // END

            ocupado = true;
            ActualizarDado();

            movimiento.Colocar(fichas[jugador], destino);
            posiciones[jugador] = destino;
            SepararFichasEn(origen);
            SepararFichasEn(destino);

            AlCaerEnCasilla(jugador, tablero.ObtenerCasilla(destino));
            Ganar(jugador);
        }

        private void ActualizarDado()
        {
            if (dado != null) dado.PuedeTirar = !ocupado;
        }

        // Si el array esta vacio, lo rellena leyendo Assets/Boards/Avatares en el
        // editor (asi el prototipo funciona aunque no se haya arrastrado nada).
        // Para el build hay que asignar el array a mano en el Inspector.
        private void AsegurarAvatares()
        {
#if UNITY_EDITOR
            if (avatares != null && avatares.Length > 0) return;
            avatares = CargarAvataresDeDisco();
            if (avatares.Length > 0)
                Debug.LogWarning($"[GestorPartida] Avatares auto-cargados en editor ({avatares.Length}). " +
                    "Asignalos en el Inspector para que funcionen en el build.");
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Cargar avatares desde Assets/Boards/Avatares")]
        private void CargarAvatares()
        {
            avatares = CargarAvataresDeDisco();
            EditorUtility.SetDirty(this);
            Debug.Log($"[GestorPartida] {avatares.Length} avatares cargados.");
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
