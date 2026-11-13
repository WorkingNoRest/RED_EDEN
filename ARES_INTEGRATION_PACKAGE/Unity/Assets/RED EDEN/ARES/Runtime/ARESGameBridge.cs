using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DevionGames.InventorySystem;
using RedEden.Crafting;
using UnityEngine;

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESGameBridge : MonoBehaviour
    {
        [SerializeField] private ARESSystemConfiguration configuration;
        [SerializeField] private MonoBehaviour vitalsSource;

        public static string BuildRuntimeSnapshot()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("GAME STATE SNAPSHOT");
            AppendInventory(builder);
            AppendCrafting(builder);
            AppendVitals(builder);
            AppendMissionStatus(builder);
            return builder.ToString();
        }

        public string BuildSnapshotForPrompt()
        {
            return BuildRuntimeSnapshot();
        }

        public static string GetInventoryStatusSummary()
        {
            StringBuilder builder = new StringBuilder();
            AppendInventory(builder);
            return builder.ToString();
        }

        public static string GetCraftingStatusSummary()
        {
            StringBuilder builder = new StringBuilder();
            AppendCrafting(builder);
            return builder.ToString();
        }

        public static string GetPlayerStatusSummary()
        {
            StringBuilder builder = new StringBuilder();
            AppendVitals(builder);
            return builder.ToString();
        }

        public static string GetMissionStatusSummary()
        {
            StringBuilder builder = new StringBuilder();
            AppendMissionStatus(builder);
            return builder.ToString();
        }

        private static void AppendInventory(StringBuilder builder)
        {
            builder.AppendLine("INVENTORY");
            try
            {
                ItemContainer[] containers = Resources.FindObjectsOfTypeAll<ItemContainer>();
                if (containers == null || containers.Length == 0)
                {
                    builder.AppendLine("- Inventory manager unavailable; no item containers were detected.");
                    return;
                }

                Dictionary<string, int> totalStacks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (ItemContainer container in containers)
                {
                    if (container == null)
                        continue;

                    Item[] items = container.GetItems<Item>(true);
                    foreach (Item item in items)
                    {
                        if (item == null)
                            continue;
                        string key = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
                        totalStacks[key] = totalStacks.TryGetValue(key, out int amount) ? amount + item.Stack : item.Stack;
                    }
                }

                if (totalStacks.Count == 0)
                {
                    builder.AppendLine("- Inventory is empty.");
                    return;
                }

                foreach (KeyValuePair<string, int> entry in totalStacks.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    builder.AppendLine("- " + entry.Key + ": " + entry.Value + " units");
            }
            catch (Exception exception)
            {
                builder.AppendLine("- Unable to read inventory: " + exception.Message);
            }
        }

        private static void AppendCrafting(StringBuilder builder)
        {
            builder.AppendLine("CRAFTING");
            try
            {
                RedEdenCraftingStation[] stations = Resources.FindObjectsOfTypeAll<RedEdenCraftingStation>();
                if (stations == null || stations.Length == 0)
                {
                    builder.AppendLine("- No RED EDEN crafting stations detected.");
                    return;
                }

                foreach (RedEdenCraftingStation station in stations.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase))
                {
                    if (station == null)
                        continue;
                    builder.AppendLine("- Station " + station.name + " | state=" + station.State + " | recipe=" +
                        (station.ActiveRecipe != null ? station.ActiveRecipe.DisplayName : "none") +
                        " | progress=" + station.Progress.ToString("P0", CultureInfo.InvariantCulture) +
                        " | remaining=" + station.RemainingSeconds.ToString("0.0s", CultureInfo.InvariantCulture));
                }
            }
            catch (Exception exception)
            {
                builder.AppendLine("- Unable to read crafting stations: " + exception.Message);
            }
        }

        private static void AppendVitals(StringBuilder builder)
        {
            builder.AppendLine("PLAYER VITALS");
            try
            {
                MonoBehaviour[] all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                bool found = false;
                foreach (MonoBehaviour behaviour in all)
                {
                    if (behaviour == null)
                        continue;
                    if (behaviour is IARESSurvivalVitalsSource source)
                    {
                        found = true;
                        foreach (ARESVitalMetric metric in Enum.GetValues(typeof(ARESVitalMetric)))
                        {
                            if (source.TryGetValue(metric, out float value))
                                builder.AppendLine("- " + metric + ": " + value.ToString("0.##", CultureInfo.InvariantCulture));
                        }
                        break;
                    }
                }

                if (!found)
                    builder.AppendLine("- No survival vitals provider is attached.");
            }
            catch (Exception exception)
            {
                builder.AppendLine("- Unable to read player vitals: " + exception.Message);
            }
        }

        private static void AppendMissionStatus(StringBuilder builder)
        {
            builder.AppendLine("MISSIONS");
            builder.AppendLine("- Mission system is read-only and currently not exposed to the AI; ARES must answer from the active mission data if available in the scene.");
        }
    }
}
