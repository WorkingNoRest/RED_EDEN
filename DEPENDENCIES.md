# Dependencies

## Required for the Unity package

| Dependency | Use | How to obtain |
|---|---|---|
| Unity Editor 6000.3.16f1 (or compatible Unity 6) | Import, edit, and build the project assets | Unity Hub under the applicable Unity terms |
| Unity Input System 1.19.0 | Player and ARES input | Unity Package Manager; merge `ARES_INTEGRATION_PACKAGE/Unity/manifest-dependencies.json` into the existing manifest |
| Unity Universal Render Pipeline 17.3.0 | Render pipeline used by the source project | Unity Package Manager. The target project must use a compatible render pipeline for matching material appearance |
| RED EDEN Crafting assembly | Crafting stations, recipes, and resource nodes | Included in the Full-System Unity package |
| Devion Inventory System, Stat System, Triggers, Utilities, and UI Widgets | Inventory, player-health, and interactable integration | Install from the Unity account that owns the applicable Devion license; not included or redistributed. The source assets contain no reliable Devion version metadata, so use the same compatible licensed release already used by the source project |
| TextMesh Pro | No direct references found in RED EDEN scripts or included prefab/material sources | Not required by the inspected RED EDEN integration; install only if the receiving project or separately installed UI uses it |

Install all required Devion modules before importing the Full-System package so the Devion GUID-based assembly references resolve. The package does not include Devion source, compiled code, examples, UI, or binaries. Do not copy Devion files into the public repository unless Devion's applicable license explicitly authorizes that distribution.

## Optional ARES backend

| Dependency | Version | Use |
|---|---:|---|
| Python | 3.10+ recommended | Run the backend |
| FastAPI | 0.115.0 | HTTP health/session endpoints and WebSocket routing |
| Uvicorn | 0.30.6 | ASGI server |
| python-dotenv | 1.0.1 | Local ignored `.env` loading |
| slowapi | 0.1.9 | HTTP session endpoint rate limit |
| websockets | 13.1 | Server-to-server Gemini Live WebSocket |
| httpx | 0.27.2 (tests) | FastAPI TestClient dependency |

Python transitive dependencies are resolved by pip from the pinned top-level requirements. Their upstream licenses and notices remain applicable. The project does not vendor these packages.

## External services and secrets

- Google Gemini Developer API Live access is required; the model is fixed in the backend to `gemini-3.8-live`. Apply Google's current API and service terms and usage pricing.
- Render is an optional test host. The Blueprint is in `ARES_INTEGRATION_PACKAGE/render.yaml`; configure secrets in the Render dashboard.
- `GEMINI_API_KEY` and `ARES_SESSION_SIGNING_KEY` must exist only in the backend secret environment or ignored local `.env`. Never put either in Unity, Git, logs, or the PDFs.
- ARES does not depend on Whisper, Ollama, local model downloads, or an offline inference service.
