# RED EDEN crafting

This module extends Devion Games Inventory System and its proximity trigger. It uses the existing `Inventory` window and keyboard interaction key `E`; it does not add a second inventory.

## Setup

1. In Unity, run **Tools > RED EDEN > Crafting > Create Starter Data**.
2. Assign `RED EDEN Core Inventory Database.asset` as the `InventoryManager` main database. Add `RED EDEN Item Database.asset` as a child database; the RED EDEN item database contains game items but not Devion's required configuration settings.
3. Add `RED EDEN Crafting Database.asset` to each `RedEdenCraftingStation` and attach the station to a printer GameObject with a collider.
4. Add `MartianResourceNode` to resource deposits or pickups, assign a resource definition, and set its remaining yield.
5. Add `RedEdenPrinterVisuals` to the printer and assign moving transforms and an indicator renderer for procedural status animation.

Recipes are data assets and specify their station type, ingredients, output, and duration. The starter data includes regolith, raw water ice, mineral/ore resources, solid manufactured outputs, and one resource-node prefab per resource. Raw ice is deliberately not converted directly into water or gas by the fabricator.

## Hydroponic farm

The starter data includes separate inventory items and resource definitions for Potato, Wheat, and Lettuce, alongside the existing Pure Oxygen Cylinder item. They are registered in `RED EDEN Item Database.asset` and `RED EDEN Crafting Database.asset`. The provided model is at `Models/Hydroponic_Farm_M01.fbx`; Unity currently reports this FBX as corrupted and cannot import it, so it must be re-exported before the prefab can be generated.

After Unity imports a valid `Hydroponic_Farm_M01.fbx`, run **Mars > Machines > Create Hydroponic Farm**. This creates `Data/Prefabs/Machines/Hydroponic Farm M01 - Agricultural Silo.prefab`, with the farm trigger, plant visuals, screen mount, digital screen, and all four inventory item references assigned. The creator enables mesh read/write because it reads the screen mesh to position the display. Add the generated prefab to a scene with an active `InventoryManager` that uses `RED EDEN Core Inventory Database.asset` as its main database and `RED EDEN Item Database.asset` as a child database.

The new `HydroponicFarm` replaces the automatic-delivery silo: it produces oxygen and the selected crop into its own bounded storage. Configure cycle times, amounts, storage limits, and starting crop in the Inspector. Collect all stored output by approaching and pressing **E**; optionally enable crop switching and use the configured key (default **F**). The digital screen shows crop/oxygen progress, storage, machine state, messages, and interaction hints. Farm timers, selected crop, operational state, and stored items are included in scene saves under a stable farm ID. Generate a new ID after duplicating a farm.

Material is removed only after a recipe is validated and the job starts. Completed output stays at its own station until collected; if the inventory cannot accept it, collection fails without consuming the pending output. Jobs and selections are station-local. The inventory window name and station type are configurable on each station. The interface fades and disables recipes that cannot currently be started, shows current/required resource quantities, and allows a ready output to be collected with E or its button.

## Persistence

The existing Devion Games save path persists inventory `ItemCollection` data when saving is enabled and invoked. RED EDEN stations and resource nodes also register their stable IDs, crafting jobs, pending outputs, and remaining yields with the existing scene-save JSON. Inventory remains authoritative for inventory quantities; crafting saves do not duplicate inventory entries. Inventory item definitions must be included in the active `InventoryManager` database (the generated RED EDEN item database is provided).

Resource definitions expose purity, contamination, density, thermal resistance, and material strength metadata. Those values describe the definition; per-node condition/quality is not currently stored on collected inventory stacks.

Use **Tools > RED EDEN > Crafting > Validate Crafting Data** to validate resource IDs, recipe references, and generated resource-node prefab assignments. New resource assets can be added to the crafting database's Resources list and selected in recipe ingredient fields.

## Persistent scene state

Crafting station jobs, pending outputs, resource-node yield, and hydroponic farm timers/crop/storage are serialized into the existing Inventory System scene-save JSON. The Inventory Manager remains authoritative for inventory quantities; crafting state never writes a second copy of inventory items. The data records use schema version 1 and stable station/node/farm IDs plus recipe, item, and resource IDs. Missing definitions are reported and unresolved job/output IDs remain saved rather than being silently discarded.

Every placed station and resource node needs a unique stable ID. IDs are generated for new components in the editor; duplicating a prefab instance also duplicates its ID, so use the component context menu to generate a new ID for each placed duplicate. The save operation reports missing or duplicate IDs and aborts before overwriting prior save data.

Resource nodes can optionally respawn. Configure **Supports Respawn** and **Respawn Delay Seconds** on the node; the remaining delay is saved alongside depletion state. Harvested inventory is still restored only by the existing inventory save.

## Play Mode and manual verification

Run **Window > General > Test Runner > PlayMode** and execute `RedEden.Crafting.PlayModeTests`. These tests exercise the existing `InventoryManager.Save/Load` path for independent stations and a resource node, hydroponic farm crop/oxygen timer and output persistence, missing recipe/output recovery, real-time fabrication countdown/completion, and an Input System E-key press that opens the station-owned interface. The E-key test runs without a configured production inventory window; it expects the corresponding missing-window error and does not verify physical trigger-range/collider events or production inventory delivery.

For a detailed Arabic setup and usage catalog, see [Crafting-Guide-AR.pdf](./Documentation/Crafting-Guide-AR.pdf) or its editable [HTML source](./Documentation/Crafting-Guide-AR.html).

For those scene-dependent interactions, configure the Inventory Manager with the RED EDEN item database, add the crafting database and unique IDs to two station instances, and place a resource node. Then run this manual Play Mode procedure:

1. Approach each station separately and press **E**. Confirm that only the approached station's UI opens.
2. With no ingredients, confirm recipes are faded, unavailable, and unselectable. Add the exact listed quantities and confirm displayed available/required counts.
3. Select a recipe, click **Start Print** repeatedly, and confirm only one job starts and ingredients are deducted once. Press **E** repeatedly while busy; confirm the job and inventory do not change.
4. Close and reopen the UI while printing. Confirm progress continues, updates on reopening, and the other printer remains independent.
5. Let the job finish. Confirm the output remains at the correct printer. Fill the inventory before collecting; verify collection fails and the output remains. Free capacity and collect with **E**, then attempt collection again; verify the output was delivered exactly once.
6. Harvest a resource node until depleted. Save, reload the scene through the Inventory System, and verify node yield matches the save and inventory quantities were not duplicated. Repeat with respawn enabled and test both before and after the saved respawn timer expires.
7. Save a printer while it is printing and while it has a pending output. Reload the scene and verify the remaining time or pending output is restored without re-consuming ingredients.
8. Temporarily remove a recipe/resource/item definition in a test copy of the scene and verify load reports the missing ID while retaining recoverable save state. Restore the reference before continuing.

GUID history limitations and the current asset mapping are recorded in [GUID-MAPPING.md](./GUID-MAPPING.md). The previous GUID values are unavailable in this workspace, so references external to the current project that used those old GUIDs cannot be automatically searched.
