using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cubatis
{
    /// <summary>
    /// Boton de un avatar dentro del popup. El diseno se edita en el prefab
    /// BotonAvatar; aqui solo se asigna el sprite y el estado de seleccion.
    /// </summary>
    public class BotonAvatar : MonoBehaviour
    {
        [SerializeField] private Image imagen;
        [SerializeField] private GameObject marcaSeleccion;
        [SerializeField] private Button boton;

        public void Configurar(Sprite sprite, Action alPulsar)
        {
            if (imagen != null) imagen.sprite = sprite;
            MarcarSeleccion(false);

            boton.onClick.RemoveAllListeners();
            boton.onClick.AddListener(() => alPulsar?.Invoke());
        }

        public void MarcarSeleccion(bool seleccionado)
        {
            if (marcaSeleccion != null) marcaSeleccion.SetActive(seleccionado);
        }
    }
}
