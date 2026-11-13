# ARES architecture and security

## Runtime flow

1. `ARESController` retrieves supporting local NASA archive records.
2. With `ARESGeminiLiveClient` assigned, the controller sends the player question and a bounded read-only game/archive snapshot to the backend; otherwise it retains the existing local Ollama path.
3. Unity requests a short-lived anonymous session token and holds it only for the in-flight request.
4. The backend validates the signed token, request sizes and fields, applies per-IP rate limits, and calls Gemini using its server-only API key.
5. Backend responses return to Unity and are delivered through the existing ARES response/voice-output path.

The HTTP integration generates text with Gemini. It does not expose a Gemini API key to the game and is not a direct Gemini Live audio/WebSocket connection.

## Authentication boundaries

- The backend signs HS256 tokens with `ARES_SESSION_SIGNING_KEY`, which must be generated and provisioned outside Unity and source control. Tokens are valid for five minutes.
- The public session endpoint issues anonymous sessions because a distributed client cannot safely hold a shared secret. A signed session protects the chat endpoint from unsigned requests and bounds replay time, but it does not prove a player's identity or stop a modified client from requesting sessions.
- IP-based limits and strict input sizes constrain accidental/low-effort abuse. Public or multi-worker deployments require gateway/shared rate limiting and should verify platform-issued player identity before incurring per-player costs.
- Unity rejects remote HTTP endpoints; HTTPS is required except for loopback development.
- Gemini and unexpected backend exceptions are logged server-side and returned as generic errors.
