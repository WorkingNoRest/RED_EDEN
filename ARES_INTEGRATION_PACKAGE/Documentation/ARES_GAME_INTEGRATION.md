# ARES game integration

## Existing systems reused

- `InventoryManager` from the Devion Inventory System.
- `RedEdenCraftingStation` and the crafting database.
- `ARESPlayerVitals` and `ARESVitalsMonitor` for survival values.
- `ARESSystemConfiguration` for local game language and fallback text.

## Read-only bridge behavior

The ARES game bridge inspects active containers and station data without mutating the player inventory or crafting data. This matches the requirement that ARES may answer questions but not directly change inventory, resources, or health values.

## Example questions

- How much iron do I have?
- What do I need to craft an Hydroponic Farm?
- What is my current oxygen level?
- What is my current mission?
- How can I obtain water?

Gemini Live can call only the backend-declared read-only functions for current vitals, inventory, crafting, and mission status. `ARESFunctionCallHandler` validates each name again in Unity and returns a bounded summary; unsupported actions and mutations are not exposed. The mission function currently reports that mission data is not available. Gemini's input transcription can optionally select supporting NASA archive records; Unity sends only bounded archive evidence as context before ending the Live turn.

ARES does not start local Ollama/Whisper services or download language models. Legacy-named scene components are inert compatibility stubs; the local-model provisioner, recognizer, and generation implementations are removed.
