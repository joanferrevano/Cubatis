using System.Collections.Generic;

namespace Cubatis
{
    [System.Serializable]
    public struct Jugador
    {
        public string nombre;
        public int avatar;   // indice del avatar elegido

        public Jugador(string nombre, int avatar)
        {
            this.nombre = nombre;
            this.avatar = avatar;
        }
    }

    /// <summary>
    /// Lista de jugadores compartida entre la escena de seleccion y la del
    /// tablero. Datos estaticos: sobreviven al cambio de escena sin necesidad
    /// de un GameObject persistente ni de serializar nada.
    /// </summary>
    public static class Jugadores
    {
        public const int Maximo = 15;    // maximo logico de jugadores
        public const int VisiblesMax = 8; // jugadores visibles en el grid (+ boton "+" = 3x3)

        public static readonly List<Jugador> Lista = new List<Jugador>();

        public static int Cuenta => Lista.Count;
        public static bool HaySitio => Lista.Count < Maximo;
        public static bool SuficientesParaJugar => Lista.Count >= 2;

        public static void Anadir(string nombre, int avatar)
        {
            if (HaySitio) Lista.Add(new Jugador(nombre, avatar));
        }

        public static void Quitar(int indice)
        {
            if (indice >= 0 && indice < Lista.Count) Lista.RemoveAt(indice);
        }

        public static void Limpiar() => Lista.Clear();
    }
}
