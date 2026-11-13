# Changelog

## Full-System Gemini Live release

- Added one curated `RedEden-Full-System.unitypackage` for RED EDEN gameplay assets, prepared databases/prefabs, and ARES Gemini Live.
- Added the native Gemini Live server-side WebSocket relay and the Unity PCM microphone/audio client.
- Fixed ARES controller flow to use Gemini Live voice only; local text/Ollama fallback is no longer invoked by the active controller.
- Removed legacy Ollama/Whisper model startup sources and their configuration assets; inert compatibility stubs preserve old scene script identities without starting services.
- Removed Whisper as an ARES package dependency.
- Added bilingual installation PDFs, dependency/licensing notices, and a release checklist.
- Kept Unity Third Person movement and camera outside the package.
- The Unity package excludes Devion proprietary content; the target project must install Devion from its licensed source.
- Cleaned the separate `.env.example` template; no real API key or signing secret is included.

### Validation status

- Unity `6000.3.16f1` compiled the package; 9 ARES and 6 Crafting EditMode tests passed.
- The 307-path package imported and compiled in a clean Unity project with licensed Devion dependencies installed locally; the export contains no scenes, Devion assets, or Starter Assets.
- ARES Python compilation passed.
- 27 backend unit tests passed.
- A live Google API call requires a user-owned API key and is not claimed as tested.
- Render's currently published endpoint still needs to be redeployed/configured with the WebSocket relay and server-side secrets.
