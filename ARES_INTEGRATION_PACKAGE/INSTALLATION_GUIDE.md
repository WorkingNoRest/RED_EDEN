# ARES installation guide

## 1. Prerequisites

- Unity Input System is installed and enabled under **Edit > Project Settings > Player > Active Input Handling**.
- The target project includes the RED EDEN Crafting and Devion Inventory System assemblies used by ARES.
- Add `com.whisper.unity` in **Window > Package Manager > + > Add package from git URL**:

  ```text
  https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity
  ```

## 2. Copy the Unity files

1. Back up `Assets/RED EDEN/ARES` if it already exists.
2. Copy the `Unity/Assets/RED EDEN/ARES` folder and its adjacent `Unity/Assets/RED EDEN/ARES.meta` file into the project's `Assets/RED EDEN/` folder. Preserve the folder structure and every nested `.meta` file. Do not copy only the `.cs` files.
3. Wait for Unity to finish importing and compiling. Resolve missing package/assembly references before continuing.
4. In the Project window, choose **Create > RED EDEN > ARES > Gemini Live Configuration**.
5. Set **Backend Base Url** to the backend origin, for example `http://localhost:8000` during local testing. Use `https://` for any non-loopback host.
6. On the scene's existing ARES controller, assign the new asset to **Gemini Live Client**. Keep the local bridge assigned if it is used as the offline fallback.
7. Keep the NASA archive/RAG configuration assigned; ARES does not send a Gemini request when the local archive has no supporting records.

## 3. Configure and run the backend (Windows PowerShell)

From the package's `Backend` directory (open that folder in PowerShell first):

```powershell
py -3 -m venv .venv
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
GEMINI_MODEL=gemini-3.8-live
ARES_RATE_LIMIT_STORAGE_URI=memory://
```

Never paste either key into Unity, a `.asset`, a build, a command-line argument, or Git. `.env` is ignored by Git. Run the development service on loopback:

```powershell
uvicorn Source.ares_gemini_server:app --host 127.0.0.1 --port 8000
```

Check `http://127.0.0.1:8000/health`, then run the Unity scene. For a remote service, use a domain with HTTPS and a maintained reverse proxy/API gateway; do not bind Uvicorn directly to a public interface. Configure gateway request-size and rate limits. The in-memory limiter is for a single development worker only.

## 4. Authentication behavior and limits

The Unity client calls `POST /api/ares/session` to obtain a server-signed bearer token, then sends it to `POST /api/ares/chat`. Tokens expire in five minutes. The service rejects missing, tampered, or expired tokens. A session is anonymous: because a distributed Unity client cannot keep a static secret, anyone who can reach the public session endpoint can request a session. The included per-IP rate limits and bounded request fields are abuse controls, not player identity verification. Integrate your game's account/provider ID token verification before enabling broad public access or per-player billing.

The default limits are five session creations and twelve chat calls per IP per minute. Shared/public deployments must use gateway limits or configure a shared SlowAPI storage backend; per-process memory limits do not coordinate across workers.

## 5. Production checklist

- [ ] Use HTTPS outside loopback and verify the certificate.
- [ ] Store Gemini and signing keys in the host's secret manager/environment, not in this package.
- [ ] Use platform identity verification if only authenticated players may use ARES.
- [ ] Apply shared rate limits, request-body limits, and monitoring at the gateway.
- [ ] Keep Uvicorn private behind the proxy; restrict inbound network access.
- [ ] Rotate the signing key if it is exposed; existing sessions will become invalid.
- [ ] Confirm no secret is present in Unity assets, build output, or Git history.

## Troubleshooting

- `503 Backend session authentication is not configured`: set `ARES_SESSION_SIGNING_KEY` to a random value of at least 32 bytes and restart.
- `401 A valid session token is required`: the client/backend versions are mismatched, or the token expired; update both package sides and retry.
- `413 Request body is too large` or `422`: reduce prompt or game context size.
- `429`: wait for the per-IP limit window to reset or tune limits at the gateway.
- `502 Gemini request failed`: inspect backend logs for the upstream failure; the client-facing response intentionally avoids returning exception details.
