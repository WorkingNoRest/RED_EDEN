# Crafting asset GUID audit

The current crafting data references resolve in Unity. The six resource assets and four recipe assets listed below are referenced by the crafting database and recipe/prefab data; no other references to these current GUIDs were found under `Assets`.

The prior resource and recipe `.meta` files are not present in this workspace, and `E:\Mars` has no Git metadata or recoverable commit history. The earlier session notes confirm those files were deleted and regenerated but do not retain their old GUID values. Therefore the old GUIDs cannot be reported or safely restored.

| Asset | Previous GUID | Current GUID |
|---|---|---|
| `Resources/Regolith.asset` | Unrecoverable | `bf724cff102e6bf48832f30e5a5bce00` |
| `Resources/WaterIce.asset` | Unrecoverable | `ca106761218916d44b4ed0b169932672` |
| `Resources/FerricOre.asset` | Unrecoverable | `4d2701cfd140e5e469bc95e572e0255c` |
| `Resources/Silica.asset` | Unrecoverable | `603a2741b0df92b45a651e0cb8c2002b` |
| `Resources/CarbonaceousOre.asset` | Unrecoverable | `33d8c77d7523f5b4da8364e68b0c8337` |
| `Resources/CopperOre.asset` | Unrecoverable | `7a72c329fdbe1614b9848be39cc66e38` |
| `Recipes/CompactedRegolithBrick.asset` | Unrecoverable | `b203b28b267be654b9ee1522b1189a85` |
| `Recipes/MetalPlate.asset` | Unrecoverable | `afecdb08c7263ab4abd0ccb59cc842e4` |
| `Recipes/SilicateGlass.asset` | Unrecoverable | `4da465a64b8906e46bbbdd2a8ddc0ddb` |
| `Recipes/ElectricalCable.asset` | Unrecoverable | `f0931d6146f5db0459b9202380093b5c` |

The current crafting database, recipe references, and resource-node prefab references point to the current assets. Unity Edit Mode tests validate that GUID references in the crafting data folder resolve. Since historical GUIDs are unknown, external assets that referenced the deleted GUIDs cannot be identified by GUID search and may require manual inspection if they existed outside this project.
