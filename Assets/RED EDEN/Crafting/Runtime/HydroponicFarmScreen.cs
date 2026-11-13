using UnityEngine;
using UnityEngine.UI;

namespace RedEden.Crafting
{
    /// <summary>
    /// Digital screen on the outside of the Hydroponic Farm.
    /// Builds a world-space UI at runtime (no extra assets) on top of the screen mount and shows
    /// the active crop, growth progress, oxygen production, stored output and key hints.
    /// Place this file next to HydroponicFarm.cs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HydroponicFarmScreen : MonoBehaviour
    {
        [SerializeField] private HydroponicFarm farm;
        [Tooltip("Transform placed on the screen surface. Its forward axis points INTO the machine.")]
        [SerializeField] private Transform screenMount;
        [Tooltip("Physical size of the screen in meters (width, height).")]
        [SerializeField] private Vector2 screenSizeMeters = new Vector2(0.9f, 1.8f);
        [SerializeField, Min(200)] private int canvasPixelWidth = 500;
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.02f, 0.07f, 0.08f, 1f);
        [SerializeField] private Color headerColor = new Color(0f, 0.45f, 0.4f, 1f);
        [SerializeField] private Color textColor = new Color(0.8f, 1f, 0.95f, 1f);
        [SerializeField] private Color cropColor = new Color(0.4f, 0.95f, 0.4f, 1f);
        [SerializeField] private Color oxygenColor = new Color(0.3f, 0.8f, 1f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.75f, 0.2f, 1f);

        private Text statusText, cropTitle, cropLine, cropStored, oxygenTitle, oxygenLine, oxygenStored, messageText, hintText;
        private RectTransform cropFill, oxygenFill;
        private Font font;
        private float timer;
        private bool built;

        private void Start()
        {
            if (farm == null)
                farm = GetComponent<HydroponicFarm>();
            if (farm == null || screenMount == null)
            {
                Debug.LogWarning("Hydroponic farm screen needs a farm and a screen mount.", this);
                enabled = false;
                return;
            }
            Build();
        }

        private void Update()
        {
            if (!built)
                return;
            timer -= Time.deltaTime;
            if (timer > 0f)
                return;
            timer = refreshInterval;
            Refresh();
        }

