using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESLocalBridge : MonoBehaviour
    {
        [SerializeField] private ARESLocalBridgeConfiguration configuration;

#pragma warning disable 0649
        [Serializable]
        private sealed class GenerateRequest
        {
            public string model;
            public string system;
            public string prompt;
            public bool stream;
            public string keep_alive;
            public GenerateOptions options;
        }

        [Serializable]
        private sealed class GenerateOptions
        {
            public int num_ctx;
            public float temperature;
        }

        [Serializable]
        private sealed class GenerateResponsePayload
        {
            public string response;
            public string error;
            public bool done;
        }
#pragma warning restore 0649

        public IEnumerator GenerateResponse(string systemPrompt, string userMessage,
            Action<string> onSuccess, Action<string> onError)
        {
            if (configuration == null)
            {
                onError?.Invoke("ARES local bridge configuration is not assigned.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                onError?.Invoke("ARES cannot send a request without a system prompt.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                onError?.Invoke("ARES cannot send an empty user prompt.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(configuration.Model))
            {
                onError?.Invoke("ARES Ollama model name is not configured.");
                yield break;
            }

            if (!TryGetLocalEndpoint(configuration.Endpoint, out string endpoint, out string endpointError))
            {
                onError?.Invoke(endpointError);
                yield break;
            }

            GenerateRequest payload = new GenerateRequest
            {
                model = configuration.Model,
                system = systemPrompt,
                prompt = "Player: " + ARESSystemConfiguration.PlayerName + "\n" +
                    "Player question: " + userMessage.Trim() + "\n\n" +
                    "Respond directly to the player. Always address him as " +
                    ARESSystemConfiguration.PlayerName + ".",
                stream = false,
                keep_alive = "30m",
                options = new GenerateOptions { num_ctx = 4096, temperature = 0.3f }
            };

            byte[] body;
            try
            {
                body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            }
            catch (ArgumentException exception)
            {
                onError?.Invoke("ARES could not serialize the Ollama request: " + exception.Message);
                yield break;
            }

            using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = configuration.TimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string responseBody = request.downloadHandler != null
                        ? request.downloadHandler.text
                        : string.Empty;
                    string detail = string.IsNullOrWhiteSpace(responseBody)
                        ? request.error
                        : request.error + " - " + responseBody.Trim();
                    onError?.Invoke("Ollama request failed (" + request.responseCode + "): " + detail);
                    yield break;
                }

                GenerateResponsePayload response;
                try
                {
                    response = JsonUtility.FromJson<GenerateResponsePayload>(request.downloadHandler.text);
                }
                catch (ArgumentException exception)
                {
                    onError?.Invoke("ARES could not parse the Ollama response: " + exception.Message);
                    yield break;
                }

                if (response == null)
                {
                    onError?.Invoke("Ollama returned an empty or invalid JSON response.");
                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(response.error))
                {
                    onError?.Invoke("Ollama returned an error: " + response.error);
                    yield break;
                }

                if (string.IsNullOrWhiteSpace(response.response))
                {
                    onError?.Invoke("Ollama completed the request without generated text.");
                    yield break;
                }

                onSuccess?.Invoke(response.response.Trim());
            }
        }

        private static bool TryGetLocalEndpoint(string configuredEndpoint, out string endpoint, out string error)
        {
            endpoint = null;
            error = null;
            if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out Uri uri))
            {
                error = "ARES Ollama endpoint must be an absolute URL.";
                return false;
            }

            bool loopbackHost = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
            if (!loopbackHost || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                error = "ARES only sends Ollama requests over HTTP to localhost or a loopback IP.";
                return false;
            }

            if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
            {
                endpoint = new Uri(uri, "/api/generate").ToString();
                return true;
            }

            if (!uri.AbsolutePath.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
            {
                error = "ARES Ollama endpoint must end with /api/generate.";
                return false;
            }

            endpoint = uri.ToString();
            return true;
        }
    }
}
