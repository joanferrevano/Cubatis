using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cubatis.EditorTools
{
    /// <summary>
    /// Inspector de <see cref="GeneradorTablero"/>: botones para cargar los
    /// sprites, generar el tablero en espiral y verificar el recorrido.
    /// </summary>
    [CustomEditor(typeof(GeneradorTablero))]
    public class GeneradorTableroEditor : Editor
    {
        private const string CarpetaSprites = "Assets/Boards/Casillas";

        // Campo serializado -> nombre de archivo del sprite (sin extension).
        private static readonly (string campo, string archivo)[] Sprites =
        {
            ("spriteStart", "START"), ("spriteEnd", "END"),
            ("spriteBeber", "Beber"), ("spriteYoNunca", "Yo nunca"),
            ("spriteVerdad", "Verdad"), ("spriteReto", "Reto"),
            ("spriteEvento", "Evento"), ("spriteHot", "Hot"),
        };

        public override void OnInspectorGUI()
        {
            var generador = (GeneradorTablero)target;
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Tablero", EditorStyles.boldLabel);

            if (!generador.SpritesAsignados)
            {
                EditorGUILayout.HelpBox("Faltan sprites por asignar.", MessageType.Warning);
                if (GUILayout.Button("Cargar sprites desde " + CarpetaSprites, GUILayout.Height(24)))
                    CargarSprites();
            }
            else
            {
                EditorGUILayout.HelpBox("Los 8 sprites estan asignados.", MessageType.Info);
            }

            EditorGUILayout.Space(4);
            GUI.enabled = generador.SpritesAsignados;
            GUI.backgroundColor = new Color(0.5f, 0.85f, 0.5f);
            if (GUILayout.Button($"Generar tablero en espiral ({GeneradorTablero.CeldasNumeradas} casillas)", GUILayout.Height(34)))
                Diferir(generador, g => g.GenerarTablero());
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Limpiar", GUILayout.Height(22)))
                    Diferir(generador, g => g.LimpiarTablero());
                if (GUILayout.Button("Verificar camino (consola)", GUILayout.Height(22)))
                    generador.VerificarCamino();
            }

            if (generador.Total > 0)
            {
                if (GUILayout.Button(generador.MostrarNumeros ? "Ocultar numeros" : "Mostrar numeros"))
                {
                    Undo.RecordObject(generador, "Numeros");
                    generador.MostrarNumeros = !generador.MostrarNumeros;
                }

                var numeradas = generador.Casillas.Where(c => c != null && c.EsNumerada).ToList();
                string resumen = $"Objetos: {generador.Total}  (1 START + {numeradas.Count} numeradas + 1 END)\n" +
                    string.Join("   ", System.Enum.GetValues(typeof(TipoCasilla)).Cast<TipoCasilla>()
                        .Where(t => t != TipoCasilla.Start && t != TipoCasilla.End)
                        .Select(t => $"{t}: {numeradas.Count(c => c.Tipo == t)}"));
                EditorGUILayout.HelpBox(resumen, MessageType.None);
            }
        }

        // Ejecuta la accion FUERA del pintado del Inspector: mutar la jerarquia
        // (new GameObject / AddComponent) durante OnInspectorGUI deja objetos a
        // medio crear y provoca MissingComponentException.
        private static void Diferir(GeneradorTablero g, Action<GeneradorTablero> accion)
        {
            EditorApplication.delayCall += () =>
            {
                if (g == null) return;
                accion(g);
                EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
            };
        }

        private void CargarSprites()
        {
            var so = new SerializedObject(target);
            foreach (var (campo, archivo) in Sprites)
            {
                string ruta = $"{CarpetaSprites}/{archivo}.png";
                Sprite sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(ruta).OfType<Sprite>().FirstOrDefault()
                                ?? AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
                if (sprite == null) { Debug.LogWarning($"[Tablero] No se encontro {ruta}"); continue; }
                so.FindProperty(campo).objectReferenceValue = sprite;
            }
            so.ApplyModifiedProperties();
        }
    }
}
