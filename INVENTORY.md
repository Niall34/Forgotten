# Backpack inventory

Open this branch with Unity 6000.5.4f1, then open `Assets/Scenes/Forgotten_Menu.unity`.

- Capacity is 8 units. Each generator part uses 3 units.
- Four quick slots appear along the bottom. The backpack shows all carried items.
- Pick up a nearby item with E/P or the on-screen pickup button.
- Open the backpack with B/Tab or the backpack button.
- Select an item and use Drop, or press G to drop the selected item.
- Hold I or the install button beside the generator to install a selected part.
- Existing flashlight control is F or the flashlight button; dark areas require it.

The Forgotten > Preview Backpack (Offline Test) editor menu starts a temporary
offline session with two parts (6/8). It restores the player to the normal spawn
after collecting test items. This is a preview, not a saved game.

After stopping the preview, reopen `Assets/Scenes/Forgotten_Menu.unity` before
pressing Play. Do not save the temporary test scene.

The original Mac implementation passed 35 offline gameplay assertions for capacity,
pickup/drop, installation, and cleanup on death. This branch ports it onto the
team's newer main branch. The integrated runtime scripts compile with Unity's
C# compiler and cached project references (existing warnings only). Rerun
`InventoryValidation.Run` for this integration;
live multiplayer and the updated iPhone build still need testing. All players
must use the same inventory-enabled version because item RPCs have changed.
