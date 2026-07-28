// Menu: TimeKiller/Setup/38 - Build Objective HUD (carved stone & brass).
//
// Imports the UI art with the right settings and builds the uGUI HUD that
// ObjectiveHUD drives. Safe to re-run: every object is found-or-created, never
// destroyed and rebuilt, so hand-tuning on the rig survives.
//
// The art set and the one number the code needs (the gauge's inner channel) are
// documented in Tools/UIArt/README.md. Point filtering and NO compression are
// not preferences here — these are pixel-art sprites, and either bilinear
// filtering or DXT compression visibly destroys them.
using TimeKiller.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Objectives.EditorTools
{
    public static class ObjectiveHudSetup
    {
        const string UiFolder = "Assets/Resources/Assets/UI";

        // Gauge.png is 614x142. Its channel, measured off the art, is
        // x 57..555 and y 46..93 in PNG pixels (top-left origin). Unity UI
        // anchors are bottom-left origin, hence the flip on Y.
        const float GaugeW = 614f, GaugeH = 142f;
        const float ChanL = 57f, ChanR = 555f, ChanTop = 46f, ChanBottom = 93f;

        [MenuItem("TimeKiller/Setup/38 - Build Objective HUD (carved stone & brass)")]
        public static void Build()
        {
            var hud = Object.FindAnyObjectByType<ObjectiveHUD>();
            if (hud == null)
            {
                Debug.LogError("[TimeKiller Setup] No ObjectiveHUD in the scene — run Setup/28 first. Nothing was created.");
                return;
            }

            var plaque = ImportSprite($"{UiFolder}/CounterPlaque.png");
            var gaugeSprite = ImportSprite($"{UiFolder}/Gauge.png");
            var needleSprite = ImportSprite($"{UiFolder}/GaugeNeedle.png");
            ImportSprite($"{UiFolder}/KeyCap_E.png");
            // Unity's spriteBorder is (left, BOTTOM, right, TOP). The README quotes
            // these as L/R/T/B, so they must be reordered on the way in — getting
            // this wrong stretches the wrong edge and distorts the brass.
            ImportSprite($"{UiFolder}/Bar.png", new Vector4(22, 4, 22, 4));        // 22px brass end caps
            ImportSprite($"{UiFolder}/EndFrame.png", new Vector4(60, 48, 60, 58)); // L60 R60 T58 B48
            var display = ImportFont($"{UiFolder}/TimeKiller_Display.ttf");
            var body = ImportFont($"{UiFolder}/TimeKiller_Body.ttf");

            if (plaque == null || gaugeSprite == null || needleSprite == null)
            {
                Debug.LogError($"[TimeKiller Setup] HUD art missing from {UiFolder} — copy it from Tools/UIArt/. Nothing was built.");
                return;
            }

            // --- canvas ---
            var canvasGo = FindOrCreate("ObjectiveHudCanvas", null);
            var canvas = Ensure<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the blood layers (500) and the wardrobe slats (600) so the
            // objective stays readable from inside a wardrobe, below the end
            // screen (900) which must cover everything.
            canvas.sortingOrder = 650;
            var scaler = Ensure<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // match height: the HUD hugs top and bottom
            Ensure<GraphicRaycaster>(canvasGo).enabled = false;   // HUD is never clicked

            // --- counter plaque, top centre ---
            var counterGo = FindOrCreate("CounterPlaque", canvasGo.transform);
            var counterImg = Ensure<Image>(counterGo);
            counterImg.sprite = plaque;
            counterImg.raycastTarget = false;
            var counterRt = counterGo.GetComponent<RectTransform>();
            Anchor(counterRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            counterRt.sizeDelta = new Vector2(431f, 130f) * 0.8f;
            counterRt.anchoredPosition = new Vector2(0f, -18f);

            var counterTextGo = FindOrCreate("CounterText", counterGo.transform);
            var counterText = Ensure<Text>(counterTextGo);
            counterText.alignment = TextAnchor.MiddleCenter;
            counterText.raycastTarget = false;
            if (display != null) counterText.font = display;
            counterText.fontSize = 64;                     // native grid multiple (32/64/96)
            counterText.horizontalOverflow = HorizontalWrapMode.Overflow;
            counterText.verticalOverflow = VerticalWrapMode.Overflow;
            counterText.text = "0 / 3";
            var ctRt = counterTextGo.GetComponent<RectTransform>();
            Stretch(ctRt);
            ctRt.offsetMin = new Vector2(0f, 4f);          // sit inside the plaque face
            ctRt.offsetMax = new Vector2(0f, -6f);

            // --- gauge, bottom centre ---
            var gaugeGo = FindOrCreate("Gauge", canvasGo.transform);
            var gaugeImg = Ensure<Image>(gaugeGo);
            gaugeImg.sprite = gaugeSprite;
            gaugeImg.raycastTarget = false;
            var gaugeRt = gaugeGo.GetComponent<RectTransform>();
            Anchor(gaugeRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            gaugeRt.sizeDelta = new Vector2(GaugeW, GaugeH) * 0.9f;
            gaugeRt.anchoredPosition = new Vector2(0f, 96f);

            // Channel: an empty rect covering the frame's opening. Everything the
            // code moves lives inside it in 0..1 space, so none of the maths has
            // to know the art's pixel size.
            var channelGo = FindOrCreate("Channel", gaugeGo.transform);
            var channelRt = channelGo.GetComponent<RectTransform>();
            if (channelRt == null) channelRt = channelGo.AddComponent<RectTransform>();
            channelRt.anchorMin = new Vector2(ChanL / GaugeW, (GaugeH - ChanBottom) / GaugeH);
            channelRt.anchorMax = new Vector2(ChanR / GaugeW, (GaugeH - ChanTop) / GaugeH);
            channelRt.offsetMin = Vector2.zero;
            channelRt.offsetMax = Vector2.zero;

            // Children of the frame render ON TOP of it — which is required,
            // because the frame's channel is opaque and would hide anything
            // drawn underneath.
            // A real sprite, NOT null: Image.Type.Filled needs one, and with a null
            // sprite fillAmount is silently ignored and the quad draws full width —
            // which read on screen as a gauge permanently at 100%.
            var solid = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Flat quads in a hue the art does not contain read as programmer art
            // sitting inside pixel art. These gradients are built from colours
            // measured out of Gauge.png itself: slate #202030/#505070 and brass
            // #604030/#A07040. Vertical gradients, so a HORIZONTAL fill reveals
            // them without distorting the shading.
            var fillSprite = MakeGradientSprite($"{UiFolder}/GaugeFill.png",
                new Color32(0x38, 0x26, 0x1A, 0xE0), new Color32(0xB0, 0x7C, 0x40, 0xF0), 0.58f);
            var zoneSprite = MakeGradientSprite($"{UiFolder}/GaugeZone.png",
                new Color32(0xC8, 0xA0, 0x54, 0xE8), new Color32(0xFF, 0xF4, 0xD2, 0xFF), 0.5f);

            var fillGo = FindOrCreate("Progress", channelGo.transform);
            var fill = Ensure<Image>(fillGo);
            fill.sprite = fillSprite != null ? fillSprite : solid;
            fill.color = Color.white;          // the gradient carries the colour now
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.raycastTarget = false;
            Stretch(fillGo.GetComponent<RectTransform>());

            // The advancing edge: a thin hot line the code parks at the fill's
            // boundary. A Filled image cannot brighten its own leading edge —
            // the gradient inside it is fixed to the texture, not to the cut.
            var edgeGo = FindOrCreate("FillEdge", channelGo.transform);
            var edgeImg = Ensure<Image>(edgeGo);
            edgeImg.sprite = solid;
            edgeImg.color = new Color(1f, 0.90f, 0.66f, 0.95f);
            edgeImg.raycastTarget = false;
            var edgeRt = edgeGo.GetComponent<RectTransform>();
            edgeRt.anchorMin = new Vector2(0f, 0f);
            edgeRt.anchorMax = new Vector2(0f, 1f);
            edgeRt.pivot = new Vector2(0.5f, 0.5f);
            edgeRt.sizeDelta = new Vector2(3f, 0f);
            edgeRt.anchoredPosition = Vector2.zero;

            var zoneGo = FindOrCreate("Zone", channelGo.transform);
            var zoneImg = Ensure<Image>(zoneGo);
            zoneImg.sprite = zoneSprite != null ? zoneSprite : solid;
            zoneImg.color = Color.white;
            zoneImg.raycastTarget = false;
            var zoneRt = zoneGo.GetComponent<RectTransform>();
            zoneRt.anchorMin = new Vector2(0.4f, 0f);
            zoneRt.anchorMax = new Vector2(0.6f, 1f);
            zoneRt.offsetMin = Vector2.zero;
            zoneRt.offsetMax = Vector2.zero;

            var needleGo = FindOrCreate("Needle", channelGo.transform);
            var needleImg = Ensure<Image>(needleGo);
            needleImg.sprite = needleSprite;
            needleImg.raycastTarget = false;
            var needleRt = needleGo.GetComponent<RectTransform>();
            needleRt.anchorMin = needleRt.anchorMax = new Vector2(0.5f, 0.5f);
            needleRt.pivot = new Vector2(0.5f, 0.5f);
            needleRt.sizeDelta = new Vector2(24f, 62f) * 0.9f;
            needleRt.anchoredPosition = Vector2.zero;

            var hintGo = FindOrCreate("Hint", gaugeGo.transform);
            var hint = Ensure<Text>(hintGo);
            hint.alignment = TextAnchor.UpperCenter;
            hint.raycastTarget = false;
            if (body != null) hint.font = body;
            hint.fontSize = 32;                            // native grid multiple (16/32/48)
            hint.color = new Color(0.82f, 0.80f, 0.74f);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.verticalOverflow = VerticalWrapMode.Overflow;
            hint.text = "SPACE ON THE MARK";   // no longer green — see the fill colours above
            var hintRt = hintGo.GetComponent<RectTransform>();
            Anchor(hintRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
            hintRt.sizeDelta = new Vector2(560f, 40f);
            hintRt.anchoredPosition = new Vector2(0f, -10f);

            // Draw order is sibling order in uGUI, and FindOrCreate appends — so a
            // re-run that adds a new element would silently put it on top of the
            // needle. Pin it: fill, its hot edge, the target, then the needle last
            // because the needle is the thing the player is actually tracking.
            fillGo.transform.SetSiblingIndex(0);
            edgeGo.transform.SetSiblingIndex(1);
            zoneGo.transform.SetSiblingIndex(2);
            needleGo.transform.SetSiblingIndex(3);

            // --- wire ---
            var so = new SerializedObject(hud);
            so.FindProperty("counterRoot").objectReferenceValue = counterGo;
            so.FindProperty("counterText").objectReferenceValue = counterText;
            so.FindProperty("gaugeRoot").objectReferenceValue = gaugeGo;
            so.FindProperty("progressFill").objectReferenceValue = fill;
            so.FindProperty("fillEdge").objectReferenceValue = edgeRt;
            so.FindProperty("zone").objectReferenceValue = zoneRt;
            so.FindProperty("needle").objectReferenceValue = needleRt;
            so.ApplyModifiedPropertiesWithoutUndo();

            gaugeGo.SetActive(false);   // only while repairing
            EditorUtility.SetDirty(hud);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            Debug.Log("[TimeKiller Setup] Objective HUD built. Counter plaque top-centre, skill-check gauge "
                      + "bottom-centre (hidden until you repair). Fonts: "
                      + $"{(display != null ? "Display" : "MISSING Display")} / {(body != null ? "Body" : "MISSING Body")}.");
        }

        /// A 4x48 vertical gradient written to disk as a real asset, so the fill
        /// is inspectable and swappable like every other piece of art rather than
        /// being conjured at runtime. `peak` is where the bright core sits
        /// (0 = bottom, 1 = top) — slightly above centre reads as a lit metal
        /// surface rather than a symmetrical tube.
        static Sprite MakeGradientSprite(string path, Color32 edge, Color32 core, float peak)
        {
            const int w = 4, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                // Distance from the bright band, normalised so both sides reach
                // the edge colour at the channel's rim.
                float d = Mathf.Abs(t - peak) / Mathf.Max(0.001f, Mathf.Max(peak, 1f - peak));
                float k = 1f - Mathf.Clamp01(d);
                k = k * k * (3f - 2f * k);              // smoothstep: no banding
                var c = Color32.Lerp(edge, core, k);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return ImportSprite(path);
        }

        /// Pixel art: point filter, no compression, no mip maps. Bilinear or DXT
        /// visibly destroys these — this is correctness, not taste.
        static Sprite ImportSprite(string path, Vector4? border = null)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            if (border.HasValue) importer.spriteBorder = border.Value;   // 9-slice
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// Pixel fonts blur unless rendered as hinted raster at integer sizes
        /// that are multiples of their native grid (see Tools/UIArt/README.md).
        static Font ImportFont(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (importer == null) return null;
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        static GameObject FindOrCreate(string name, Transform parent)
        {
            if (parent != null)
            {
                var existing = parent.Find(name);
                if (existing != null) return existing.gameObject;
            }
            else
            {
                var found = GameObject.Find(name);
                if (found != null) return found;
            }
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Objective HUD");
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
        }
    }
}
