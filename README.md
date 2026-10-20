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

## Pausing, quitting and spectating

- Tap the pause button or press Ctrl (left or right) to pause and resume. Pausing only
  freezes your own screen, so other players in the room keep playing.
- In a multiplayer room the pause menu shows the room code.
- Quit from the pause menu leaves the match. Solo players and guests disconnect and go
  back to the main menu. When the host quits, everyone returns to the lobby.
- Players can be healthy, injured (slower) or dead. A dead player spectates living
  teammates, pressing A or D to switch between them.
- Players who reach the exit leave the map for everyone, and the minimap stops showing them.

## Included settings

- Game and music volume, with live preview and a test tone.
- Low, Medium and High graphics presets; 20, 40 or 60 FPS target.
- Field of view: 60–100 degrees.
- HUD and control scale: 70–110%.
- Look sensitivity: 0.50–2.00x, with optional inverted vertical look.
- Working room text chat, with an on/off preference.
- Separate Audio, Display, Controls and Chat tabs.
- Save & Back, and Back without saving. Ctrl also backs out without saving.

Settings use the lobby's title colour and font, with warm dark panels and matching
selected controls. In a match, tap the speech-bubble button or press T to type a
message, then SEND or Enter. BACK / Escape closes chat. Players in the same room see
your username and message; the last three messages also appear briefly on the HUD.
Messages are limited to 120 characters with a one-second send interval. The chat
window retains up to 24 messages for this session. Saving chat OFF hides and clears
messages and blocks sending/receiving. There is no voice chat or cross-room channel.

- `ForgottenGameSettings.cs`: Defaults, validation and saving/loading preferences.
- `ForgottenGraphicsSettings.cs`: Runtime URP presets and frame-rate target.
- `ForgottenSettingsMenu.cs`: Settings screen, draft values and audio preview.
- `ForgottenSettingsUI.cs`: Shared UI layout and styling.
- `ForgottenTouchJoyStick.cs`: Joystick used by the HUD-size preview.
- `NetworkManager.cs`: Photon connection, solo/host/join rooms, leaving to the lobby or menu.
- `LobbyUI.cs`: Main menu and lobby panels, name prompt, ready and start flow.
- `PauseMenuController.cs`: Local pause menu, Ctrl/button toggle and quit to lobby or menu.
- `PlayerController.cs`: Player movement, camera, torch and escape handling.
- `PlayerHealthStateMachine.cs`: Healthy, injured and dead states, and the speed change.
- `SpectatorController.cs`: Follow camera for teammates after you die.
- `MonsterAI.cs`: Monster behaviour.
- `MonsterProximityEffect.cs`: Dark overlay and heartbeat that build up as the monster gets close.
- `Minimap.cs`: Radar minimap with arrows to teammates, the generator and the exit.
- `MinimapTarget.cs`: Put on the player prefab so the minimap can find players and see who is alive.
- `ChatOpenButton.cs`: Links the HUD chat button to the room chat at runtime.
- `MobileSafeArea.cs`: Safe-area layout for the settings screen.
- `RoomChatSession.cs`: Room messaging, sender validation and bounded session history.
- `RoomChatUI.cs`: Message entry, recent-message HUD and mobile keyboard layout.



## Credit
---Sounds---
Lobby music - OceanFrameMusic 
Start sound - asproductionmusic
Generator sound - freesound_community
Generator fixing sound - freesound_community
Item Pickup - freesound_community
Button click - freesound_community
Flash light on/off - freesound_community
Breathing - freesound_community
Monster sounds - freesound_community
Crouch Breathing - ribhavagrawal
Player hit - u_xjrmmgxfru / universfield
Walking/Running - The Sound Guild

---Assets---
Monster Spawner - SnoozyRat
Torch - Michele Marcelli
Pallets/cones - Universal Assets
Generator - Charles Eudy
Concrete textures - Undoing Planets / Texture Haven / Nobiax/Yughues
Door - Mash Virtual
Wooden Boxes - Hitansh 3D Artist 
Other Decorations - Sickhead games / Rem Stroms
Barrels - Masksim Bugrimox
Axe - Rpfreelance
Key - Aleksn09
Medical supplies - Cookiepopworks.com
Animations - Kevin Iglesias / Mixamo
Hearts - Oarielg
Dumpster - Radu Mitroi
Locker - Vis Games
Cardboard Boxes - Wing13GD
Industrial Cabinets - Adventure Forge
Monster - Tensori
Player Model - Dmitriy Poskrebyshev 

Multiplayer - Photon Engine Pun 2