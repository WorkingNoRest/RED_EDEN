using System.Collections.Generic;
using System.Linq;
using DevionGames.InventorySystem;
using UnityEngine;
using UnityEngine.UI;

namespace RedEden.Crafting
{
    internal sealed class RedEdenCraftingUI : MonoBehaviour
    {
        private RedEdenCraftingStation station;
        private GameObject panel;
        private Text statusText;
        private Text detailsText;
        private Text messageText;
        private Button craftButton;
        private Button collectButton;
        private Transform recipeList;
        private RedEdenCraftingRecipe selectedRecipe;
        private readonly Dictionary<RedEdenCraftingRecipe, Button> recipeButtons =
            new Dictionary<RedEdenCraftingRecipe, Button>();
        private float refreshAfter;

        public bool IsOpen => panel != null && panel.activeSelf;

        public static RedEdenCraftingUI Open(RedEdenCraftingStation target)
        {
            RedEdenCraftingUI ui = target.GetComponentInChildren<RedEdenCraftingUI>(true);
            if (ui == null)
            {
                GameObject host = new GameObject("RED EDEN Crafting UI");
                host.transform.SetParent(target.transform, false);
                ui = host.AddComponent<RedEdenCraftingUI>();
                ui.Build();
            }

            ui.station = target;
            ui.PopulateRecipes();
            ui.panel.SetActive(true);
            ui.Refresh(true);
            return ui;
        }

        public void Refresh(bool force = false)
        {
            if (station == null || statusText == null)
                return;
            bool refreshAvailability = force || Time.unscaledTime >= refreshAfter;
            if (refreshAvailability)
                refreshAfter = Time.unscaledTime + 0.2f;

            RedEdenStationState state = station.State;
            statusText.text = state == RedEdenStationState.Crafting
                ? "PRINTING  " + Mathf.RoundToInt(station.Progress * 100f) + "%  " +
                  Mathf.CeilToInt(station.RemainingSeconds) + "s"
                : state == RedEdenStationState.ReadyToCollect
                    ? "OUTPUT READY  /  PRESS E OR COLLECT"
                    : state == RedEdenStationState.Disabled ? "STATION OFFLINE" : "FABRICATOR ONLINE";

            if (!refreshAvailability)
                return;

            if (selectedRecipe != null)
                UpdateRecipeDetails();

            string craftReason = string.Empty;
            bool canCraft = selectedRecipe != null && station.CanCraft(selectedRecipe, out craftReason);
            craftButton.interactable = canCraft;
            collectButton.interactable = state == RedEdenStationState.ReadyToCollect;
            foreach (KeyValuePair<RedEdenCraftingRecipe, Button> entry in recipeButtons)
            {
                bool available = station.CanCraft(entry.Key, out _);
                entry.Value.interactable = available;
                entry.Value.image.color = available
                    ? new Color(0.13f, 0.3f, 0.34f, 1f)
                    : new Color(0.22f, 0.25f, 0.28f, 0.62f);
                Text label = entry.Value.GetComponentInChildren<Text>();
                if (label != null)
                    label.color = available
                        ? new Color(0.85f, 0.93f, 0.95f, 1f)
                        : new Color(0.58f, 0.63f, 0.65f, 0.62f);
            }

            if (!canCraft && selectedRecipe != null && state == RedEdenStationState.Idle)
                messageText.text = craftReason;
        }

        public void ShowMessage(string message)
        {
            if (messageText != null)
                messageText.text = message;
            Refresh(true);
        }

        public void Close()
        {
            if (panel != null)
                panel.SetActive(false);
            if (station != null)
                station.StopUse();
        }