        // ------------------------------------------------------------ build
        private void Build()
        {
            font = LoadFont();

            var canvasObject = new GameObject("Farm Screen Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(screenMount, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            int w = canvasPixelWidth;
            int h = Mathf.RoundToInt(w * (screenSizeMeters.y / Mathf.Max(0.01f, screenSizeMeters.x)));
            RectTransform rt = canvasObject.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * (screenSizeMeters.x / w);

            Transform root = canvasObject.transform;
            Panel("Background", root, 0f, 0f, 1f, 1f, backgroundColor);

            Panel("Header", root, 0f, 0.92f, 1f, 1f, headerColor);
            Label("Title", root, 0.05f, 0.92f, 0.95f, 1f, 34, TextAnchor.MiddleCenter, textColor).text = "HYDRO-FARM  M01";
            statusText = Label("Status", root, 0.05f, 0.86f, 0.95f, 0.92f, 26, TextAnchor.MiddleCenter, textColor);

            // crop section
            cropTitle = Label("CropTitle", root, 0.06f, 0.78f, 0.94f, 0.85f, 32, TextAnchor.MiddleLeft, cropColor);
            cropFill = Bar("CropBar", root, 0.06f, 0.735f, 0.94f, 0.77f, cropColor);
            cropLine = Label("CropLine", root, 0.06f, 0.68f, 0.94f, 0.73f, 24, TextAnchor.MiddleLeft, textColor);
            cropStored = Label("CropStored", root, 0.06f, 0.62f, 0.94f, 0.68f, 28, TextAnchor.MiddleLeft, textColor);

            Panel("Divider", root, 0.06f, 0.595f, 0.94f, 0.6f, headerColor);

            // oxygen section
            oxygenTitle = Label("OxygenTitle", root, 0.06f, 0.52f, 0.94f, 0.59f, 28, TextAnchor.MiddleLeft, oxygenColor);
            oxygenFill = Bar("OxygenBar", root, 0.06f, 0.475f, 0.94f, 0.51f, oxygenColor);
            oxygenLine = Label("OxygenLine", root, 0.06f, 0.42f, 0.94f, 0.47f, 24, TextAnchor.MiddleLeft, textColor);
            oxygenStored = Label("OxygenStored", root, 0.06f, 0.36f, 0.94f, 0.42f, 28, TextAnchor.MiddleLeft, textColor);

            Panel("Divider2", root, 0.06f, 0.335f, 0.94f, 0.34f, headerColor);

            messageText = Label("Message", root, 0.06f, 0.2f, 0.94f, 0.32f, 28, TextAnchor.MiddleCenter, warningColor);
            hintText = Label("Hint", root, 0.06f, 0.03f, 0.94f, 0.18f, 26, TextAnchor.MiddleCenter, textColor);

            built = true;
            Refresh();
        }

        private static Font LoadFont()
        {
            Font loadedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (loadedFont != null)
                return loadedFont;

            loadedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (loadedFont == null)
                Debug.LogError("Hydroponic farm screen could not load a built-in UI font.");
            return loadedFont;
        }

        private static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Image Panel(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            RectTransform rt = Rect(name, parent, x0, y0, x1, y1);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private RectTransform Bar(string name, Transform parent, float x0, float y0, float x1, float y1, Color fillColor)
        {
            Panel(name + " Back", parent, x0, y0, x1, y1, new Color(0.05f, 0.15f, 0.16f, 1f));
            RectTransform back = Rect(name + " Holder", parent, x0, y0, x1, y1);
            RectTransform fill = Rect(name + " Fill", back, 0f, 0.12f, 0f, 0.88f);
            Image img = fill.gameObject.AddComponent<Image>();
            img.color = fillColor;
            img.raycastTarget = false;
            return fill;
        }

        private Text Label(string name, Transform parent, float x0, float y0, float x1, float y1, int size, TextAnchor anchor, Color color)
        {
            RectTransform rt = Rect(name, parent, x0, y0, x1, y1);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 10;
            t.resizeTextMaxSize = size;
            return t;
        }

        // ------------------------------------------------------------ update
        private static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return s >= 60 ? (s / 60) + "m " + (s % 60).ToString("00") + "s" : s + "s";
        }

        private static void SetFill(RectTransform fill, float value)
        {
            Vector2 max = fill.anchorMax;
            max.x = Mathf.Clamp01(value);
            fill.anchorMax = max;
        }

        private void Refresh()
        {
            if (farm == null)
                return;

            statusText.text = farm.Operational ? "SYSTEM ONLINE" : "SYSTEM OFFLINE";
            statusText.color = farm.Operational ? cropColor : warningColor;

            // crop
            cropTitle.text = "CROP: " + farm.CropName;
            SetFill(cropFill, farm.CropProgress);
            if (!farm.CropConfigured)
            {
                cropLine.text = "NO ITEM ASSIGNED";
                cropLine.color = warningColor;
            }
            else if (farm.CropStorageFull)
            {
                cropLine.text = "STORAGE FULL - COLLECT";
                cropLine.color = warningColor;
            }
            else
            {
                cropLine.text = "GROWTH " + Mathf.RoundToInt(farm.CropProgress * 100f) + "%   " + FormatTime(farm.CropSecondsLeft) + " LEFT";
                cropLine.color = textColor;
            }
            cropStored.text = "READY: " + farm.StoredCrop + " / " + farm.MaxStoredCrop + "   (+" + farm.CropAmountPerCycle + "/cycle)";

            // oxygen
            oxygenTitle.text = "PURE OXYGEN CYLINDER";
            SetFill(oxygenFill, farm.OxygenProgress);
            if (!farm.OxygenConfigured)
            {
                oxygenLine.text = "NO ITEM ASSIGNED";
                oxygenLine.color = warningColor;
            }
            else if (farm.OxygenStorageFull)
            {
                oxygenLine.text = "STORAGE FULL - COLLECT";
                oxygenLine.color = warningColor;
            }
            else
            {
                oxygenLine.text = "PRODUCTION " + Mathf.RoundToInt(farm.OxygenProgress * 100f) + "%   " + FormatTime(farm.OxygenSecondsLeft) + " LEFT";
                oxygenLine.color = textColor;
            }
            oxygenStored.text = "READY: " + farm.StoredOxygen + " / " + farm.MaxStoredOxygen + "   (+" + farm.OxygenAmountPerCycle + "/cycle)";

            // message + hints
            messageText.text = farm.Message;
            if (farm.PlayerInRange)
            {
                string hint = "[E]  COLLECT ALL";
                if (farm.AllowPlayerCropSwitch)
                    hint += "\n[" + farm.CycleCropKey + "]  NEXT CROP";
                hintText.text = hint;
            }
            else
            {
                hintText.text = "COME CLOSER TO USE";
            }
        }

        private void OnValidate()
        {
            screenSizeMeters.x = Mathf.Max(0.01f, screenSizeMeters.x);
            screenSizeMeters.y = Mathf.Max(0.01f, screenSizeMeters.y);
            canvasPixelWidth = Mathf.Max(200, canvasPixelWidth);
            refreshInterval = Mathf.Max(0.02f, refreshInterval);
        }
    }
}
