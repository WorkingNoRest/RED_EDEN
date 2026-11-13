# RED EDEN installation checklist

## Before import

- [ ] Use Unity 6 compatible with `6000.3.16f1`.
- [ ] Install the Unity Input System and Universal Render Pipeline dependencies from `ARES_INTEGRATION_PACKAGE/Unity/manifest-dependencies.json`.
- [ ] Install Devion Inventory System, Stat System, Triggers, Utilities, and UI Widgets from the licensed account. Match the compatible source-project version; the imported Devion assets do not expose version metadata.
- [ ] Back up the target project and confirm its Third Person Controller and camera are already working.

## Import and configure

- [ ] Merge `ARES_INTEGRATION_PACKAGE/Unity/manifest-dependencies.json` into the existing package manifest.
- [ ] Import `packages/RedEden-Full-System.unitypackage`.
- [ ] Wait for Unity compilation; resolve missing Devion references from the licensed installation.
- [ ] Configure exactly one `InventoryManager`; assign RED EDEN Core Inventory Database as main and RED EDEN Item Database as a child.
- [ ] Add Devion inventory UI only if the scene does not already have it.
- [ ] Place the supplied agricultural silo/resource prefabs; do not rebuild them from models.
- [ ] Add ARES components and set the service origin in the Gemini Live configuration asset if ARES is used.
- [ ] Confirm ARES compatibility components are inert and no Ollama process, Whisper model, or local model files are installed.
- [ ] Verify the existing player's movement, camera, interaction key, inventory, and crafting behavior.

## Release checks

- [x] Unity `6000.3.16f1` compilation and ARES/Crafting EditMode tests (15 passed).
- [x] Import the package into a clean Unity project with licensed Devion dependencies; verify the silo/model, databases, and compilation.
- [x] Run backend compile and unit tests (27 passed).
- [ ] Confirm the deployed Live WebSocket with an authorized Gemini API key; `/health` alone is insufficient.
- [x] Keep all server secrets out of Unity and Git.
