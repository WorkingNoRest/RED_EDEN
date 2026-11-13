# ARES integration package

This package contains the ARES Unity runtime, a secure FastAPI-to-Gemini Live WebSocket gateway, focused backend and Unity tests, and copy-and-paste setup instructions.

## Contents

- `Unity/Assets/RED EDEN/ARES/` — ARES runtime scripts, test scripts, configuration assets, and matching Unity `.meta` files.
- `Backend/` — FastAPI session and Live WebSocket gateway, pinned runtime/test dependencies, and a safe environment-variable template.
- `Documentation/` — architecture, game integration, Live protocol, and validation status.
- `INSTALLATION_GUIDE.md` — Unity, local Windows backend, Render Free testing, and production setup instructions.
- `render.yaml` — Render Blueprint for a single-instance Free test service.

## Security model

- `GEMINI_API_KEY` and `ARES_SESSION_SIGNING_KEY` are backend-only environment secrets. They are never embedded in Unity assets or client builds.
- The backend authenticates its Gemini Developer API WebSocket with the server-side `key` query parameter; its credential-bearing upstream URL is never sent to Unity or written to application logs.
- Unity requests an anonymous, HMAC-signed bearer session that expires after five minutes. This temporary session token is extractable from a distributed client but cannot reveal the Gemini key and is not treated as a secure secret.
- Anonymous sessions do not prove player identity. Add verification of your platform-issued identity token before launch if requests must be limited to known players.
- The backend uses a fixed model, read-only function allowlist, bounded messages, and per-IP/process capacity controls. For public/multi-instance deployments, configure shared gateway rate limits and connection limits.
- Use HTTPS/WSS for remote deployments. Render Free services can sleep or restart and are for testing, not production.

## Dependencies

- Unity Input System package.
- `com.whisper.unity`: `https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity`
- RED EDEN Crafting and Devion Inventory System assemblies used by the game bridge.
- Python runtime/test dependencies are pinned in `Backend/requirements.txt` and `Backend/requirements-test.txt`.

Follow [INSTALLATION_GUIDE.md](./INSTALLATION_GUIDE.md) for exact setup steps and [ARES_GEMINI_LIVE_SETUP.md](./Documentation/ARES_GEMINI_LIVE_SETUP.md) for protocol details.
