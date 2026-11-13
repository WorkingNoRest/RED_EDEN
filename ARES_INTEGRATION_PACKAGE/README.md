# ARES Gemini Live integration

This source package contains the project-owned ARES Live runtime and a FastAPI relay to Google's native Gemini Live WebSocket API. The complete importable RED EDEN Unity package is `../packages/RedEden-Full-System.unitypackage`.

## Contents

- `Unity/Assets/RED EDEN/ARES/` — ARES source, `.meta` files, runtime configuration, knowledge data, and focused PCM tests. The Full-System export is curated and excludes legacy local-model scripts and configurations.
- `Backend/` — authenticated session endpoint, bounded Gemini Live WebSocket relay, tests, pinned requirements, and `.env.example`.
- `Documentation/` — architecture, game integration, protocol, and validation status.
- `INSTALLATION_GUIDE.md` — Unity import, local Windows testing, and Render deployment steps.
- `render.yaml` — one-worker Render Blueprint for testing.

## Runtime and dependencies

- Hold **V** to stream mono PCM16 audio at 16 kHz; Gemini's 24 kHz PCM output is played in Unity. The backend fixes the model and exposes only read-only game tools.
- This package does not include or initialize Ollama, Whisper, offline models, or model downloads. Legacy-named components are inert compatibility stubs only; the provisioning, recognition, and local-generation implementations are removed.
- Unity requires Input System and the target project's RED EDEN Crafting and licensed Devion Inventory assemblies. Install Devion from its licensed source; no Devion code or binaries are redistributed here.
- Python dependencies are pinned in `Backend/requirements.txt` and `Backend/requirements-test.txt`.

## Secrets and limits

- `GEMINI_API_KEY` and `ARES_SESSION_SIGNING_KEY` are backend-only environment values; never place them in Unity assets or builds.
- Unity receives a short-lived anonymous bearer session, not the Gemini key. Anonymous access does not prove a player's identity.
- The backend uses bounded messages, a fixed model, allowlisted read-only tools, and per-process limits. Add player authentication and shared gateway limits before public production use.
- Render Free is a test host and may sleep or restart. The hosted service must be redeployed/configured with the Live relay and Gemini key before it can serve Live sessions.

See [INSTALLATION_GUIDE.md](./INSTALLATION_GUIDE.md), [ARES_GEMINI_LIVE_SETUP.md](./Documentation/ARES_GEMINI_LIVE_SETUP.md), and the [dependency/licensing notes](./Unity/DEPENDENCIES.md).
