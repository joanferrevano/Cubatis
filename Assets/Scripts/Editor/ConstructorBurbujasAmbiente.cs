using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cubatis.EditorTools
{
    /// <summary>
    /// Genera el prefab BurbujasAmbiente (ParticleSystem de Shuriken) y lo
    /// coloca en las escenas. El prefab se construye por codigo en vez de a
    /// mano para que todos los valores del efecto queden versionados y
    /// comentados aqui (un ParticleSystem serializado son miles de lineas de
    /// YAML ilegible).
    ///
    /// "Aplicar a escenas" tambien convierte los fondos de UI (Image de
    /// "Fondo Personajes" dentro de un Canvas Overlay) en sprites de mundo como
    /// el del Tablero: un Canvas Overlay se dibuja encima de todo lo del mundo,
    /// asi que con el fondo en el Canvas las burbujas quedarian tapadas.
    /// Orden resultante: fondo (-1000) -> burbujas (-500) -> tablero (0+) ->
    /// UI Overlay. Ambos pasos son idempotentes.
    /// </summary>
    public static class ConstructorBurbujasAmbiente
    {
        private const string RutaPrefab = "Assets/Prefabs/BurbujasAmbiente.prefab";
        private const string RutaSpriteBurbuja = "Assets/Art/Particulas/Burbuja.png";
        private const string RutaFondo = "Assets/Art/Fondos/Fondo Personajes.png";

        private static readonly string[] Escenas =
        {
            "Assets/Scenes/SeleccionJugadores.unity",
            "Assets/Scenes/ModosJuegos.unity",
            "Assets/Scenes/Tablero.unity",
            "Assets/Scenes/Ranking.unity",
        };

        /// <summary>Entre el fondo (FondoJuego, -1000) y el tablero (casillas desde 0).</summary>
        private const int OrdenBurbujas = -500;

        // Pensado para tamano ortografico 5 (vista de 10 de alto). Ver BurbujasAmbiente.
        private const float VelocidadMin = 0.5f, VelocidadMax = 0.75f;
        // Recorrido: 10 de pantalla + 0.5 de margen abajo + 0.5 arriba = 11.
        // La vida cubre a la burbuja mas lenta (11 / 0.5 = 22 s); las rapidas
        // salen antes por arriba y el resto de su vida ocurre fuera de pantalla.
        private const float Vida = 22f;
        // ~0.7/s con ~16 s de media en pantalla = unas 10-12 burbujas visibles.
        private const float Ritmo = 0.7f;

        [MenuItem("Cubatis/Burbujas/Crear o actualizar prefab BurbujasAmbiente")]
        public static GameObject CrearPrefab()
        {
            var sprite = PrepararSpriteBurbuja();
            if (sprite == null)
            {
                Debug.LogError($"[Burbujas] No se encuentra {RutaSpriteBurbuja}.");
                return null;
            }

            var go = new GameObject("BurbujasAmbiente");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = Vida;               // prewarm simula un ciclo: asi llena la pantalla entera
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;           // lo arranca BurbujasAmbiente tras encajarse a la camara
            main.startLifetime = Vida;
            main.startSpeed = 0f;               // la subida va en Velocity over Lifetime (independiente de la forma)
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.18f), new Color(1f, 1f, 1f, 0.40f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.useUnscaledTime = true;
            main.maxParticles = 30;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emision = ps.emission;
            emision.rateOverTime = Ritmo;

            // Linea horizontal bajo la pantalla; BurbujasAmbiente ajusta el ancho.
            var forma = ps.shape;
            forma.shapeType = ParticleSystemShapeType.Box;
            forma.scale = new Vector3(6.6f, 0.2f, 0f);
            forma.rotation = Vector3.zero;

            // Velocidad por particula elegida al azar entre dos constantes y
            // mantenida toda su vida: unas suben algo mas rapido que otras.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(VelocidadMin, VelocidadMax);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Wobble lateral: ruido suave solo en X, sin tocar la subida.
            var ruido = ps.noise;
            ruido.enabled = true;
            ruido.separateAxes = true;
            ruido.strengthX = 0.35f;
            ruido.strengthY = 0f;
            ruido.strengthZ = 0f;
            ruido.frequency = 0.25f;
            ruido.scrollSpeed = 0.15f;
            ruido.damping = true;
            ruido.octaveCount = 1;
            ruido.quality = ParticleSystemNoiseQuality.Medium;

            // Aparecen y desaparecen con fundido; el final coincide con el borde
            // superior para las mas lentas.
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var degradado = new Gradient();
            degradado.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f),
                    new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f),
                });
            color.color = new ParticleSystem.MinMaxGradient(degradado);

            // Modo Sprites: el Sprites-Default (el mismo material que usan los
            // sprites del proyecto) recibe la textura de la burbuja.
            var hoja = ps.textureSheetAnimation;
            hoja.enabled = true;
            hoja.mode = ParticleSystemAnimationMode.Sprites;
            if (hoja.spriteCount == 0) hoja.AddSprite(sprite);
            else hoja.SetSprite(0, sprite);

            var render = go.GetComponent<ParticleSystemRenderer>();
            render.renderMode = ParticleSystemRenderMode.Billboard;
            render.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            render.sortingLayerName = "Default";
            render.sortingOrder = OrdenBurbujas;

            go.AddComponent<BurbujasAmbiente>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, RutaPrefab);
            Object.DestroyImmediate(go);
            Debug.Log($"[Burbujas] Prefab guardado en {RutaPrefab}.");
            return prefab;
        }

        [MenuItem("Cubatis/Burbujas/Aplicar burbujas a las escenas")]
        public static void AplicarAEscenas()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
            if (prefab == null) prefab = CrearPrefab();
            if (prefab == null) return;

            string escenaInicial = SceneManager.GetActiveScene().path;
            foreach (var ruta in Escenas)
            {
                var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                int fondos = MigrarFondosUI(escena);
                bool burbujas = InstanciarBurbujas(prefab);
                if (fondos > 0 || burbujas)
                {
                    EditorSceneManager.SaveScene(escena);
                    Debug.Log($"[Burbujas] {ruta}: {fondos} fondo(s) de UI pasados a mundo, burbujas {(burbujas ? "anadidas" : "ya estaban")}.");
                }
                else Debug.Log($"[Burbujas] {ruta}: sin cambios.");
            }
            if (!string.IsNullOrEmpty(escenaInicial)) EditorSceneManager.OpenScene(escenaInicial);
        }

        /// <summary>
        /// Crea el fondo de mundo en la escena activa: sprite + FondoJuego
        /// (sorting -1000) + FondoCubreCamara + FondoParallax. Lo usa tambien
        /// el constructor de SeleccionJugadores.
        /// </summary>
        public static GameObject CrearFondoMundo(Sprite sprite, Color color)
        {
            var go = new GameObject("Fondo");
            go.transform.SetSiblingIndex(0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            go.AddComponent<FondoJuego>();
            go.AddComponent<FondoCubreCamara>();
            go.AddComponent<FondoParallax>();
            return go;
        }

        /// <summary>Anade el prefab a la escena activa si no tiene ya unas burbujas.</summary>
        public static bool InstanciarBurbujas(GameObject prefab = null)
        {
            if (Object.FindAnyObjectByType<BurbujasAmbiente>(FindObjectsInactive.Include) != null) return false;
            if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
            if (prefab == null) prefab = CrearPrefab();
            if (prefab == null) return false;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            go.transform.SetSiblingIndex(Mathf.Min(1, go.transform.GetSiblingIndex()));
            return true;
        }

        public static Sprite CargarSpriteFondo()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(RutaFondo))
                if (o is Sprite s) return s;
            return null;
        }

        /// <summary>
        /// Sustituye cada Image de Canvas con el sprite de fondo por un fondo de
        /// mundo con el mismo sprite y color, conservando los ajustes del
        /// FondoParallax que tuviera.
        /// </summary>
        private static int MigrarFondosUI(Scene escena)
        {
            var spriteFondo = CargarSpriteFondo();
            if (spriteFondo == null) return 0;

            int n = 0;
            foreach (var raiz in escena.GetRootGameObjects())
            foreach (var img in raiz.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != spriteFondo || img.GetComponentInParent<Canvas>(true) == null) continue;
                if (img.GetComponent<Canvas>() != null)
                {
                    Debug.LogWarning($"[Burbujas] {escena.path}: el fondo esta en el propio Canvas '{img.name}', no se migra.");
                    continue;
                }

                var nuevo = CrearFondoMundo(img.sprite, img.color);
                var viejo = img.GetComponent<FondoParallax>();
                if (viejo != null) EditorUtility.CopySerialized(viejo, nuevo.GetComponent<FondoParallax>());
                Object.DestroyImmediate(img.gameObject);
                n++;
            }
            return n;
        }

        private static Sprite PrepararSpriteBurbuja()
        {
            if (AssetImporter.GetAtPath(RutaSpriteBurbuja) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single ||
                 !ti.alphaIsTransparency || ti.mipmapEnabled))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.spritePixelsPerUnit = 128;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(RutaSpriteBurbuja);
        }
    }
}
