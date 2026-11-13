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

## Deployment and service access

See the [installation guide](../INSTALLATION_GUIDE.md) for copy-and-paste Windows and Render steps. The included `render.yaml` can be selected as the Blueprint file during Render setup. It defines a Free service for testing; Render Free web services can sleep after 15 minutes without inbound traffic, take about a minute to start, and may restart. A Live WebSocket is not guaranteed to survive deploys or platform restarts. Use a paid service and gateway-backed rate/concurrency controls for production.

## Authentication boundaries

Unity requests a five-minute HMAC-signed anonymous session, then presents it as a bearer token in the WebSocket handshake. The backend validates it before accepting the socket and uses the Gemini API key only in the upstream server-to-server connection. Session tokens are temporary access credentials, not secure secrets; a player can extract one from a running/distributed client, but it expires quickly and cannot reveal the Gemini key.

The session is anonymous and does not prove player identity. Add verification of a platform-issued identity token and enforce per-player quotas before public launch or per-player billing.

## Verification limits

The official model documentation lists `gemini-3.8-live` as stable and supports these protocol features. Account-specific API-key entitlement, actual upstream calls, Arabic/Egyptian-Arabic quality, device microphone behavior, and Unity runtime playback require credentials and a Unity runtime; they are not claimed as tested by local compilation or backend unit tests.
