namespace Cubatis
{
    /// <summary>
    /// Tipo de cada casilla del tablero. Para las casillas numeradas (1..58)
    /// es su "tipo de reto" y engancha la logica del juego; ademas selecciona
    /// el sprite (uno por valor, en Assets/Boards/Casillas).
    ///
    /// Las categorias del modo Pareja van DESPUES de End y con valor explicito:
    /// escenas y prefabs guardan el enum como entero, asi que insertarlas en
    /// medio desplazaria los tipos ya serializados. RetoPareja es distinta de
    /// Reto (otras cartas y otras frases), por eso no se reutiliza.
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
        End,     // casilla final (bloque 2x2 central, sin numero)

        // Modo Pareja
        Conocimiento = 8,
        Conexion = 9,
        Confesion = 10,
        RetoPareja = 11
    }
}
