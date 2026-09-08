using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Cubatis.Editor
{
    [CustomEditor(typeof(BoardManager))]
    public class BoardManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            BoardManager manager = (BoardManager)target;

            // Dibujar inspector por defecto
            DrawDefaultInspector();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Herramientas de Generación de Tablero", EditorStyles.boldLabel);

            // Estado de los sprites
            bool spritesListos = manager.ValidarSprites();
            if (spritesListos)
            {
                EditorGUILayout.HelpBox("✔ Todos los 8 sprites requeridos están asignados correctamente.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ Faltan uno o más sprites de casilla por asignar.", MessageType.Warning);
                if (GUILayout.Button("🔍 Buscar y Cargar Sprites Automáticamente", GUILayout.Height(28)))
                {
                    manager.CargarSpritesAutomaticamente();
                }
            }

            EditorGUILayout.Space(8);

            // Botón Generar Tablero
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
            if (GUILayout.Button("🎲 Generar Tablero (63 Casillas)", GUILayout.Height(38)))
            {
                manager.GenerarTablero();
            }

            GUI.backgroundColor = new Color(0.95f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑 Limpiar Tablero", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Limpiar Tablero", "¿Seguro que deseas eliminar las casillas generadas?", "Sí, Limpiar", "Cancelar"))
                {
                    manager.LimpiarTablero();
                }
            }
            GUI.backgroundColor = Color.white;

            // Resumen de casillas actuales
            if (manager.TotalCasillas > 0)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("Estadísticas del Tablero Actual", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    $"Total de casillas: {manager.TotalCasillas}\n" +
                    $"• START (Casilla 0): 1 (Doble ancho)\n" +
                    $"• Beber: {manager.CasillasData.Count(c => c != null && c.Categoria == TipoCasilla.Beber)} (Esperado: 19)\n" +
                    $"• Yo nunca: {manager.CasillasData.Count(c => c != null && c.Categoria == TipoCasilla.YoNunca)} (Esperado: 12)\n" +
                    $"• Verdad: {manager.CasillasData.Count(c => c != null && c.Categoria == TipoCasilla.Verdad)} (Esperado: 9)\n" +
                    $"• Reto: {manager.CasillasData.Count(c => c != null && c.Categoria == TipoCasilla.Reto)} (Esperado: 9)\n" +
                    $"• Evento: {manager.CasillasData.Count(c => c != null && c.Categoria == TipoCasilla.Evento)} (Esperado: 6)\n" +
                    $"• Hot: {manager.CasillasData.Count(c => c != null && c.Categoria == TipoCasilla.Hot)} (Esperado: 6)\n" +
                    $"• END (Casilla 62): 1 (3x3 en el centro)",
                    MessageType.None
                );
            }
        }
    }
}
