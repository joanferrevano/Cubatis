using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cubatis.EditorTools
{
    /// <summary>
    /// Genera el prefab BurbujasAmbiente (ParticleSystem de Shuriken) y lo
    /// coloca en las escenas. El prefab se construye por codigo en vez de a
    /// mano para que todos los valores del efecto queden versionados y
    /// comentados aqui (un ParticleSystem serializado son miles de lineas de
    /// YAML ilegible).
    ///
    /// Las burbujas necesitan el fondo como sprite de mundo (no Image de un
    /// Canvas Overlay, que se dibuja encima de todo lo del mundo). Orden:
    /// fondo (-1000) -> burbujas (-500) -> tablero (0+) -> UI Overlay.
    /// "Aplicar a escenas" es idempotente.
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
                if (InstanciarBurbujas(prefab))
                {
                    EditorSceneManager.SaveScene(escena);
                    Debug.Log($"[Burbujas] {ruta}: burbujas anadidas.");
                }
                else Debug.Log($"[Burbujas] {ruta}: ya tenia burbujas, sin cambios.");
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
