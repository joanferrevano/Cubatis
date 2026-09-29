namespace Cubatis
{
    /// <summary>Modos de juego de las tarjetas de ModosJuegos.</summary>
    public enum ModoPartida
    {
        Clasico,
        Etilico,
        Hot,
        Pareja
    }

    /// <summary>
    /// Modo elegido en ModosJuegos, leido al arrancar el Tablero. Clase
    /// estatica, igual que <see cref="Jugadores"/> y <see cref="DatosPartida"/>:
    /// es un dato que se escribe en una escena y se lee en otra, asi que
    /// sobrevive al cambio de escena sin GameObject persistente.
    ///
    /// Todos los modos comparten la escena Tablero: la tarjeta de modo guarda
    /// aqui el suyo antes de cargarla y <see cref="GeneradorTablero"/> adapta
    /// las casillas al arrancar. Por defecto Clasico, que es tambien lo que se
    /// juega al darle a Play directamente en la escena Tablero.
    /// </summary>
    public static class ModoJuego
    {
        public static ModoPartida Actual { get; set; } = ModoPartida.Clasico;
    }
}
