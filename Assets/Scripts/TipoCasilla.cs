namespace Cubatis
{
    /// <summary>
    /// Tipo de cada casilla del tablero. Para las casillas numeradas (1..58)
    /// es su "tipo de reto" y engancha la logica del juego; ademas selecciona
    /// el sprite (uno por valor, en Assets/Boards/Casillas).
    /// </summary>
    public enum TipoCasilla
    {
        Start,   // casilla de salida (2 celdas, sin numero)
        Beber,
        YoNunca,
        Verdad,
        Reto,
        Evento,
        Hot,
        End      // casilla final (bloque 2x2 central, sin numero)
    }
}
