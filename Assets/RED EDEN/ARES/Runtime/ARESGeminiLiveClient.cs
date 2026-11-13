using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESGeminiLiveClient : MonoBehaviour
    {
        private const int MaximumPromptLength = 4000;
        private const int MaximumGameContextLength = 8000;

        [SerializeField] private ARESGeminiLiveConfiguration configuration;

#pragma warning disable 0649
        [Serializable]
        private sealed class GeminiChatRequest
        {
            public string language;
            public string prompt;
            public string gameContext;
            public string playerName;
        }

        [Serializable]
        private sealed class GeminiSessionResponse
        {
            public string access_token;
            public string token_type;
            public int expires_in;
        }

        [Serializable]
        private sealed class GeminiChatResponse
        {
            public bool ok;
            public string answer;
            public string error;
        }
#pragma warning restore 0649

        public IEnumerator Ask(
            string userPrompt,
            string language,
            string gameContext,
            Action<string> onSuccess,
            Action<string> onError)
        {
            if (configuration == null)
            {
                onError?.Invoke("ARES Gemini Live configuration is missing.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(userPrompt) || userPrompt.Length > MaximumPromptLength)
            {
                onError?.Invoke("ARES prompt must contain between 1 and 4000 characters.");
                yield break;
            }

            string finalLanguage = string.IsNullOrWhiteSpace(language)
                ? configuration.DefaultLanguage
                : language.Trim();
            string finalContext = gameContext ?? string.Empty;
            if (finalLanguage.Length > 32 || finalContext.Length > MaximumGameContextLength)
            {
                onError?.Invoke("ARES request language or game context exceeds the configured limit.");
                yield break;
            }

            if (!TryGetEndpoint(configuration.BackendBaseUrl, "/api/ares/session", out string sessionUrl, out string error))
            {
                onError?.Invoke(error);
                yield break;
            }

            string accessToken = null;
            string sessionError = null;
            yield return CreateSession(sessionUrl, token => accessToken = token, message => sessionError = message);
            if (!string.IsNullOrWhiteSpace(sessionError))
            {
                onError?.Invoke(sessionError);
                yield break;
            }
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                onError?.Invoke("ARES backend did not issue a session token.");
                yield break;
            }

            if (!TryGetEndpoint(configuration.BackendBaseUrl, "/api/ares/chat", out string chatUrl, out error))
            {
                onError?.Invoke(error);
                yield break;
            }

            string payloadJson = JsonUtility.ToJson(new GeminiChatRequest
            {
                language = finalLanguage,
                prompt = userPrompt.Trim(),
                gameContext = finalContext,
                playerName = ARESSystemConfiguration.PlayerName
            });

            using (UnityWebRequest request = CreateJsonPost(chatUrl, payloadJson))
            {
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
                request.timeout = 45;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string detail = request.downloadHandler != null &&
                        !string.IsNullOrWhiteSpace(request.downloadHandler.text)
                        ? request.downloadHandler.text
                        : request.error;
                    onError?.Invoke("Gemini backend request failed (" + request.responseCode + "): " + detail);
                    yield break;
                }

                GeminiChatResponse response;
                try
                {
                    response = JsonUtility.FromJson<GeminiChatResponse>(request.downloadHandler.text);
                }
                catch (ArgumentException exception)
                {
                    onError?.Invoke("Gemini backend returned unusable JSON: " + exception.Message);
                    yield break;
                }

                if (response == null || !response.ok)
                {
                    onError?.Invoke(string.IsNullOrWhiteSpace(response?.error)
                        ? "Gemini backend returned an invalid response."
                        : response.error);
                    yield break;
                }

                if (string.IsNullOrWhiteSpace(response.answer))
                {
                    onError?.Invoke("Gemini backend completed without a response.");
                    yield break;
                }

                onSuccess?.Invoke(response.answer.Trim());
            }
        }

        private static IEnumerator CreateSession(string endpoint, Action<string> onSuccess, Action<string> onError)
        {
            using (UnityWebRequest request = CreateJsonPost(endpoint, "{}"))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string detail = request.downloadHandler != null &&
                        !string.IsNullOrWhiteSpace(request.downloadHandler.text)
                        ? request.downloadHandler.text
                        : request.error;
                    onError?.Invoke("ARES session request failed (" + request.responseCode + "): " + detail);
                    yield break;
                }

                GeminiSessionResponse response;
                try
                {
                    response = JsonUtility.FromJson<GeminiSessionResponse>(request.downloadHandler.text);
                }
                catch (ArgumentException exception)
                {
                    onError?.Invoke("ARES backend returned an invalid session response: " + exception.Message);
                    yield break;
                }

                if (response == null ||
                    !string.Equals(response.token_type, "bearer", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(response.access_token) ||
                    response.access_token.Length > 2048 ||
                    response.expires_in < 1 ||
                    response.expires_in > 300)
                {
                    onError?.Invoke("ARES backend returned an invalid or expired session.");
                    yield break;
                }

                onSuccess?.Invoke(response.access_token);
            }
        }

        private static UnityWebRequest CreateJsonPost(string endpoint, string json)
        {
            UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static bool TryGetEndpoint(string baseUrl, string path, out string endpoint, out string error)
        {
            endpoint = null;
            error = null;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "ARES Gemini backend URL must be an absolute URL without credentials or query parameters.";
                return false;
            }

            bool localHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                uri.IsLoopback;
            bool secureHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!localHttp && !secureHttps)
            {
                error = "ARES requires HTTPS for remote backend URLs; HTTP is allowed only for localhost.";
                return false;
            }

            Uri root = new Uri(uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/", UriKind.Absolute);
            endpoint = new Uri(root, path.TrimStart('/')).ToString();
            return true;
        }
    }
}
