# ARES Gemini Live Verification Report

## Backend tests

Verified with Python 3.14 on the repository backend:

- `python -m compileall -q Source tests` — passed.
- `python -m unittest discover -s tests -v` — 27 tests passed.

Coverage includes signed-session expiration/tampering, live WebSocket setup and relay protocol, UTF-8/binary frame validation, audio bounds, read-only function-call allowlisting, rate/concurrency limits, error sanitization, and cleanup paths.

## Unity and hosted service status

- Unity `6000.3.16f1` compiled the project and exported `RedEden-Full-System.unitypackage` (23,463,648 bytes).
- The RED EDEN ARES EditMode assembly passed all 9 tests: PCM resampling and player-vitals defaults/fallback.
- The RED EDEN Crafting EditMode assembly passed all 6 tests.
- The package audit found 307 asset paths, including the hydroponic farm model, agricultural-silo prefab, and item/crafting databases; it found no scenes, Devion assets, or Starter Assets.
- The package imported into a clean Unity 6 project with the project's locally licensed Devion dependencies installed, then compiled with no C# errors.
- No authenticated Gemini Live voice session or device-microphone round trip has been verified.
- The public `https://red-eden.onrender.com` service must not be considered Gemini Live-ready until its authenticated `/api/ares/live` WebSocket is tested against a current deployment. `/health` alone is not sufficient.
- Do not add API keys, session tokens, raw audio, or unredacted transcripts to this report.
