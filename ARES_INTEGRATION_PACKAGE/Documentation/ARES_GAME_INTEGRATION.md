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

The game bridge reports the actual scene status, and the backend asks Gemini to answer from that snapshot while remaining honest about missing data.
