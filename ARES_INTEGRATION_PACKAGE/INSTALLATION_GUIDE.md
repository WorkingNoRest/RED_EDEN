# ARES Gemini Live installation guide

ARES provides native, full-duplex Gemini Live voice. It streams PCM over an authenticated WebSocket; it does not use HTTP text generation as a voice substitute. The Gemini API key stays in the backend.

## 1. Prerequisites

- Unity Input System is installed and enabled under **Edit > Project Settings > Player > Active Input Handling**.
- The target project includes the RED EDEN Crafting and Devion Inventory System assemblies used by ARES.
- Add `com.whisper.unity` in **Window > Package Manager > + > Add package from git URL**:

  ```text
  https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity
  ```

- A Google Gemini Developer API key with Live API access to `gemini-3.8-live`. Documentation lists the model as stable, but each Google project must be checked with its own API key.
- A desktop or mobile Unity target that supports `System.Net.WebSockets.ClientWebSocket`. This implementation does not support WebGL.

## 2. Copy the Unity files

1. Back up `Assets/RED EDEN/ARES` if it already exists.
2. Copy the `Unity/Assets/RED EDEN/ARES` folder and its adjacent `Unity/Assets/RED EDEN/ARES.meta` file into the project's `Assets/RED EDEN/` folder. Preserve the folder structure and every nested `.meta` file. Do not copy only the `.cs` files.
3. Wait for Unity to finish importing and compiling. Resolve missing package/assembly references before continuing.
4. On the scene's existing ARES controller object, add/assign:
   - `ARESController`: assign the `ARESGeminiLiveClient`, game settings/vitals providers as applicable, and `ARESMicrophoneVoiceInput` as the voice input provider.
   - `ARESGeminiLiveClient`: assign an `ARES Gemini Live Configuration` asset.
   - `ARESMicrophoneVoiceInput`: use the existing component; it now streams microphone PCM for Live and retains offline capture for the local path.
   - `ARESAudioPlayer`: add it to the same object as `ARESGeminiLiveClient` or another active object; assign it to the client's **Audio Output Provider** field.
   - `ARESFunctionCallHandler`: add it to the client object and assign it to **Function Call Handler**. It only returns read-only summaries.
   - `ARESInterfaceController`: add it to an active object and assign the Live client to display connection/listening/speaking/error status.
5. In **Create > RED EDEN > ARES > Gemini Live Configuration**, set **Backend Base Url** to `http://localhost:8000` for local testing, or the Render service origin (for example `https://ares-gemini-live.onrender.com`). Use HTTPS for every non-loopback host.
6. On the controller, assign the Live client and microphone provider. The Live path uses **hold V to speak** and release to end the activity. Press V while ARES speaks to interrupt. The old text test panel is disabled when Live is configured.
7. Keep existing local Ollama/offline components assigned if they are used when Gemini Live is not configured. Live speech does not require the offline Whisper recognizer to be ready.

The four Live tools read current vitals, inventory, crafting, and mission status. Mission data is explicitly unavailable until a mission provider is exposed. Existing `ARESVitalsMonitor` and local alerts remain in place; no additional monitor or alert framework is required.

## 3. Run the backend locally (Windows PowerShell)

Open PowerShell in `ARES_INTEGRATION_PACKAGE\Backend`:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
Copy-Item .env.example .env
python -c "import secrets; print(secrets.token_urlsafe(48))"
```

Open `.env` in a local editor and set:

```dotenv
GEMINI_API_KEY=your_real_gemini_api_key
ARES_SESSION_SIGNING_KEY=paste_the_random_value_generated_above
ARES_RATE_LIMIT_STORAGE_URI=memory://
```

Keep both secrets in the backend environment only. `.env` is ignored by Git. Run the development service on loopback:

```powershell
python -m uvicorn Source.ares_gemini_server:app --host 127.0.0.1 --port 8000 --ws-max-size 16384
```

Check `http://127.0.0.1:8000/health`, then run the Unity scene. For backend tests, install test-only requirements and run:

```powershell
python -m pip install -r requirements-test.txt
python -m compileall -q Source Tests
python -m unittest discover -s Tests -v
```

The Unity live endpoint is `/api/ares/live`. It receives a bearer token in the WebSocket handshake. Unity streams 16 kHz, mono, signed PCM16 little-endian binary audio frames, plus small JSON activity/tool messages. The backend forwards Gemini's 24 kHz PCM output as binary frames and transcription/status/tool events as JSON. No audio is stored by this service.

## 4. Deploy a Render Free test service

