# ARES integration package

This package contains the ARES Unity runtime, Gemini HTTP backend, and copy-and-paste setup instructions.

## Contents

- `Unity/Assets/RED EDEN/ARES/` — ARES runtime scripts, configuration assets, and matching Unity `.meta` files.
- `Backend/` — FastAPI service, pinned Python dependencies, and a safe environment-variable template.
- `Documentation/` — architecture, integration notes, and validation status.
- `INSTALLATION_GUIDE.md` — complete backend and Unity setup.

## Security model

- The Gemini API key and session-signing key are backend-only environment variables. Neither is included in Unity assets or this repository.
- Unity requests an anonymous, HMAC-signed bearer session that expires after five minutes. The client stores it only in memory and requests a new one as needed; there is no permanent client token.
- Anonymous sessions do not prove a player's identity. The session endpoint is public by design, so the IP-based limits and input bounds reduce abuse but are not a substitute for account/platform authentication. Add verification of your platform-issued identity token before launch if requests must be limited to known players.
- The default in-memory rate limiter is per process. For multiple workers or public deployment, enforce limits at an HTTPS reverse proxy/API gateway and configure a shared SlowAPI storage backend.
- Do not expose the development Uvicorn server directly to the Internet. Terminate TLS at a maintained reverse proxy and keep secrets out of logs, Unity builds, and source control.

## Dependencies

- Unity Input System package.
- `com.whisper.unity`: `https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity`
- RED EDEN Crafting and Devion Inventory System assemblies used by the game bridge.
- Python dependencies are pinned in `Backend/requirements.txt`.

Follow [INSTALLATION_GUIDE.md](./INSTALLATION_GUIDE.md) for exact copy-and-paste steps.
