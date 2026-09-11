using System.Collections.Generic;

namespace Cubatis
{
    /// <summary>
    /// Ranking final de la partida (jugadores en orden de llegada al ganar),
    /// compartido entre la escena del Tablero y la de Ranking. Clase estatica,
    /// igual que <see cref="Jugadores"/>: los datos no cuelgan de ningun
    /// GameObject, asi que sobreviven a <c>SceneManager.LoadScene</c> sin
    /// necesidad de <c>DontDestroyOnLoad</c> (un objeto con DontDestroyOnLoad
    /// solo hace falta para sobrevivir cuando ALGO debe seguir actualizandose o
    /// recibiendo eventos entre escenas; aqui solo hay datos que leer).
    ///
    /// Reutiliza <see cref="Jugador"/> (nombre + indice de avatar) en vez de un
    /// tipo nuevo: es exactamente lo que <see cref="GestorPartida.alTerminarJuego"/>
    /// ya entrega, y evita duplicar el mismo struct con otro nombre.
    /// </summary>
    public static class DatosPartida
    {
        public static readonly List<Jugador> Ranking = new List<Jugador>();

        /// <summary>Sustituye el ranking guardado por 'ordenLlegada' (copia los valores).</summary>
        public static void GuardarRanking(IReadOnlyList<Jugador> ordenLlegada)
        {
            Ranking.Clear();
            if (ordenLlegada == null) return;
            for (int i = 0; i < ordenLlegada.Count; i++) Ranking.Add(ordenLlegada[i]);
        }

        public static void Limpiar() => Ranking.Clear();
    }
}
