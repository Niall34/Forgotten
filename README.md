# Forgotten settings

Forgotten is a mobile horror game where you as a solo or in a team, upwards of 4 players must traverse a unkown space to repair research equipement and leave before a unkown creatures finds you.

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







This branch records the settings-only export. Earlier implementation history is
linked above in the original repository. The module was developed with AI assistance; its feature owner should be
able to explain and demonstrate the code and the Unity systems it uses.
