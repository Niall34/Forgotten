# Forgotten

Forgotten is a mobile horror game for solo play or a team of up to four players.
Explore the map, find generator parts and repair the equipment to escape the creature.

## Open and run in Unity

1. Clone or download the **main** branch of this repository.
2. In Unity Hub, choose **Add project from disk** and select the folder containing
   `Assets`, `Packages` and `ProjectSettings`.
3. Open with **Unity 6000.5.4f1** and wait for the initial import to finish.
4. Open `Assets/Scenes/Forgotten_Menu.unity` and press **Play**.
5. Enter a username. Open **Settings**, or choose Play / Host / Join.

Use the on-screen joystick and drag-to-look controls with the mouse in the Editor.
The current solo option uses a one-player Photon room, so solo and multiplayer both
require an internet connection and a working Photon PUN App ID. Ask the team's Photon
project owner if a connection fails; do not replace the team's configuration blindly.

`Forgotten.unity` is an older menu asset, not the current starting scene.
The enabled build scenes are Forgotten_Menu followed by Forgotten_Map.

## Combined branches

Main includes Map, Player, Lobby, Generator and Settings. Merge commits retain the
original branch histories and contributors. The original branches have not been deleted.
Integration fixes connect the settings and generator systems to the actual network
player and move the health/spectator scripts into Unity's Assets folder.

## Included settings

- Game and music volume, with live preview and a test tone.
- Low, Medium and High graphics presets; 30 or 60 FPS target.
- Field of view: 60–100 degrees.
- HUD and control scale: 70–110%.
- Look sensitivity: 0.50–2.00x, with optional inverted vertical look.
- Working room text chat, with an on/off preference.
- Separate Audio, Display, Controls and Chat tabs.
- Save & Back, Back without saving, and confirmed Reset.

Settings use the lobby's title colour and font, with warm dark panels and matching
selected controls. In a match, tap the speech-bubble button or press T to type a
message, then SEND or Enter. BACK / Escape closes chat. Players in the same room see
your username and message; the last three messages also appear briefly on the HUD.
Messages are limited to 120 characters with a one-second send interval. The chat
window retains up to 24 messages for this session. Saving chat OFF hides and clears
messages and blocks sending/receiving. There is no voice chat or cross-room channel.

| File | Responsibility |
| --- | --- |
| `ForgottenGameSettings.cs` | Defaults, validation and saving/loading preferences. |
| `ForgottenGraphicsSettings.cs` | Runtime URP presets and frame-rate target. |
| `ForgottenSettingsMenu.cs` | Settings screen, draft values and audio preview. |
| `ForgottenSettingsUI.cs` | Shared UI layout and styling. |
| `ForgottenTouchJoyStick.cs` | Joystick used by the HUD-size preview. |
| `MobileSafeArea.cs` | Safe-area layout for the settings screen. |
| `RoomChatSession.cs` | Room messaging, sender validation and bounded session history. |
| `RoomChatUI.cs` | Message entry, recent-message HUD and mobile keyboard layout. |

## Music and remaining work

The merged branches do not contain the lobby music file. To enable it, import a track
you have permission to use at
`Assets/Resources/Forgotten/Audio/whispering_shadows.ogg`. Unity loads it automatically.
A YouTube link alone is not an audio asset in the project.

The generator opens its door after five parts. Since this repository does not yet
contain a WinScreen scene, reaching the exit shows an in-map escape message.
Health currently supports healthy, injured and dead states; it is not the previously
proposed 60-second revive system. Spectator target switching currently uses A/D keys.
Phone controls and network gameplay race conditions still need device/team playtesting.
Room chat was checked bidirectionally with two separate Photon client processes;
see [chat validation](Docs/ChatValidation.md) for coverage and limitations.

See [integration validation](Docs/IntegrationValidation.md) for the checks actually run
and [project context](Docs/AI/UnityProjectContext.md) for architecture and merge decisions.
iPhone builds additionally require a Mac, iOS Build Support, Xcode and device signing.

The settings module and this integration were developed with AI assistance. Feature
owners should review, understand and be able to demonstrate their contributions.
