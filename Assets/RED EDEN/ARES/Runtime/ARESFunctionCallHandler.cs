using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RedEden.ARES
{
    [Serializable]
    public sealed class ARESLiveToolCall
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class ARESLiveToolResponse
    {
        public string id;
        public string name;
        public ARESLiveToolResult response;
    }

    [Serializable]
    public sealed class ARESLiveToolResult
    {
        public string summary;
    }

    [Serializable]
    internal sealed class ARESLiveToolResponseMessage
    {
        public string type = "tool_response";
        public ARESLiveToolResponse[] responses;
    }

    [DisallowMultipleComponent]
    public sealed class ARESFunctionCallHandler : MonoBehaviour
    {
        private const int MaximumResponseBytes = 1500;

        public string HandleCalls(ARESLiveToolCall[] calls)
        {
            if (calls == null || calls.Length == 0 || calls.Length > 4)
                throw new ArgumentException("ARES received an invalid tool call batch.", nameof(calls));

            List<ARESLiveToolResponse> responses = new List<ARESLiveToolResponse>(calls.Length);
            foreach (ARESLiveToolCall call in calls)
            {
                if (call == null || string.IsNullOrWhiteSpace(call.id) || call.id.Length > 128)
                    throw new ArgumentException("ARES received an invalid tool call.", nameof(calls));

                string summary;
                if (call.name == "get_player_status")
                    summary = ARESGameBridge.GetPlayerStatusSummary();
                else if (call.name == "get_inventory")
                    summary = ARESGameBridge.GetInventoryStatusSummary();
                else if (call.name == "get_crafting_status")
                    summary = ARESGameBridge.GetCraftingStatusSummary();
                else if (call.name == "get_mission_status")
                    summary = ARESGameBridge.GetMissionStatusSummary();
                else
                    throw new ArgumentException("ARES rejected an unsupported read-only tool.", nameof(calls));

                responses.Add(new ARESLiveToolResponse
                {
                    id = call.id,
                    name = call.name,
                    response = new ARESLiveToolResult { summary = Limit(summary) }
                });
            }

            return JsonUtility.ToJson(new ARESLiveToolResponseMessage { responses = responses.ToArray() });
        }

        private static string Limit(string text)
        {
            string value = string.IsNullOrWhiteSpace(text) ? "No data is currently available." : text.Trim();
            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                if (char.IsControl(characters[i]))
                    characters[i] = ' ';
            }
            value = new string(characters);
            while (Encoding.UTF8.GetByteCount(value) > MaximumResponseBytes)
            {
                int length = Math.Max(0, value.Length * 3 / 4);
                if (length > 0 && char.IsHighSurrogate(value[length - 1]))
                    length--;
                value = value.Substring(0, length);
            }
            return value;
        }
    }
}
