using UnityEditor;
using UnityEngine;

namespace Cubatis.EditorTools
{
    /// <summary>
    /// Inspector de <see cref="CartaReto"/>: boton "Vista previa en Editor" que
    /// coloca la carta y su texto en la vista Scene sin entrar en Play, para
    /// ajustar 'Zona Texto' / 'Offset Texto' viendo el resultado real.
    /// </summary>
    [CustomEditor(typeof(CartaReto))]
    public class CartaRetoEditor : Editor
    {
        private void OnEnable()
        {
            var carta = target as CartaReto;
            if (carta != null && carta.VistaPreviaActiva && !Application.isPlaying)
                // false: re-mostrar sin recolocar el texto (no pisar un arrastre manual).
                EditorApplication.delayCall += () => { if (carta != null) carta.RefrescarVistaPrevia(false); };
        }

        public override void OnInspectorGUI()
        {
            var carta = (CartaReto)target;

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool cambiado = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Vista previa en Editor", EditorStyles.boldLabel);

            bool activa = carta.VistaPreviaActiva;
            GUI.backgroundColor = activa ? new Color(1f, 0.55f, 0.55f) : new Color(0.55f, 1f, 0.55f);
            if (GUILayout.Button(activa ? "Desactivar vista previa" : "Activar vista previa", GUILayout.Height(30)))
            {
                bool activar = !activa;
                // Diferido: crear/destruir hijos dentro de OnInspectorGUI deja objetos a medio hacer.
                EditorApplication.delayCall += () =>
                {
                    if (carta == null) return;
                    if (activar) carta.ActivarVistaPrevia();
                    else carta.DesactivarVistaPrevia();
                    EditorUtility.SetDirty(carta);
                    SceneView.RepaintAll();
                };
            }
            GUI.backgroundColor = Color.white;

            if (activa)
            {
                EditorGUILayout.HelpBox(
                    "Elige la categoria en 'Categoria Vista Previa'. Mueve/escala a mano los GameObjects " +
                    "'Carta' y/o 'TextoReto' en la Scene y pulsa 'Fijar ajuste de esta categoria': " +
                    "guarda escala+posicion de la carta y del texto SOLO para esa categoria (marca 'ajuste propio'). " +
                    "Guarda la escena (Ctrl+S). Desactiva la vista previa antes de jugar / hacer build.",
                    MessageType.Info);

                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                if (GUILayout.Button("Fijar ajuste de esta categoria", GUILayout.Height(26)))
                    carta.FijarAjusteDeCategoria();
                GUI.backgroundColor = Color.white;

                if (cambiado)
                    EditorApplication.delayCall += () =>
                    {
                        if (carta != null) carta.RefrescarVistaPrevia();
                        SceneView.RepaintAll();
                    };
            }
        }
    }
}
