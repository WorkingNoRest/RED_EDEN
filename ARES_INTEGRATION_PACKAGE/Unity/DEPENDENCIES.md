# Unity dependencies

Merge the entries from `manifest-dependencies.json` into the target project's existing `Packages/manifest.json`; do not replace that manifest. The source project uses Unity Input System `1.19.0` and Universal Render Pipeline `17.3.0`.

The target project must already have the RED EDEN Crafting and licensed Devion Inventory System, Stat System, Triggers, Utilities, and UI Widgets assemblies referenced by RED EDEN. Install Devion through the account that owns its license. The Devion source assets do not expose reliable version metadata; use the same compatible licensed release as the source project. This repository does not include or redistribute Devion code, examples, UI, or binaries.

No direct TextMesh Pro references were found in the inspected RED EDEN scripts or prefab/material sources. Install it only if the receiving project or its separately installed UI requires it.

ARES Gemini Live uses Unity/.NET WebSockets and does not require Whisper, Ollama, or downloaded local language models. Google Gemini access is server-side; the Unity package contains no API key.
