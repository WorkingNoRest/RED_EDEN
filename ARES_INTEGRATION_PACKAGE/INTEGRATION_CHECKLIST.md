# Integration checklist

- [ ] Install Unity Input System, `com.whisper.unity`, RED EDEN Crafting, and Devion Inventory dependencies.
- [ ] Copy all of `Unity/Assets/RED EDEN/ARES/`, preserving every `.meta` file.
- [ ] Create and assign the ARES Gemini configuration asset with a backend base URL.
- [ ] Set backend-only `GEMINI_API_KEY` and a generated `ARES_SESSION_SIGNING_KEY` in the host environment.
- [ ] Start the backend behind HTTPS for non-loopback use; do not expose development Uvicorn directly.
- [ ] Confirm `GET /health` responds and Unity can request a session and chat successfully.
- [ ] Verify missing, tampered, and expired bearer tokens are rejected.
- [ ] Verify invalid/oversized request fields return 4xx responses and limits return 429.
- [ ] Configure shared gateway rate limits and player identity verification before public launch.
- [ ] Scan Unity assets, build output, and Git-tracked files for credentials.
- [ ] Confirm ARES still functions with the existing local Ollama bridge when Gemini is not assigned.
- [ ] Confirm the game-state bridge remains read-only and local vitals alerts work offline.
