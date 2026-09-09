using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Slot visual de un jugador en el grid. El diseno se edita en el prefab
    /// SlotJugador; aqui solo se rellenan los datos.
    /// </summary>
    public class SlotJugador : MonoBehaviour
    {
        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text nombre;
        [SerializeField] private Button botonQuitar;

        public void Configurar(string nombreJugador, Sprite spriteAvatar, Action alQuitar)
        {
            if (avatar != null) avatar.sprite = spriteAvatar;
            if (nombre != null) nombre.text = nombreJugador;

            botonQuitar.onClick.RemoveAllListeners();
            botonQuitar.onClick.AddListener(() => alQuitar?.Invoke());
        }
    }
}
