# Integration checklist

- [ ] Install Unity Input System, `com.whisper.unity`, RED EDEN Crafting, and Devion Inventory dependencies.
- [ ] Copy all of `Unity/Assets/RED EDEN/ARES/`, preserving every `.meta` file.
- [ ] Assign `ARESController`, `ARESGeminiLiveClient`, streaming `ARESMicrophoneVoiceInput`, `ARESAudioPlayer`, `ARESFunctionCallHandler`, and `ARESInterfaceController`.
- [ ] Set the ARES backend base URL to loopback for local testing or HTTPS for a remote service.
- [ ] Set backend-only `GEMINI_API_KEY` and a generated `ARES_SESSION_SIGNING_KEY` in the host environment.
- [ ] Keep the model fixed to `gemini-3.8-live`; do not add a key/model override to Unity.
- [ ] Start the backend behind HTTPS/WSS for remote use and verify `GET /health`.
- [ ] Verify missing, tampered, and expired bearer tokens are rejected before WebSocket accept.
- [ ] Verify malformed/oversized frames, function-call allowlist, concurrency limits, and rate limits.
- [ ] Run the Python compile check and backend test suite from the installation guide.
- [ ] Run Unity EditMode PCM conversion tests and test press/hold/release, playback interruption, microphone permissions, and reconnect in the Editor/player.
- [ ] Configure shared gateway rate/concurrency limits and game identity verification before public launch.
- [ ] Scan Unity assets, build output, and Git-tracked files for credentials.
- [ ] Confirm the local Ollama/offline path and local vitals alerts still work when Gemini Live is not configured.