        private void Build()
        {
            GameObject canvasObject = new GameObject("Crafting Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            panel = CreatePanel(canvasObject.transform, "FabricatorPanel", new Vector2(760f, 620f));
            CreateText(panel.transform, "Title", "RED EDEN  /  FABRICATOR", 25, new Vector2(0f, 270f), new Vector2(680f, 42f));
            statusText = CreateText(panel.transform, "Status", string.Empty, 18, new Vector2(0f, 225f), new Vector2(680f, 36f));
            detailsText = CreateText(panel.transform, "RecipeDetails", "Select an available blueprint.", 16, new Vector2(0f, 135f), new Vector2(660f, 180f));
            messageText = CreateText(panel.transform, "Notification", string.Empty, 15, new Vector2(0f, -178f), new Vector2(660f, 30f));

            GameObject recipeListObject = new GameObject("BlueprintList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            recipeListObject.transform.SetParent(panel.transform, false);
            recipeList = recipeListObject.transform;
            RectTransform listRect = recipeListObject.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.5f, 0.5f);
            listRect.anchorMax = new Vector2(0.5f, 0.5f);
            listRect.anchoredPosition = new Vector2(0f, -70f);
            listRect.sizeDelta = new Vector2(650f, 150f);
            VerticalLayoutGroup layout = recipeListObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            craftButton = CreateButton(panel.transform, "Print", "START PRINT", new Vector2(-220f, -255f), () =>
            {
                if (station != null && selectedRecipe != null)
                {
                    if (station.StartCrafting(selectedRecipe, out string reason))
                        ShowMessage("FABRICATION STARTED");
                    else
                        ShowMessage(reason);
                }
            });
            collectButton = CreateButton(panel.transform, "Collect", "COLLECT OUTPUT", new Vector2(0f, -255f), () =>
            {
                if (station != null && !station.TryCollect(out string reason))
                    ShowMessage(reason);
                else
                    ShowMessage("OUTPUT TRANSFERRED TO INVENTORY");
            });
            CreateButton(panel.transform, "Close", "CLOSE", new Vector2(220f, -255f), Close);
        }

        private void PopulateRecipes()
        {
            if (recipeList == null)
                return;
            for (int i = recipeList.childCount - 1; i >= 0; i--)
                Destroy(recipeList.GetChild(i).gameObject);
            recipeButtons.Clear();

            if (station == null || station.Database == null)
                return;

            foreach (RedEdenCraftingRecipe recipe in station.Database.GetRecipesForStation(station.StationType))
            {
                if (recipe == null)
                    continue;
                Button button = CreateButton(recipeList, recipe.name, recipe.DisplayName, Vector2.zero, () =>
                {
                    if (!station.CanCraft(recipe, out string reason))
                    {
                        ShowMessage(reason);
                        return;
                    }
                    selectedRecipe = recipe;
                    UpdateRecipeDetails();
                    messageText.text = string.Empty;
                    Refresh(true);
                });
                LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 38f;
                recipeButtons.Add(recipe, button);
            }
            Refresh(true);
        }

        private void UpdateRecipeDetails()
        {
            if (station == null || selectedRecipe == null)
            {
                detailsText.text = "Select a blueprint.";
                return;
            }
            if (!selectedRecipe.IsValid(out string validationError))
            {
                detailsText.text = selectedRecipe.DisplayName + "\n" + validationError;
                return;
            }

            List<string> lines = new List<string> { selectedRecipe.DisplayName, selectedRecipe.Description };
            ItemContainer inventory = RedEdenInventoryBridge.FindInventory(station.InventoryWindow);
            foreach (IGrouping<string, RedEdenRecipeIngredient> group in selectedRecipe.Ingredients
                .GroupBy(ingredient => ingredient.Resource.InventoryItem.Id))
            {
                RedEdenRecipeIngredient ingredient = group.First();
                int required = group.Sum(item => item.Amount);
                int available = 0;
                if (inventory != null)
                    inventory.HasItem(ingredient.Resource.InventoryItem, required, out available);
                lines.Add((available >= required ? "[OK] " : "[MISSING] ") +
                    ingredient.Resource.DisplayName + "  " + available + " / " + required);
            }
            lines.Add("Output: " + selectedRecipe.OutputItem.DisplayName + "  x" + selectedRecipe.OutputAmount);
            lines.Add("Print time: " + selectedRecipe.DurationSeconds.ToString("0.#") + "s");
            detailsText.text = string.Join("\n", lines);
        }

        private static GameObject CreatePanel(Transform parent, string objectName, Vector2 size)
        {
            GameObject result = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            result.transform.SetParent(parent, false);
            RectTransform rect = result.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            result.GetComponent<Image>().color = new Color(0.055f, 0.075f, 0.095f, 0.97f);
            return result;
        }

        private static Text CreateText(Transform parent, string objectName, string value, int size, Vector2 position, Vector2 dimensions)
        {
            GameObject result = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            result.transform.SetParent(parent, false);
            RectTransform rect = result.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            Text text = result.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = new Color(0.83f, 0.9f, 0.93f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            return text;
        }

        private static Button CreateButton(Transform parent, string objectName, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            GameObject result = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            result.transform.SetParent(parent, false);
            RectTransform rect = result.GetComponent<RectTransform>();
            if (parent.GetComponent<VerticalLayoutGroup>() == null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = new Vector2(190f, 42f);
            }
            result.GetComponent<Image>().color = new Color(0.12f, 0.24f, 0.29f, 1f);
            Button button = result.GetComponent<Button>();
            button.onClick.AddListener(action);
            Text text = CreateText(result.transform, "Label", label, 15, Vector2.zero, new Vector2(180f, 38f));
            text.raycastTarget = false;
            return button;
        }
    }
}
