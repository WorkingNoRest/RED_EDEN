using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESGeminiLiveClient : MonoBehaviour
    {
        private const int MaximumFrameBytes = 65_536;
        private const int MaximumQueuedFrames = 128;
        private const int MaximumQueuedEvents = 512;
        private const int MaximumQueuedAudioBytes = 1_048_576;
        private const float ConnectTimeoutSeconds = 20f;
        private const float KeepAliveSeconds = 20f;

        [SerializeField] private ARESGeminiLiveConfiguration configuration;
        [SerializeField] private MonoBehaviour audioOutputProvider;
        [SerializeField] private ARESFunctionCallHandler functionCallHandler;

        [Serializable]
        private sealed class GeminiSessionResponse
        {
            public string access_token;
            public string token_type;
            public int expires_in;
        }

        [Serializable]
        private sealed class ServerMessage
        {
            public string type;
            public string message;
            public string text;
            public bool interim;
            public bool value;
            public ARESLiveToolCall[] calls;
        }

        [Serializable]
        private sealed class ControlMessage
        {
            public string type;
        }

        [Serializable]
        private sealed class ContextMessage
        {
            public string type = "context";
            public string archiveEvidence;
        }

        private sealed class ClientEvent
        {
            public string type;
            public string text;
            public byte[] data;
        }

        private sealed class OutboundFrame
        {
            public byte[] data;
            public WebSocketMessageType type;
        }

        private readonly ConcurrentQueue<ClientEvent> incomingEvents = new ConcurrentQueue<ClientEvent>();
        private readonly ConcurrentQueue<OutboundFrame> outgoingFrames = new ConcurrentQueue<OutboundFrame>();
        private readonly SemaphoreSlim outgoingSignal = new SemaphoreSlim(0);
        private IARESLiveAudioOutput audioOutput;
        private ClientWebSocket socket;
        private CancellationTokenSource connectionCancellation;
        private Task sendTask;
        private Task receiveTask;
        private int queuedFrameCount;
        private int queuedEventCount;
        private int queuedAudioBytes;
        private int queueOverflow;
        private float lastKeepAliveTime;
        private bool explicitlyDisconnected;

        public event Action<string, bool> InputTranscriptReceived;
        public event Action<string> OutputTranscriptReceived;
        public event Action<ARESLiveConnectionState> StateChanged;

        public ARESLiveConnectionState State { get; private set; } = ARESLiveConnectionState.Disconnected;
        public string LastError { get; private set; }
        public bool IsReady { get; private set; }
        public bool IsConnecting => State == ARESLiveConnectionState.Connecting;

        private void Awake()
        {
            audioOutput = audioOutputProvider as IARESLiveAudioOutput;
            if (audioOutputProvider != null && audioOutput == null)
                Debug.LogError("ARES audio output component does not implement IARESLiveAudioOutput.", audioOutputProvider);
            if (functionCallHandler == null)
                functionCallHandler = GetComponent<ARESFunctionCallHandler>();
        }

        private void Update()
        {
            if (Interlocked.Exchange(ref queueOverflow, 0) != 0)
            {
                Disconnect();
                SetError("ARES Live audio or event queue exceeded its safe limit.");
            }

            ClientEvent incoming;
            while (incomingEvents.TryDequeue(out incoming))
            {
                Interlocked.Decrement(ref queuedEventCount);
                if (incoming.data != null)
                    Interlocked.Add(ref queuedAudioBytes, -incoming.data.Length);
                HandleEvent(incoming);
            }

            if (IsReady && Time.unscaledTime - lastKeepAliveTime >= KeepAliveSeconds)
            {
                lastKeepAliveTime = Time.unscaledTime;
                QueueJson(JsonUtility.ToJson(new ControlMessage { type = "ping" }));
            }
        }

        private void OnDisable()
        {
            Disconnect();
        }

        public IEnumerator Connect(string language, Action onConnected, Action<string> onError)
        {
            if (IsReady)
            {
                onConnected?.Invoke();
                yield break;
            }
            if (IsConnecting)
            {
                onError?.Invoke("ARES is already connecting.");
                yield break;
            }
            if (configuration == null)
            {
                string message = "ARES Gemini Live configuration is missing.";
                SetError(message);
                onError?.Invoke(message);
                yield break;
            }

            if (socket != null || connectionCancellation != null)
                Disconnect();
            explicitlyDisconnected = false;
            LastError = null;
            SetState(ARESLiveConnectionState.Connecting);
            if (!TryGetSessionEndpoint(configuration.BackendBaseUrl, out string sessionUrl, out string error))
            {
                SetError(error);
                onError?.Invoke(error);
                yield break;
            }
            if (!TryGetLiveEndpoint(configuration.BackendBaseUrl, language, out Uri liveUri, out error))
            {
                SetError(error);
                onError?.Invoke(error);
                yield break;
            }

            string accessToken = null;
            string sessionError = null;
            yield return CreateSession(
                sessionUrl,
                token => accessToken = token,
                message => sessionError = message);
            if (!string.IsNullOrWhiteSpace(sessionError) || string.IsNullOrWhiteSpace(accessToken))
            {
                error = string.IsNullOrWhiteSpace(sessionError)
                    ? "ARES backend did not issue a session token."
                    : sessionError;
                SetError(error);
                onError?.Invoke(error);
                yield break;
            }

            socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", "Bearer " + accessToken);
            connectionCancellation = new CancellationTokenSource();
            Task connectTask;
            try
            {
                connectTask = socket.ConnectAsync(liveUri, connectionCancellation.Token);
            }
            catch (WebSocketException)
            {
                error = "ARES could not connect to its Live backend.";
                Disconnect();
                SetError(error);
                onError?.Invoke(error);
                yield break;
            }

            float connectStart = Time.realtimeSinceStartup;
            while (!connectTask.IsCompleted && Time.realtimeSinceStartup - connectStart < ConnectTimeoutSeconds)
                yield return null;
            if (!connectTask.IsCompleted)
            {
                error = "ARES Live connection timed out.";
                Disconnect();
                SetError(error);
                onError?.Invoke(error);
                yield break;
            }
            try
            {
                connectTask.GetAwaiter().GetResult();
            }
            catch (WebSocketException)
            {
                error = "ARES could not connect to Gemini Live.";
                Disconnect();
                SetError(error);
                onError?.Invoke(error);
                yield break;
            }
            catch (OperationCanceledException)
            {
                error = "ARES Live connection was cancelled.";
                Disconnect();
                SetError(error);
                onError?.Invoke(error);
                yield break;
            }

            sendTask = SendLoop(socket, connectionCancellation.Token);
            receiveTask = ReceiveLoop(socket, connectionCancellation.Token);
            lastKeepAliveTime = Time.unscaledTime;
            float readyStart = Time.realtimeSinceStartup;
            while (!IsReady && State != ARESLiveConnectionState.Error &&
                Time.realtimeSinceStartup - readyStart < ConnectTimeoutSeconds)
                yield return null;
            if (!IsReady)
            {
                error = string.IsNullOrWhiteSpace(LastError)
                    ? "ARES Live setup did not complete."
                    : LastError;
                Disconnect();
                SetError(error);
                onError?.Invoke(error);
                yield break;
            }

            onConnected?.Invoke();
        }

        public bool BeginUserActivity()
        {
            if (!IsReady)
                return false;
            if (!QueueJson(JsonUtility.ToJson(new ControlMessage { type = "activity_start" })))
                return false;
            audioOutput?.Flush();
            SetState(ARESLiveConnectionState.Listening);
            return true;
        }

        public void EndUserActivity()
        {
            if (!IsReady)
                return;
            QueueJson(JsonUtility.ToJson(new ControlMessage { type = "activity_end" }));
            if (State == ARESLiveConnectionState.Listening)
                SetState(ARESLiveConnectionState.Connected);
        }

        public bool SendPcm(byte[] pcm)
        {
            if (!IsReady || pcm == null || pcm.Length == 0 || pcm.Length > 3_200 || (pcm.Length & 1) != 0)
                return false;
            return QueueFrame(pcm, WebSocketMessageType.Binary);
        }

        public void SendArchiveEvidence(string evidence)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(evidence))
                return;
            string boundedEvidence = evidence.Length <= 4_000 ? evidence : evidence.Substring(0, 4_000);
            QueueJson(JsonUtility.ToJson(new ContextMessage { archiveEvidence = boundedEvidence }));
        }

        public void Disconnect()
        {
            explicitlyDisconnected = true;
            IsReady = false;
            if (connectionCancellation != null)
            {
                connectionCancellation.Cancel();
                connectionCancellation.Dispose();
                connectionCancellation = null;
            }
            if (socket != null)
            {
                socket.Abort();
                socket.Dispose();
                socket = null;
            }
            audioOutput?.Flush();
            while (outgoingFrames.TryDequeue(out _))
                Interlocked.Decrement(ref queuedFrameCount);
            while (incomingEvents.TryDequeue(out _))
                Interlocked.Decrement(ref queuedEventCount);
            Interlocked.Exchange(ref queuedAudioBytes, 0);
            SetState(ARESLiveConnectionState.Disconnected);
        }

        private void HandleEvent(ClientEvent incoming)
        {
            if (incoming.type == "audio")
            {
                if (audioOutput == null || !audioOutput.TryPlayPcm(incoming.data))
                {
                    Disconnect();
                    SetError("ARES could not queue Gemini audio for playback.");
                }
                return;
            }
            if (incoming.type == "transport_closed")
            {
                if (!explicitlyDisconnected)
                    SetError(string.IsNullOrWhiteSpace(incoming.text)
                        ? "ARES Live connection closed."
                        : incoming.text);
                IsReady = false;
                return;
            }
            if (incoming.type == "transport_error")
            {
                Disconnect();
                SetError(incoming.text);
                return;
            }

            ServerMessage message;
            try
            {
                message = JsonUtility.FromJson<ServerMessage>(incoming.text);
            }
            catch (ArgumentException)
            {
                SetError("ARES Live backend returned invalid JSON.");
                return;
            }
            if (message == null)
            {
                SetError("ARES Live backend returned an empty message.");
                return;
            }

            switch (message.type)
            {
                case "ready":
                    IsReady = true;
                    LastError = null;
                    SetState(ARESLiveConnectionState.Connected);
                    break;
                case "input_transcript":
                    InputTranscriptReceived?.Invoke(message.text ?? string.Empty, message.interim);
                    break;
                case "output_transcript":
                    OutputTranscriptReceived?.Invoke(message.text ?? string.Empty);
                    break;
                case "speaking":
                    SetState(message.value
                        ? ARESLiveConnectionState.Speaking
                        : ARESLiveConnectionState.Connected);
                    break;
                case "interrupted":
                    audioOutput?.Flush();
                    SetState(ARESLiveConnectionState.Connected);
                    break;
                case "turn_complete":
                    SetState(ARESLiveConnectionState.Connected);
                    break;
                case "tool_calls":
                    HandleToolCalls(message.calls);
                    break;
                case "tool_call_cancelled":
                case "pong":
                    break;
                case "reconnect_required":
                    SetError("Gemini Live is restarting this session. Release V and reconnect.");
                    break;
                case "error":
                    SetError(string.IsNullOrWhiteSpace(message.message)
                        ? "ARES Live request failed."
                        : message.message);
                    break;
                default:
                    SetError("ARES Live backend sent an unsupported event.");
                    break;
            }
        }

        private void HandleToolCalls(ARESLiveToolCall[] calls)
        {
            if (functionCallHandler == null)
            {
                SetError("ARES read-only function handler is not assigned.");
                return;
            }
            try
            {
                string response = functionCallHandler.HandleCalls(calls);
                QueueJson(response);
            }
            catch (ArgumentException exception)
            {
                SetError(exception.Message);
            }
        }

        private bool QueueJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;
            return QueueFrame(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text);
        }

        private bool QueueFrame(byte[] data, WebSocketMessageType type)
        {
            if (socket == null || socket.State != WebSocketState.Open || data == null || data.Length > MaximumFrameBytes)
                return false;

            int queued = Interlocked.Increment(ref queuedFrameCount);
            if (queued > MaximumQueuedFrames)
            {
                Interlocked.Decrement(ref queuedFrameCount);
                Interlocked.Exchange(ref queueOverflow, 1);
                return false;
            }

            outgoingFrames.Enqueue(new OutboundFrame { data = data, type = type });
            outgoingSignal.Release();
            return true;
        }

        private async Task SendLoop(ClientWebSocket activeSocket, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await outgoingSignal.WaitAsync(cancellationToken);
                    OutboundFrame frame;
                    if (!outgoingFrames.TryDequeue(out frame))
                        continue;
                    try
                    {
                        await activeSocket.SendAsync(
                            new ArraySegment<byte>(frame.data),
                            frame.type,
                            true,
                            cancellationToken);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref queuedFrameCount);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
                QueueTransportError("ARES could not send data to the Live backend.");
            }
            catch (ObjectDisposedException)
            {
                if (!cancellationToken.IsCancellationRequested)
                    QueueTransportError("ARES Live connection was closed.");
            }
        }

        private async Task ReceiveLoop(ClientWebSocket activeSocket, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[8_192];
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    using (MemoryStream messageBuffer = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await activeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                QueueEvent(new ClientEvent
                                {
                                    type = "transport_closed",
                                    text = "ARES Live backend closed the connection."
                                });
                                return;
                            }
                            if (messageBuffer.Length + result.Count > MaximumFrameBytes)
                                throw new InvalidDataException("ARES Live frame exceeded its size limit.");
                            messageBuffer.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        byte[] payload = messageBuffer.ToArray();
                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            if (payload.Length == 0 || (payload.Length & 1) != 0)
                                throw new InvalidDataException("ARES Live audio frame is invalid.");
                            QueueEvent(new ClientEvent { type = "audio", data = payload });
                        }
                        else if (result.MessageType == WebSocketMessageType.Text)
                        {
                            QueueEvent(new ClientEvent
                            {
                                type = "server_message",
                                text = Encoding.UTF8.GetString(payload)
                            });
                        }
                        else
                        {
                            throw new InvalidDataException("ARES Live frame type is invalid.");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
                if (!cancellationToken.IsCancellationRequested)
                    QueueEvent(new ClientEvent
                    {
                        type = "transport_error",
                        text = "ARES Live connection failed."
                    });
            }
            catch (InvalidDataException exception)
            {
                QueueEvent(new ClientEvent { type = "transport_error", text = exception.Message });
                activeSocket.Abort();
            }
            catch (ObjectDisposedException)
            {
                if (!cancellationToken.IsCancellationRequested)
                    QueueEvent(new ClientEvent
                    {
                        type = "transport_closed",
                        text = "ARES Live connection was closed."
                    });
            }
        }

        private void QueueTransportError(string message)
        {
            QueueEvent(new ClientEvent { type = "transport_error", text = message });
        }

        private void QueueEvent(ClientEvent incoming)
        {
            if (incoming.data != null)
            {
                int audioBytes = Interlocked.Add(ref queuedAudioBytes, incoming.data.Length);
                if (audioBytes > MaximumQueuedAudioBytes)
                {
                    Interlocked.Add(ref queuedAudioBytes, -incoming.data.Length);
                    Interlocked.Exchange(ref queueOverflow, 1);
                    return;
                }
            }
            int count = Interlocked.Increment(ref queuedEventCount);
            if (count > MaximumQueuedEvents)
            {
                Interlocked.Decrement(ref queuedEventCount);
                if (incoming.data != null)
                    Interlocked.Add(ref queuedAudioBytes, -incoming.data.Length);
                Interlocked.Exchange(ref queueOverflow, 1);
                return;
            }
            incomingEvents.Enqueue(incoming);
        }

        private static IEnumerator CreateSession(string endpoint, Action<string> onSuccess, Action<string> onError)
        {
            using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke("ARES session request failed (" + request.responseCode + ").");
                    yield break;
                }

                GeminiSessionResponse response;
                try
                {
                    response = JsonUtility.FromJson<GeminiSessionResponse>(request.downloadHandler.text);
                }
                catch (ArgumentException)
                {
                    onError?.Invoke("ARES backend returned an invalid session response.");
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

        private void SetError(string message)
        {
            if (socket != null || connectionCancellation != null)
                Disconnect();
            LastError = string.IsNullOrWhiteSpace(message) ? "ARES Live request failed." : message;
            IsReady = false;
            audioOutput?.Flush();
            SetState(ARESLiveConnectionState.Error);
            Debug.LogWarning(LastError, this);
        }

        private void SetState(ARESLiveConnectionState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private static bool TryGetSessionEndpoint(string baseUrl, out string endpoint, out string error)
        {
            endpoint = null;
            error = null;
            if (!TryGetBaseUri(baseUrl, out Uri uri, out error))
                return false;
            endpoint = new Uri(uri, "/api/ares/session").ToString();
            return true;
        }

        private static bool TryGetLiveEndpoint(string baseUrl, string language, out Uri endpoint, out string error)
        {
            endpoint = null;
            error = null;
            if (!TryGetBaseUri(baseUrl, out Uri uri, out error))
                return false;

            string cleanLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language.Trim();
            if (cleanLanguage.Length > 32 || cleanLanguage.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            {
                error = "ARES response language is invalid.";
                return false;
            }

            UriBuilder builder = new UriBuilder(uri)
            {
                Scheme = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    ? "wss"
                    : "ws",
                Path = "/api/ares/live",
                Query = "language=" + Uri.EscapeDataString(cleanLanguage)
            };
            endpoint = builder.Uri;
            return true;
        }

        private static bool TryGetBaseUri(string baseUrl, out Uri uri, out string error)
        {
            uri = null;
            error = null;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "ARES backend URL must be absolute and cannot contain credentials, query, or fragment.";
                return false;
            }

            bool localHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                uri.IsLoopback;
            bool secureHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!localHttp && !secureHttps)
            {
                error = "ARES requires HTTPS/WSS for remote backends; HTTP/WS is allowed only for localhost.";
                return false;
            }
            return true;
        }
    }
}