1. Push the package branch to a Git provider connected to Render.
2. In Render, choose **New > Blueprint** and select the repository. During Blueprint setup, set the Blueprint file path to `ARES_INTEGRATION_PACKAGE/render.yaml`.
3. Review the service: `render.yaml` selects a single-instance Free web service with the backend directory as its root, a `/health` check, and the required WebSocket message-size limit.
4. Set `GEMINI_API_KEY` as a secret environment variable in Render. It must be a Google Gemini Developer API key for a project with access to `gemini-3.8-live`; ensure its API restrictions allow the Generative Language API and its application restrictions allow requests from the Render service. If using IP restrictions, account for the service's outbound egress. The Blueprint generates `ARES_SESSION_SIGNING_KEY`; do not replace it with a committed value.
5. Deploy and copy the service origin (for example, `https://ares-gemini-live.onrender.com`) into the Unity Live configuration asset. Do not add a trailing path or API key.
6. Wait for the service to finish its first deploy before opening the Unity scene.

Render supports inbound WebSockets and terminates TLS for public traffic. The included Free plan is for testing only: a Free web service can spin down after 15 minutes without inbound traffic, take about a minute to wake, consume up to the workspace's included monthly hours, and restart during platform maintenance/deploys. The Unity heartbeat keeps an active Live connection from going idle, but does not prevent service restarts. A disconnect ends the current conversation; reconnecting creates a new session. Use a paid service for production.

The in-memory session limiter, per-IP WebSocket attempt limiter, and concurrent-session counter are process-local. Run one worker for this configuration. Multiple workers/instances require a shared gateway or equivalent distributed rate/concurrency control; `ARES_RATE_LIMIT_STORAGE_URI` configures only the HTTP session-creation limiter.

## 5. Authentication and request limits

- Unity obtains a server-signed bearer session from `POST /api/ares/session`. Tokens expire in five minutes and are not permanent client secrets.
- The session is anonymous. Anyone who can reach the public session endpoint can request a session; a modified client can also extract and replay a live token until it expires. This does **not** make the token a secure secret or prove player identity.
- The Gemini API key is used only by the backend on the upstream TLS WebSocket, using Google's Developer API `key` query parameter. This authenticated URL is server-to-server only; Unity never receives it. Internal WebSocket protocol logging is disabled to prevent the credential-bearing URL from being logged.
- The backend records safe upstream diagnostics in its server logs: endpoint, API version, fixed model, handshake status, close code, authentication/setup acceptance, and sanitized Google error status/message. It never returns those details to Unity or logs credentials, bearer tokens, request payloads, or the upstream query URL.
- The service limits session creation to five requests per IP/minute; Live WebSocket handshakes to six attempts per IP/minute; concurrent Live sessions to two per IP and sixteen per process; incoming client messages to 3,600/minute; audio chunks to 3,200 bytes; and JSON control messages to 16 KiB.
- These are baseline abuse controls, not authorization or a substitute for account identity, shared gateway limits, monitoring, and spend controls. Verify your game's platform-issued identity token before allowing broad public access or per-player billing.

## 6. Production checklist

- [ ] Use HTTPS/WSS outside loopback and verify certificates.
- [ ] Store Gemini and signing keys in the host's secret manager/environment, not in this package.
- [ ] Add platform identity verification and enforce per-player quotas before public launch.
- [ ] Apply shared rate/concurrency limits and WebSocket timeouts at the gateway.
- [ ] Use a paid, restart-aware deployment for production; test reconnection behavior.
- [ ] Keep Uvicorn behind the platform/reverse proxy and set a bounded WebSocket message size.
- [ ] Ensure user consent/privacy disclosures cover voice audio sent to Google.
- [ ] Confirm no credential exists in Unity assets, build output, or Git-tracked files.

## Troubleshooting

- `503 Backend session authentication is not configured`: set `ARES_SESSION_SIGNING_KEY` to a random value of at least 32 bytes and restart.
- `401` / WebSocket policy close: the session token is missing, invalid, or expired; reconnect to request a new one.
- `1013` / capacity message: the per-IP/process Live limit or handshake rate limit was reached; wait and retry.
- `Gemini Live is unavailable or not enabled for this API project`: verify the backend API key, billing/project configuration, and Live API access for that Google project. Account entitlement is not inferred from documentation alone.
- For upstream setup failures, inspect the Render service logs for the sanitized `Gemini Live upstream` diagnostic. Confirm that `GEMINI_API_KEY` is present, valid, restricted to the Generative Language API as appropriate, and permitted for server-side Render requests. Never paste the key or a session token into chat, GitHub, source files, or a support report.
- No microphone/audio: confirm the microphone provider implements streaming, the client has an `ARESAudioPlayer` assigned, microphone permissions are granted, and the target supports `ClientWebSocket`.
- Unity Live does not compile: check the Unity Input System, `com.whisper.unity`, RED EDEN/Devion references, and the target's .NET API compatibility.
