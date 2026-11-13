# Release and integration checklist

## Unity package

- [ ] Install the required Devion assets from the account that owns their license.
- [ ] Merge `Unity/manifest-dependencies.json` into the target project's existing package manifest.
- [ ] Import `packages/RedEden-Full-System.unitypackage`; do not import the superseded Core/ARES packages alongside it.
- [ ] Confirm the FBX, agricultural silo prefab, resource-node prefabs, and all three RED EDEN databases are present.
- [ ] Confirm no test/original game scene, player, Devion source, local inference implementation, or model configuration was exported.
- [ ] Keep Unity Starter Assets Third Person movement and camera; configure one InventoryManager and its licensed UI.
- [ ] Configure the ARES client, streaming microphone, audio output, function-call handler, and HTTPS backend origin.
- [ ] Run Unity compilation, PCM EditMode tests, and an import check in a clean project.

## Backend

- [ ] Run `python -m compileall -q Source tests`.
- [ ] Run `python -m unittest discover -s tests -v`.
- [ ] Keep Gemini and session-signing secrets only in local ignored `.env` or the host secret manager.
- [ ] Deploy `ARES_INTEGRATION_PACKAGE/render.yaml` and verify `/health`, session issuance, and an authenticated `/api/ares/live` WebSocket.
- [ ] Do not treat HTTP OpenAPI output or a healthy process as proof of a working Gemini Live session.
- [ ] Confirm actual Gemini account access, Arabic voice quality, microphone permissions, and output playback with an authorized key/device.
- [ ] Add player identity, spend controls, and shared gateway limits before public launch.
