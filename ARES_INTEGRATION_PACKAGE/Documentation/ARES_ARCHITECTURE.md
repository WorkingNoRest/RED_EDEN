# ARES architecture and security

## Runtime flow

1. The player holds V. `ARESController` asks the existing microphone component for streaming PCM and opens a Gemini Live session when needed.
2. Unity obtains a five-minute anonymous bearer session from `POST /api/ares/session`. It never receives the Gemini API key.
3. Unity opens `wss://<backend>/api/ares/live` with the bearer token in the handshake. The backend validates the signature and expiry before accepting the socket.
4. The backend connects to Google's Live WebSocket API using its server-only API key and a fixed `models/gemini-3.8-live` setup. It relays bounded PCM and JSON control messages in both directions.
5. Gemini output PCM is queued in Unity's 24 kHz streaming audio player. New V activity interrupts the model and flushes queued audio.
6. Only read-only player-status, inventory, crafting, and mission-status function calls are declared. Unity validates each call again and returns bounded summaries; no game-changing actions are exposed.
7. Release V ends the audio activity. When local speech transcription and the NASA archive are available, ARES adds a bounded evidence excerpt before ending the turn.

The HTTP text-generation route has been removed. The Live WebSocket is the voice path, not an HTTP chat substitute.

## Authentication boundaries

- The backend signs HS256 session tokens with `ARES_SESSION_SIGNING_KEY`; tokens last five minutes and are validated before the WebSocket is accepted.
- A token is temporary client-side access, not a secure secret. It can be extracted from a running game and replayed until expiry, but it does not expose the Gemini API key.
- The public session endpoint issues anonymous sessions because a distributed client cannot safely hold a shared secret. Anonymous sessions do not prove player identity; verify platform-issued identity tokens before public launch or per-player billing.
- Per-IP session-creation limits, WebSocket handshake limits, process-local concurrency limits, bounded messages, short sessions, and a fixed tool allowlist are abuse controls. They do not replace identity verification, a shared limiter, or gateway protections.
- Remote Unity endpoints require HTTPS/WSS; plain HTTP/WS is allowed only for loopback development.
- Audio and bearer tokens are not logged. Gemini upstream errors are reduced to generic client messages and exception types in server logs.

## Deployment constraints

The included Render Blueprint targets a Free service for testing. Free services can sleep after 15 minutes without inbound traffic and can restart at any time. WebSockets can be interrupted by deploys and maintenance. Limiter/concurrency state is process-local; use shared gateway controls for multiple workers or instances. See the [installation guide](../INSTALLATION_GUIDE.md).

## Compatibility and unverified behavior

The backend uses the documented Gemini Live WebSocket endpoint with raw PCM (16 kHz input, 24 kHz output). Unity uses `ClientWebSocket`; WebGL is not supported by this client implementation. Unity Editor/Play Mode, target-platform microphone capture, actual API-key entitlement, audio quality, and Arabic/Egyptian-Arabic performance need runtime credentials and were not locally verified.
