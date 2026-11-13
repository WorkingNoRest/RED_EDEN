# ARES Gemini backend setup

The included implementation is a Unity-to-FastAPI text-generation proxy. Despite the historical "Gemini Live" class names, it does not implement Gemini's native real-time audio session protocol. It keeps the Gemini API key on the server and uses a five-minute anonymous session token between Unity and the backend.

## Model configuration

Set `GEMINI_MODEL` in the backend environment to a model available to the Google project that owns `GEMINI_API_KEY` and supports the `generate_content` API used by this backend. The example uses `gemini-3.8-live`; replace it if that model is not enabled for your account.

## Required environment

- `GEMINI_API_KEY`: secret Google API credential; server only.
- `ARES_SESSION_SIGNING_KEY`: random value with at least 32 bytes; server only.
- `GEMINI_MODEL`: backend-selected model; Unity cannot override it.
- `ARES_RATE_LIMIT_STORAGE_URI`: defaults to process-local `memory://`. Use a shared backend or gateway limits in multi-worker deployments.

See [the installation guide](../INSTALLATION_GUIDE.md) for executable setup steps and deployment requirements.
