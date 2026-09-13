# Forgotten project context

## Scope and baseline

Repository: Niall34/Forgotten. Integration began from main c2f1909 on 13 September 2026.
Map 09bc8c0 and Player 1fb3a36 were already ancestors of main. Lobby 6ff71ca,
Generator 3cace16 and Settings a5a7fd8 were merged with history preserved.
Settings was a source-only export with an unrelated history.

## Runtime and packages

- Unity 6000.5.4f1; URP 17.5.0; uGUI 2.5.0 with TextMeshPro.
- Legacy Input Manager and StandaloneInputModule. No Input System dependency needed.
- Photon PUN imported under Assets/Photon; NetworkManager owns room/lobby callbacks.
- Navigation uses NavMeshAgent and the Navigation package 2.0.14.
- First-party scripts compile in Assembly-CSharp. Editor checks are under Assets/Editor.
- Landscape mobile orientation is configured in ProjectSettings.

## Startup and ownership

Enabled scenes: Forgotten_Menu first, then Forgotten_Map. Forgotten.unity is an older
menu containing script references no longer present in this repository; it is preserved
as a source asset but is not the startup scene.

LobbyUI owns username and room panels and initializes ForgottenSettingsMenu.
NetworkManager persists across scenes and loads the map through Photon.
Solo currently uses a one-player Photon cloud room and therefore needs a connection.
GameplaySession spawns Resources/Player/Player and the monster prefab. Only the master
client drives monster AI; local players own camera, touch controls, inventory UI and input.

Settings use a draft/saved snapshot and PlayerPrefs. Audio/graphics apply on startup;
FOV, HUD scale and look preferences are read by the spawned player. Graphics presets
clone the URP asset at runtime rather than modifying source render assets.
The chat switch stores a preference, not a complete team-chat implementation.

GeneratorAssembly coordinates five pieces, its exit door and WinTrigger. The Generator
branch's scene-only inventory prototype is not the actual network player. The spawned
PlayerController now attaches inventory; ownership guards prevent prototype/remote UI.

The Player branch's health/spectator scripts originally lived outside Assets and had
syntax/import errors plus a missing state enum. They now compile under Assets/Scripts,
and monster damage reaches the health component through the existing Photon player.
This is Healthy/Injured/Dead behaviour, not a 60-second revive system.

## Integration decisions

- Retain main's evolved project/package configuration over Lobby's older empty template.
- Combine added generator scene objects with main's newer map objects; retain main's
  changed prop position where both branches edited it.
- Preserve main's monster patrol/animation code and add Generator's damage behaviour.
- Keep main's Scripts folder meta GUID; retain Settings script GUIDs.
- No packages upgraded, branches deleted, or history rewritten.
- Missing optional lobby music is not supplied by any merged branch. The expected
  Resources path is Forgotten/Audio/whispering_shadows; only add audio licensed for use.
- The missing WinScreen scene falls back to an in-map escape message, not an invalid load.

## Validation entry point and remaining coverage

CombinedProjectValidation.Run performs scene/prefab missing-script checks, settings
bounds checks, and an offline Play Mode lobby-to-map smoke test without saving scenes
or settings. See Docs/IntegrationValidation.md for the actual result of this run.

An offline room is not evidence of two-client replication, cloud connection success,
mobile touch behaviour, iPhone signing/build success, or performance on hardware.
Visual asset quality, map-wide navigation and race conditions on simultaneous pickup
need team playtesting beyond this merge.
