# Unity dependencies

Merge the entries from `manifest-dependencies.json` into the existing `Packages/manifest.json`; do not replace the target project's manifest. Install the RED EDEN Crafting and Devion Inventory System assemblies used by `ARESGameBridge.cs` from the target project's licensed sources. Their `.asmdef` references are included in the ARES assembly definition.

The package intentionally does not redistribute Unity, Google, Whisper, or Devion third-party source/binaries beyond the project-owned ARES code.
