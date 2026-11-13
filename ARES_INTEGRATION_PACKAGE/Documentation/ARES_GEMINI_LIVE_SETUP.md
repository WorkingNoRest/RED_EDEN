# ARES Gemini Live setup

ARES uses a native, full-duplex Gemini Live WebSocket session. Unity streams microphone PCM while V is held and plays Gemini's streamed audio; it does not substitute an HTTP text-generation request or embed a Gemini credential in the game.

## Model and audio protocol

- The backend pins `models/gemini-3.8-live`; Unity cannot select another model.
- Gemini's documented Live API supports audio input/output, synchronous and asynchronous function calling, transcription, and interruption.
- Unity converts microphone samples to mono, signed 16-bit little-endian PCM at 16 kHz and sends bounded binary WebSocket frames.
- Gemini audio output is forwarded as raw 16-bit PCM at 24 kHz to the Unity audio ring buffer.
- Hold V to stream speech; release V to end the activity. Press V while ARES is speaking to interrupt and flush queued playback.
- The backend enables only four read-only game tools: current vitals, inventory, crafting status, and exposed mission status. Mission status reports that data is unavailable until a game mission provider is integrated.
- Local NASA archive retrieval can supplement a spoken request when its final/interim transcription is available before V is released. Missing archive records do not block Live speech.

## Required server environment

- `GEMINI_API_KEY`: Google API key, server secret only.
- `ARES_SESSION_SIGNING_KEY`: random value of at least 32 bytes, server secret only.
- `ARES_RATE_LIMIT_STORAGE_URI`: defaults to process-local `memory://`; shared/public deployments still need gateway rate limits.

There is no `GEMINI_MODEL` setting: model selection and tool definitions remain backend-controlled. Never place either secret in Unity configuration, a Unity build, command-line arguments, logs, or source control.

## Upstream Developer API connection

- The backend uses the Gemini Developer API WebSocket endpoint `wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent` and the fixed `gemini-3.8-live` model.
- Developer API authentication is sent using Google's `key` query parameter on the backend's upstream TLS WebSocket. This is separate from the five-minute ARES bearer token Unity presents to the backend; neither the Gemini key nor the upstream URL is sent to Unity.
- The backend sends `setup` with `model: "models/gemini-3.8-live"` and `generationConfig.responseModalities: ["AUDIO"]`, along with its server-controlled instructions, tools, and realtime/transcription settings. This follows the current [`BidiGenerateContentSetup` API reference](https://ai.google.dev/api/live#BidiGenerateContentSetup) and its [`GenerationConfig` fields](https://ai.google.dev/api/generate-content#GenerationConfig). It waits for the server's `setupComplete` event before reporting `ready`.
- With pinned `websockets==13.1`, `recv()` returns `str` for a TEXT frame and `bytes` for a BINARY frame. The backend attempts strict UTF-8 decoding of bytes before JSON parsing; it classifies errors, malformed/empty frames, unexpected envelopes, and closure without logging frame contents.
- The backend suppresses the WebSocket library's internal logging because the upstream URL contains the credential. Its own diagnostics log only the endpoint without query, API version, model, handshake/close status, frame type/opcode/length, UTF-8 validity, safe JSON top-level keys, whether authentication and setup were accepted, and sanitized upstream errors. Client-facing errors remain generic.
- In Render, set the exact variable `GEMINI_API_KEY` to a valid Gemini Developer API key with Live access for the selected model. Check that the key's API restrictions allow the Generative Language API and that any application restriction permits server-side Render traffic. Do not send the key or session tokens in chat or reports.

## Deployment and service access

See the [installation guide](../INSTALLATION_GUIDE.md) for copy-and-paste Windows and Render steps. The included `render.yaml` can be selected as the Blueprint file during Render setup. It defines a Free service for testing; Render Free web services can sleep after 15 minutes without inbound traffic, take about a minute to start, and may restart. A Live WebSocket is not guaranteed to survive deploys or platform restarts. Use a paid service and gateway-backed rate/concurrency controls for production.

## Authentication boundaries

Unity requests a five-minute HMAC-signed anonymous session, then presents it as a bearer token in the WebSocket handshake. The backend validates it before accepting the socket and uses the Gemini API key only in the upstream server-to-server connection. Session tokens are temporary access credentials, not secure secrets; a player can extract one from a running/distributed client, but it expires quickly and cannot reveal the Gemini key.

The session is anonymous and does not prove player identity. Add verification of a platform-issued identity token and enforce per-player quotas before public launch or per-player billing.

## Upstream failure diagnostics

The backend server logs contain a sanitized `Gemini Live upstream` diagnostic for connection, setup, and relay failures. It includes the configured endpoint/API version/model, upstream HTTP status when available, WebSocket close code, authentication/setup acceptance state, and a sanitized error status/message. Gemini keys, ARES bearer tokens, authorization headers, full request payloads, and credential-bearing URLs are not logged or returned to Unity. A successful health check or ARES session issuance does not prove that the upstream Gemini Live connection succeeded; only the actual `setupComplete` event marks setup accepted.

Relay validation failures are logged with a fixed `stage` and `diagnostic` code. These codes identify whether the backend rejected client audio/control data or an upstream response without logging message contents, transcripts, or audio.

## Verification limits

The official model documentation lists `gemini-3.8-live` as stable and supports these protocol features. Account-specific API-key entitlement, actual upstream calls, Arabic/Egyptian-Arabic quality, device microphone behavior, and Unity runtime playback require credentials and a Unity runtime; they are not claimed as tested by local compilation or backend unit tests.
