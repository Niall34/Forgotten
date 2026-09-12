# Forgotten settings

This branch contains the settings module exported from the open Forgotten Unity
project. It does not contain the lobby scene, map, characters, monsters, audio files,
or multiplayer implementation. This export is published to the `Settings` branch in
the team's [Niall34/Forgotten repository](https://github.com/Niall34/Forgotten).
The team's other branches are unchanged.

This is a source module to import into an existing Unity project, not a complete
project that can be opened directly in Unity Hub.

## Included settings

- Game volume and music volume, with live preview and a test tone.
- Low, Medium and High graphics presets.
- 30 or 60 FPS target.
- Field of view: 60–100 degrees.
- HUD and control scale: 70–110%.
- Look sensitivity: 0.50–2.00x, with optional inverted vertical look.
- Team text chat preference (on/off).
- Separate Audio, Display, Controls and Chat tabs.
- Save & Back, Back without saving, and confirmed Reset.

## Files

| File | Responsibility |
| --- | --- |
| `ForgottenGameSettings.cs` | Defaults, preference validation, saving and loading. |
| `ForgottenGraphicsSettings.cs` | Runtime URP graphics presets and frame-rate target. |
| `ForgottenSettingsMenu.cs` | Settings screen, draft values and audio preview. |
| `ForgottenSettingsUI.cs` | Shared UI layout and styling. |
| `ForgottenTouchJoyStick.cs` | Joystick used by the HUD-size preview. |
| `MobileSafeArea.cs` | Keeps the settings controls within the device safe area. |

Unity `.meta` files are included to preserve asset identities.

## Import and connect

The source project uses Unity **6000.5.4f1**, URP **17.5.0**, Input System **1.19.0**
and Unity UI **2.5.0**, including TextMesh Pro.

1. Copy the scripts and their `.meta` files into the target project's `Assets/Scripts`
   folder. If these classes already exist there, update those files instead of adding
   a second copy.
2. Make sure the project has URP, Unity UI/TextMesh Pro and the Input System installed.
   Import TMP Essential Resources if the target project does not already have them.
3. Use an EventSystem with `InputSystemUIInputModule` and a screen-space Canvas for
   the existing menu panel.
4. Attach `ForgottenSettingsMenu` to a persistent menu controller outside the panel
   that is hidden while Settings is open. Call its public initializer once:

   ```csharp
   settingsMenu.Initialize(mainMenuPanel);
   ```

   The module creates a Settings button on that panel and its own settings overlay.
   The entry button currently uses Forgotten's menu coordinates; adapt
   `CreateSettingsButton` if the host menu has a different layout.

5. When entering gameplay, call `HideImmediately()` and `StopLobbyMusic()` on the
   settings component. Read `ForgottenGameSettings.Load()` in the gameplay controller
   to apply FOV, HUD scale, sensitivity and inverted look. Enable or disable the
   separate multiplayer chat system using the saved `GameChatEnabled` preference.

The chat toggle saves a preference; this branch does not implement network chat.
The camera and gameplay controller are intentionally outside this export.

## Optional music

The existing module looks for a clip at the extension-free Resources path
`Forgotten/Audio/whispering_shadows`. Audio files and the lobby itself are excluded.
Without a clip, Settings still works and Unity logs a missing-music warning.
Only add music that the team has permission to use.

## Verification and history

The six scripts match the versions previously published in
[commit d8c1a9a](https://github.com/FDOTwrldd/Forgotten/commit/d8c1a9ae21bb962b6203249bc8b7507af1b81d85).
Those settings passed Unity compilation and isolated Play Mode checks for saving,
discarding, reset confirmation, tab navigation and graphics choices. This export
was checked against those source files; no new iPhone build was performed.

This branch records the settings-only export. Earlier implementation history is
linked above in the original repository. The module was developed with AI assistance; its feature owner should be
able to explain and demonstrate the code and the Unity systems it uses.
