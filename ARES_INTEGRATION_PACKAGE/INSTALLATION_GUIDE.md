# RED EDEN Full-System installation

The recommended delivery is the single Unity package `../packages/RedEden-Full-System.unitypackage`. It contains project-owned RED EDEN gameplay assets, prepared databases and prefabs, and the Gemini Live ARES runtime. It does not contain a player, original game scene, Unity Starter Assets, or Devion third-party source.

## 1. Prerequisites

- Unity `6000.3.16f1` (or a compatible Unity 6 editor).
- Unity Input System `1.19.0`.
- A licensed Devion installation, including Inventory System, Stat System, Triggers, Utilities, and UI Widgets. Install these from the account that owns the license before importing this package.
- The target project's existing Unity Starter Assets — Third Person Controller, if used. The package does not replace movement, camera, or character input.
- For ARES: a backend deployment configured with the Gemini Live relay and a Gemini Developer API key with access to `gemini-3.8-live`.

The package intentionally does not distribute Devion code, examples, UI, or binaries. See [Unity dependencies](./Unity/DEPENDENCIES.md) and [third-party notices](../LICENSE-AND-THIRD-PARTY-NOTICES.md).

## 2. Import the Unity package

1. Back up the target project and confirm its Devion assemblies are installed.
2. Merge the dependencies from `Unity/manifest-dependencies.json` into the existing `Packages/manifest.json`; do not replace the target manifest. Ensure the Input System and Universal Render Pipeline versions are compatible with the target Unity editor.
3. In Unity, select **Assets > Import Package > Custom Package...** and choose `RedEden-Full-System.unitypackage`.
4. Import the listed files and wait for compilation to finish. Resolve any missing Devion assembly references from the licensed installation; do not install Whisper, Ollama, or local language models for ARES.
5. Review the Unity Console before placing assets in a scene.

The package contains the ready-to-use `Hydroponic_Farm_M01.fbx`, agricultural-silo prefab, resource-node prefabs, inventory/crafting databases, and project-owned scripts. It does not include the source test scene or a player prefab.

## 3. Wire gameplay to the existing player

1. Keep the existing Unity Starter Assets Third Person Controller and camera. Do not add a second movement controller.
2. Add exactly one Devion `InventoryManager` to the scene. Assign `RED EDEN Core Inventory Database.asset` as its main database and `RED EDEN Item Database.asset` as a child database. Add any other licensed game databases without duplicating entries.
3. Add Devion's inventory UI and a single `EventSystem` from the licensed installation if the scene does not already have them.
4. Keep the player tag `Player`, enable the Input System, and verify that the existing character input still moves and controls the camera.
5. Place the supplied agricultural silo and resource-node prefabs where needed. Keep their prefab references intact; do not rebuild them from the FBX.
6. Follow the crafting README and database assets to connect the existing inventory and interactable systems.

## 4. Configure ARES in Unity

The Full-System package includes ARES scripts and default resource configuration, but no scene or player setup. On one ARES GameObject:

- Add `ARESController`, assign the `ARESSystemConfiguration`, the `ARESGeminiLiveClient`, and `ARESMicrophoneVoiceInput` as its streaming microphone provider. Assign a game-settings provider only if the project implements `IARESGameSettingsProvider`.
- Add `ARESGeminiLiveClient`, assign the `ARESGeminiLiveConfiguration` asset, an `ARESAudioPlayer` as its audio output provider, and an `ARESFunctionCallHandler`.
- Add `ARESAudioPlayer` with an `AudioSource`; keep the output sample rate at 24 kHz.
- Add `ARESMicrophoneVoiceInput`; it streams 16 kHz PCM and does not initialize an offline recognizer.
- Add `ARESInterfaceController` if connection/listening/speaking status should be shown.
- Optionally add `ARESRAGHandler` and the supplied Mars knowledge assets for local archive context. This is data retrieval only; it does not start a local model.

Set `Backend Base Url` in the Gemini Live configuration asset to the deployed service origin only (for example `https://your-service.onrender.com`). Do not include an API path, query string, or credential. Set the response language in `ARESSystemConfiguration` to `Arabic` or `English`, or provide it through `IARESGameSettingsProvider`. Hold **V** to speak and release it to end the activity. The game bridge exposes read-only player, inventory, crafting, and mission summaries.

The Unity runtime contains no Gemini API key. Do not put backend secrets in a ScriptableObject, Unity scene, build, or source-control file.

## 5. Run the backend locally on Windows

Open PowerShell in `ARES_INTEGRATION_PACKAGE\Backend`:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python -m pip install -r requirements-test.txt
Copy-Item .env.example .env
python -c "import secrets; print(secrets.token_urlsafe(48))"
```

Set the generated random value as `ARES_SESSION_SIGNING_KEY` and a valid Google Gemini Developer API key as `GEMINI_API_KEY` in the local `.env`. Keep `.env` out of Git. Start the server on loopback:

```powershell
python -m uvicorn Source.ares_gemini_server:app --host 127.0.0.1 --port 8000 --ws-max-size 16384
```

Check `http://127.0.0.1:8000/health`, then set the Unity backend URL to `http://localhost:8000`. Health only confirms that the process is running; a successful Gemini Live session also requires a valid key and model access.

Run backend tests from the `Backend` directory:

```powershell
python -m compileall -q Source tests
python -m unittest discover -s tests -v
```

## 6. Deploy the relay to Render

1. In Render, create or update a Blueprint from the GitHub repository, selecting `ARES_INTEGRATION_PACKAGE/render.yaml`.
2. Confirm the service root is `ARES_INTEGRATION_PACKAGE/Backend`, its start command runs one Uvicorn worker, and the health check is `/health`.
3. Add `GEMINI_API_KEY` through Render's secret environment-variable UI. Keep the generated `ARES_SESSION_SIGNING_KEY`; do not add either secret to GitHub or the Unity project.
4. Deploy and wait for a successful build. Copy the service's HTTPS origin into the Unity configuration.
5. Verify the deployed source is the Live relay: `/api/ares/session` issues a temporary session and `/api/ares/live` accepts an authenticated WebSocket. The OpenAPI document lists HTTP routes only, so it will not enumerate the WebSocket route. A health response alone does not prove Live works.

The existing public endpoint may still be serving an older HTTP-only build until it is redeployed and configured. Do not use it as a Gemini Live endpoint until an authenticated WebSocket session succeeds. This repository does not contain the Render dashboard credentials or a Gemini API key.

Render Free is suitable only for testing: services can sleep or restart and interrupt a conversation. The included rate and concurrent-connection limits are process-local; use shared gateway controls, player authentication, quota enforcement, and a paid restart-aware service before a public launch.

## 7. Privacy and troubleshooting

- Microphone audio is streamed to the backend and then to Google Gemini Live; disclose this and obtain any required user consent. This service does not store audio.
- ARES sessions are anonymous, HMAC-signed, and expire after five minutes. They do not prove player identity.
- `503` when creating a session: configure `ARES_SESSION_SIGNING_KEY` in the backend environment.
- WebSocket policy close: the session token may be missing, invalid, or expired; reconnect to request a new one.
- Live setup failure: inspect sanitized backend diagnostics and verify `GEMINI_API_KEY` and model access. Never share keys, bearer tokens, raw audio, or transcripts in logs or support reports.
- Missing microphone or playback: verify OS microphone permission, streaming microphone assignment, active audio output, and a target that supports `ClientWebSocket`. WebGL is not supported.
- Unity compile errors: verify Input System and licensed Devion/Crafting assemblies. Do not add Whisper or local model downloads as a workaround.
