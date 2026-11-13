# ARES validation report

## Executed checks

- Python compilation: `python -m py_compile Source\ares_gemini_server.py Tests\test_ares_gemini_server.py` — passed.
- Backend tests: `python -m unittest discover -s Tests -v` — 15 passed, covering session signing/expiry, WebSocket authentication, protocol bounds, rate/concurrency limits, tool-call validation, audio forwarding, and upstream failures.
- Unity source/package synchronization — all Live runtime scripts and EditMode test sources match their package copies.
- Credential scan: no real Gemini API key, permanent bearer token, or private-key PEM marker was found in tracked files or Unity source/package assets. The only long-value pattern matches are the setup guide's explicit `your_real_...` and `paste_the_random_...` placeholders. No compiled Unity build artifacts were present under `Assets` to scan.
- .NET/Unity build: `dotnet build` was attempted and could not run because this checkout has no Unity `ProjectSettings\ProjectVersion.txt`, package manifest, `.sln`, or `.csproj` build input (`MSB1003`). The Unity editor is installed, but the game project needed to generate those inputs is not present here.
- Unity EditMode tests and live Gemini API calls were not run; they require a complete Unity project and an enabled Gemini Live API key, respectively.

## Security properties

- Unity requests five-minute server-signed bearer sessions; the token is temporary access, not a client-side secret or proof of player identity.
- The Gemini API key and session-signing key are backend environment values and are never sent to Unity.
- The backend controls the model/system instruction and only exposes a fixed allowlist of read-only tools.
- HTTP request bodies, JSON control messages, audio frames, tool responses, session duration, connection attempts, active sessions, and messages are bounded.
- The included Render configuration uses one worker because the additional WebSocket limits are process-local.

## Deployment and runtime limitations

- Anonymous sessions do not authenticate players. Production deployments should verify platform identity and enforce per-player quotas and shared gateway limits.
- No live Gemini call, account entitlement, Arabic speech quality, Unity Play Mode behavior, or Unity-generated build was verified in this checkout.
- The Render Free plan is for testing only; sleeps/restarts interrupt sessions, and the client starts a new session after reconnecting.
