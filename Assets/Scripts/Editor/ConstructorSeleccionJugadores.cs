using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cubatis.EditorTools
{
    /// <summary>
    /// Genera la escena SeleccionJugadores con toda la UI como GameObjects
    /// reales (editables a mano) + los prefabs SlotJugador y BotonAvatar +
    /// los sprites placeholder de avatar, y deja TODAS las referencias del
    /// componente SeleccionJugadores asignadas.
    ///
    /// Menu: Cubatis > Construir escena Seleccion de Jugadores
    /// </summary>
    public static class ConstructorSeleccionJugadores
    {
        private const string RutaEscena = "Assets/Scenes/SeleccionJugadores.unity";
        private const string CarpetaPrefabs = "Assets/Prefabs";
        private const string CarpetaAvatares = "Assets/Boards/Avatares";
        private const string RutaSlot = CarpetaPrefabs + "/SlotJugador.prefab";
        private const string RutaBotonAvatar = CarpetaPrefabs + "/BotonAvatar.prefab";
        private const string RutaMascara = CarpetaAvatares + "/avatar_mascara.png";
        private const string RutaAnillo = CarpetaAvatares + "/avatar_seleccion.png";
        private const int NumAvatares = 12;
        private static readonly Color Morado = new Color(0.29f, 0.14f, 0.36f);

        [MenuItem("Cubatis/Construir escena Seleccion de Jugadores")]
        public static void Construir()
        {
            if (!EditorUtility.DisplayDialog("Construir escena de seleccion",
                "Se regeneran la escena SeleccionJugadores, los prefabs SlotJugador / BotonAvatar y los " +
                "sprites placeholder. Perderas los ajustes manuales sobre esos objetos. ¿Continuar?",
                "Construir", "Cancelar"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(CarpetaPrefabs);
            Directory.CreateDirectory(CarpetaAvatares);
            AssetDatabase.Refresh();

            // 1. Sprites placeholder
            GuardarSpriteRuta("avatar_mascara", PintarCirculo(Color.white, false));
            GuardarSpriteRuta("avatar_seleccion", PintarCirculo(new Color(1f, 0.85f, 0.2f), true));
            var avataresRutas = new string[NumAvatares];
            for (int i = 0; i < NumAvatares; i++)
                avataresRutas[i] = GuardarSpriteRuta($"avatar_{i:00}",
                    PintarCirculo(Color.HSVToRGB(i / (float)NumAvatares, 0.55f, 0.95f), false));
            AssetDatabase.Refresh();

            // 2. Escena vacia
            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Prefabs (cargan sus sprites frescos desde disco)
            CrearPrefabSlot();
            CrearPrefabBotonAvatar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 4. Cargar prefabs y sprites FRESCOS desde disco (evita refs obsoletas)
            var slotGO = AssetDatabase.LoadAssetAtPath<GameObject>(RutaSlot);
            var botonAvGO = AssetDatabase.LoadAssetAtPath<GameObject>(RutaBotonAvatar);
            if (slotGO == null || botonAvGO == null)
            {
                Debug.LogError("[Constructor] No se pudieron cargar los prefabs recien creados. Abortado.");
                return;
            }
            var slotPrefab = slotGO.GetComponent<SlotJugador>();
            var avatarPrefab = botonAvGO.GetComponent<BotonAvatar>();
            var avatares = new Sprite[NumAvatares];
            for (int i = 0; i < NumAvatares; i++)
                avatares[i] = AssetDatabase.LoadAssetAtPath<Sprite>(avataresRutas[i]);

            // 5. UI + wiring
            var sj = ConstruirContenido(slotPrefab, avatarPrefab, avatares);

            // 6. Verificacion
            bool ok = VerificarReferencias(sj);

            // 7. Guardar
            EditorUtility.SetDirty(sj);
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena, RutaEscena);
            AssetDatabase.SaveAssets();
            Selection.activeObject = sj.gameObject;

            Debug.Log(ok
                ? "[Constructor] Escena + prefabs + avatares generados. TODAS las referencias asignadas. Listo para Play."
                : "[Constructor] Generado, pero HAY referencias sin asignar (ver errores arriba).");
        }

        /// <summary>
        /// Solo re-asigna prefabSlot / prefabBotonAvatar en el SeleccionJugadores
        /// de la escena abierta (por si quedaron en None sin querer regenerar todo).
        /// </summary>
        [MenuItem("Cubatis/Reasignar prefabs a SeleccionJugadores")]
        public static void ReasignarPrefabs()
        {
            var sj = Object.FindFirstObjectByType<SeleccionJugadores>();
            if (sj == null) { Debug.LogError("[Constructor] No hay SeleccionJugadores en la escena abierta."); return; }

            var slot = AssetDatabase.LoadAssetAtPath<GameObject>(RutaSlot);
            var botonAv = AssetDatabase.LoadAssetAtPath<GameObject>(RutaBotonAvatar);
            if (slot == null || botonAv == null)
            { Debug.LogError("[Constructor] Faltan los prefabs en " + CarpetaPrefabs + ". Ejecuta 'Construir escena'."); return; }

            var so = new SerializedObject(sj);
            so.FindProperty("prefabSlot").objectReferenceValue = slot.GetComponent<SlotJugador>();
            so.FindProperty("prefabBotonAvatar").objectReferenceValue = botonAv.GetComponent<BotonAvatar>();
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(sj);
            EditorSceneManager.MarkSceneDirty(sj.gameObject.scene);
            VerificarReferencias(sj);
        }

        /// <summary>
        /// Migracion QUIRURGICA sobre la escena ABIERTA: convierte el ContenedorSlots
        /// actual en un Scroll View vertical (ScrollRect + RectMask2D + Content con
        /// Grid + ContentSizeFitter) SIN tocar nada mas. Conserva todos los sprites,
        /// colores y ajustes manuales del resto de la UI.
        ///
        /// Menu: Cubatis > Migrar ContenedorSlots a Scroll View
        /// </summary>
        [MenuItem("Cubatis/Migrar ContenedorSlots a Scroll View")]
        public static void MigrarContenedorAScrollView()
        {
            var sj = Object.FindFirstObjectByType<SeleccionJugadores>();
            if (sj == null) { Debug.LogError("[Migrar] No hay SeleccionJugadores en la escena abierta."); return; }

            var so = new SerializedObject(sj);
            var contProp = so.FindProperty("contenedorSlots");
            var scrollProp = so.FindProperty("scrollSlots");

            if (scrollProp != null && scrollProp.objectReferenceValue != null)
            { Debug.Log("[Migrar] scrollSlots ya esta asignado: la escena ya parece migrada. Nada que hacer."); return; }

            var actual = contProp != null ? contProp.objectReferenceValue as Transform : null;
            if (actual == null) { Debug.LogError("[Migrar] 'contenedorSlots' esta sin asignar. Abortado."); return; }

            var viewport = (RectTransform)actual;                 // pasara a ser el Scroll View
            var gridViejo = viewport.GetComponent<GridLayoutGroup>();
            if (gridViejo == null) { Debug.LogError("[Migrar] '" + viewport.name + "' no tiene GridLayoutGroup. ¿Ya migrado a mano? Abortado."); return; }

            // 1. Content nuevo, hijo del viewport, estirado en horizontal y anclado arriba.
            var contGO = new GameObject("Contenido", typeof(RectTransform));
            var contRT = (RectTransform)contGO.transform;
            contRT.SetParent(viewport, false);
            contRT.anchorMin = new Vector2(0f, 1f);
            contRT.anchorMax = new Vector2(1f, 1f);
            contRT.pivot = new Vector2(0.5f, 1f);
            contRT.sizeDelta = Vector2.zero;
            contRT.anchoredPosition = Vector2.zero;

            // 2. Mover los hijos actuales del viewport (BotonMas, etc.) dentro del Content.
            var hijos = new List<Transform>();
            foreach (Transform h in viewport) if (h != contRT) hijos.Add(h);
            foreach (var h in hijos) h.SetParent(contRT, false);

            // 3. Copiar el GridLayoutGroup tal cual al Content y borrar el viejo.
            UnityEditorInternal.ComponentUtility.CopyComponent(gridViejo);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(contGO);
            Object.DestroyImmediate(gridViejo);

            // 4. ContentSizeFitter: la altura del Content crece con el numero de filas.
            var fitter = contGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 5. Viewport: mascara + (Image transparente si no habia ninguna) + ScrollRect.
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            if (viewport.GetComponent<Graphic>() == null)
            {
                var img = viewport.gameObject.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0f);   // invisible, solo para capturar el arrastre
            }
            var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = contRT;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            // 6. Rewire de las referencias del componente.
            contProp.objectReferenceValue = contRT;
            if (scrollProp != null) scrollProp.objectReferenceValue = scroll;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(sj);
            EditorSceneManager.MarkSceneDirty(sj.gameObject.scene);
            Debug.Log("[Migrar] ContenedorSlots convertido en Scroll View. Revisa en Play y guarda la escena (Ctrl+S).");
        }

        // ===================== ESCENA =====================
        private static SeleccionJugadores ConstruirContenido(SlotJugador slotPrefab, BotonAvatar avatarPrefab, Sprite[] avatares)
        {
            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0, 0, -10);
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Morado;
            cam.orthographic = true;
            camGO.AddComponent<UniversalAdditionalCameraData>();

            var esGO = new GameObject("EventSystem", typeof(EventSystem));
            esGO.AddComponent<InputSystemUIInputModule>();

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var raiz = (RectTransform)canvasGO.transform;

            var fondo = NuevaImagen("Fondo", raiz, Morado, null);
            Estirar(fondo.rectTransform);

            var titulo = NuevoTexto("Titulo", raiz, "¿QUIEN BEBE HOY?", 74, FontStyles.Bold, TextAlignmentOptions.Center);
            Anclar(titulo.rectTransform, new Vector2(0.5f, 1f), new Vector2(960, 150), new Vector2(0, -130));

            // ContenedorSlots = Scroll View vertical (ScrollRect + mascara) con un
            // Content que crece en altura (ContentSizeFitter) y lleva el Grid 3 columnas.
            var scrollGO = NuevoUI("ContenedorSlots", raiz);
            var scrollRT = (RectTransform)scrollGO.transform;
            Anclar(scrollRT, new Vector2(0.5f, 0.5f), new Vector2(960, 1040), new Vector2(0, 30));
            var scrollImg = scrollGO.AddComponent<Image>();
            scrollImg.color = new Color(1, 1, 1, 0.03f);
            scrollGO.AddComponent<RectMask2D>();
            var scroll = scrollGO.AddComponent<ScrollRect>();

            var contGO = NuevoUI("Contenido", scrollGO.transform);
            var contRT = (RectTransform)contGO.transform;
            contRT.anchorMin = new Vector2(0f, 1f);
            contRT.anchorMax = new Vector2(1f, 1f);
            contRT.pivot = new Vector2(0.5f, 1f);
            contRT.sizeDelta = Vector2.zero;
            contRT.anchoredPosition = Vector2.zero;
            var glg = contGO.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(300, 320);
            glg.spacing = new Vector2(20, 22);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
            glg.childAlignment = TextAnchor.UpperCenter;
            glg.padding = new RectOffset(0, 0, 0, 12);
            var fitter = contGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contRT;
            scroll.viewport = scrollRT;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            var mas = CrearBoton("BotonMas", contGO.transform, "+", 130, new Color(1, 1, 1, 0.14f));

            var empezar = CrearBoton("BotonEmpezar", raiz, "EMPEZAR", 60, new Color(0.95f, 0.35f, 0.55f));
            Anclar((RectTransform)empezar.transform, new Vector2(0.5f, 0f), new Vector2(720, 160), new Vector2(0, 140));

            // ---- Popup ----
            var popup = NuevoUI("Popup", raiz);
            Estirar((RectTransform)popup.transform);

            var oscurecer = NuevaImagen("Oscurecer", popup.transform, new Color(0, 0, 0, 0.72f), null);
            Estirar(oscurecer.rectTransform);

            var panel = NuevaImagen("Panel", popup.transform, new Color(0.16f, 0.08f, 0.22f, 1f), null);
            Anclar(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(940, 1180), Vector2.zero);

            var tp = NuevoTexto("Titulo", panel.transform, "NUEVO JUGADOR", 52, FontStyles.Bold, TextAlignmentOptions.Center);
            Anclar(tp.rectTransform, new Vector2(0.5f, 1f), new Vector2(800, 80), new Vector2(0, -36));

            var cerrar = CrearBoton("BotonCerrar", panel.transform, "X", 40, new Color(0.9f, 0.3f, 0.3f));
            Anclar((RectTransform)cerrar.transform, new Vector2(1f, 1f), new Vector2(72, 72), new Vector2(-20, -20));

            var contAv = NuevoUI("ContenedorAvatares", panel.transform);
            Anclar((RectTransform)contAv.transform, new Vector2(0.5f, 1f), new Vector2(760, 600), new Vector2(0, -140));
            var g2 = contAv.AddComponent<GridLayoutGroup>();
            g2.cellSize = new Vector2(150, 150);
            g2.spacing = new Vector2(20, 20);
            g2.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g2.constraintCount = 4;
            g2.childAlignment = TextAnchor.UpperCenter;

            var campo = CrearCampoNombre(panel.transform);
            Anclar((RectTransform)campo.transform, new Vector2(0.5f, 0f), new Vector2(760, 104), new Vector2(0, 280));

            var aceptar = CrearBoton("BotonAceptar", panel.transform, "ACEPTAR", 50, new Color(0.35f, 0.8f, 0.45f));
            Anclar((RectTransform)aceptar.transform, new Vector2(0.5f, 0f), new Vector2(560, 130), new Vector2(0, 110));

            popup.SetActive(false);

            // ---- Logica + referencias ----
            var sj = canvasGO.AddComponent<SeleccionJugadores>();
            var so = new SerializedObject(sj);
            Asignar(so, "contenedorSlots", contRT);
            Asignar(so, "scrollSlots", scroll);
            Asignar(so, "prefabSlot", slotPrefab);
            Asignar(so, "botonMas", mas.gameObject);
            Asignar(so, "botonAbrirPopup", mas);
            Asignar(so, "botonEmpezar", empezar);
            Asignar(so, "popup", popup);
            Asignar(so, "contenedorAvatares", contAv.transform);
            Asignar(so, "prefabBotonAvatar", avatarPrefab);
            Asignar(so, "campoNombre", campo);
            Asignar(so, "botonAceptar", aceptar);
            Asignar(so, "botonCerrarPopup", cerrar);
            var pav = so.FindProperty("avatares");
            pav.arraySize = avatares.Length;
            for (int i = 0; i < avatares.Length; i++)
                pav.GetArrayElementAtIndex(i).objectReferenceValue = avatares[i];
            so.FindProperty("escenaTablero").stringValue = "Tablero";
            so.FindProperty("empezarConListaVacia").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return sj;
        }

        private static void Asignar(SerializedObject so, string campo, Object valor)
        {
            var p = so.FindProperty(campo);
            if (p == null) { Debug.LogError($"[Constructor] Campo '{campo}' no existe en SeleccionJugadores."); return; }
            if (valor == null) { Debug.LogError($"[Constructor] Valor null para '{campo}'."); return; }
            p.objectReferenceValue = valor;
        }

        private static bool VerificarReferencias(SeleccionJugadores sj)
        {
            var so = new SerializedObject(sj);
            string[] refs =
            {
                "contenedorSlots", "scrollSlots", "prefabSlot", "botonMas", "botonEmpezar", "popup",
                "contenedorAvatares", "prefabBotonAvatar", "campoNombre", "botonAceptar",
                "botonCerrarPopup", "botonAbrirPopup"
            };
            var faltan = new List<string>();
            foreach (var r in refs)
                if (so.FindProperty(r).objectReferenceValue == null) faltan.Add(r);

            var pav = so.FindProperty("avatares");
            if (pav.arraySize == 0) faltan.Add("avatares (vacio)");
            for (int i = 0; i < pav.arraySize; i++)
                if (pav.GetArrayElementAtIndex(i).objectReferenceValue == null) faltan.Add($"avatares[{i}]");

            if (faltan.Count == 0)
            {
                Debug.Log("[Constructor] Verificacion OK: ningun campo en None.");
                return true;
            }
            Debug.LogError("[Constructor] Referencias sin asignar: " + string.Join(", ", faltan));
            return false;
        }

        // ===================== PREFABS =====================
        private static void CrearPrefabSlot()
        {
            var circulo = AssetDatabase.LoadAssetAtPath<Sprite>(RutaMascara);
            var root = NuevoUI("SlotJugador", null);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.08f);
            bg.raycastTarget = false;
            ((RectTransform)root.transform).sizeDelta = new Vector2(300, 320);

            var av = NuevoUI("Avatar", root.transform);
            var avImg = av.AddComponent<Image>();
            avImg.sprite = circulo;
            avImg.preserveAspect = true;
            av.AddComponent<Mask>().showMaskGraphic = false;
            Anclar((RectTransform)av.transform, new Vector2(0.5f, 1f), new Vector2(200, 200), new Vector2(0, -16));

            var img = NuevaImagen("Img", av.transform, Color.white, null);
            img.raycastTarget = false;
            img.preserveAspect = true;
            Estirar(img.rectTransform);
            var arf = img.gameObject.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = 1f;

            var nom = NuevoTexto("Nombre", root.transform, "Jugador", 32, FontStyles.Bold, TextAlignmentOptions.Center);
            nom.overflowMode = TextOverflowModes.Ellipsis;
            Anclar(nom.rectTransform, new Vector2(0.5f, 1f), new Vector2(280, 54), new Vector2(0, -224));

            var x = CrearBoton("BotonQuitar", root.transform, "X", 30, new Color(0.9f, 0.3f, 0.3f));
            Anclar((RectTransform)x.transform, new Vector2(1f, 1f), new Vector2(56, 56), new Vector2(-4, -4));

            var comp = root.AddComponent<SlotJugador>();
            var so = new SerializedObject(comp);
            so.FindProperty("avatar").objectReferenceValue = img;
            so.FindProperty("nombre").objectReferenceValue = nom;
            so.FindProperty("botonQuitar").objectReferenceValue = x;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RutaSlot);
            Object.DestroyImmediate(root);
        }

        private static void CrearPrefabBotonAvatar()
        {
            var circulo = AssetDatabase.LoadAssetAtPath<Sprite>(RutaMascara);
            var anillo = AssetDatabase.LoadAssetAtPath<Sprite>(RutaAnillo);
            var root = NuevoUI("BotonAvatar", null);
            var rootImg = root.AddComponent<Image>();
            rootImg.sprite = circulo;
            rootImg.preserveAspect = true;
            root.AddComponent<Mask>().showMaskGraphic = false;
            var boton = root.AddComponent<Button>();
            boton.targetGraphic = rootImg;
            ((RectTransform)root.transform).sizeDelta = new Vector2(150, 150);

            var img = NuevaImagen("Img", root.transform, Color.white, null);
            img.raycastTarget = false;
            img.preserveAspect = true;
            Estirar(img.rectTransform);
            var arf = img.gameObject.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = 1f;

            var sel = NuevaImagen("Seleccion", root.transform, Color.white, anillo);
            sel.raycastTarget = false;
            sel.maskable = false;
            Estirar(sel.rectTransform, -6);
            sel.gameObject.SetActive(false);

            var comp = root.AddComponent<BotonAvatar>();
            var so = new SerializedObject(comp);
            so.FindProperty("imagen").objectReferenceValue = img;
            so.FindProperty("marcaSeleccion").objectReferenceValue = sel.gameObject;
            so.FindProperty("boton").objectReferenceValue = boton;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RutaBotonAvatar);
            Object.DestroyImmediate(root);
        }

        // ===================== SPRITES PLACEHOLDER =====================
        private static Texture2D PintarCirculo(Color col, bool anillo)
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = s / 2f;
            var c = new Vector2(r, r);
            float interior = r - 12f;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = anillo
                        ? Mathf.Clamp01(r - 1f - d) * Mathf.Clamp01(d - interior)
                        : Mathf.Clamp01(r - d);
                    px[y * s + x] = new Color(col.r, col.g, col.b, col.a * a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static string GuardarSpriteRuta(string nombre, Texture2D tex)
        {
            string ruta = $"{CarpetaAvatares}/{nombre}.png";
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);

            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return ruta;
        }

        // ===================== HELPERS UI =====================
        private static GameObject NuevoUI(string nombre, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            if (padre != null) go.transform.SetParent(padre, false);
            return go;
        }

        private static Image NuevaImagen(string nombre, Transform padre, Color color, Sprite sprite)
        {
            var img = NuevoUI(nombre, padre).AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            return img;
        }

        private static TMP_Text NuevoTexto(string nombre, Transform padre, string texto, float tamano,
            FontStyles estilo, TextAlignmentOptions alineacion)
        {
            var t = NuevoUI(nombre, padre).AddComponent<TextMeshProUGUI>();
            t.text = texto;
            t.fontSize = tamano;
            t.fontStyle = estilo;
            t.alignment = alineacion;
            t.color = Color.white;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static Button CrearBoton(string nombre, Transform padre, string etiqueta, float tamano, Color color)
        {
            var img = NuevaImagen(nombre, padre, color, null);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.disabledColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.5f);
            b.colors = cb;

            var t = NuevoTexto("Texto", img.transform, etiqueta, tamano, FontStyles.Bold, TextAlignmentOptions.Center);
            t.raycastTarget = false;
            Estirar(t.rectTransform);
            return b;
        }

        private static TMP_InputField CrearCampoNombre(Transform padre)
        {
            var img = NuevaImagen("CampoNombre", padre, Color.white, null);
            var input = img.gameObject.AddComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 15;
            input.targetGraphic = img;

            var area = NuevoUI("Area", img.transform);
            Estirar((RectTransform)area.transform, 14);
            area.AddComponent<RectMask2D>();

            var ph = NuevoTexto("Placeholder", area.transform, "Nombre...", 34, FontStyles.Italic, TextAlignmentOptions.Left);
            ph.color = new Color(0.5f, 0.5f, 0.5f);
            Estirar(ph.rectTransform);

            var txt = NuevoTexto("Texto", area.transform, "", 34, FontStyles.Normal, TextAlignmentOptions.Left);
            txt.color = Color.black;
            Estirar(txt.rectTransform);

            input.textViewport = (RectTransform)area.transform;
            input.textComponent = txt;
            input.placeholder = ph;
            return input;
        }

        private static void Estirar(RectTransform rt, float margen = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margen, margen);
            rt.offsetMax = new Vector2(-margen, -margen);
        }

        private static void Anclar(RectTransform rt, Vector2 ancla, Vector2 tamano, Vector2 posicion)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = ancla;
            rt.sizeDelta = tamano;
            rt.anchoredPosition = posicion;
        }
    }
}
