# Separate-process chat test helper

This is an Editor-only Photon Realtime peer, not game functionality.

1. Next to the game project, create `work/ChatValidationPeer-2026-09-30`.
   For example, game `D:/AUT ESCAP/ForgottenCombined` uses
   `D:/AUT ESCAP/work/ChatValidationPeer-2026-09-30`.
2. In that separate Unity project, copy the game's `ProjectSettings/ProjectVersion.txt`.
3. Copy the game's `Assets/Photon/PhotonRealtime` and `Assets/Photon/PhotonLibs`
   (including metadata). Omit PhotonRealtime's Demos folder.
4. Put this folder's `ChatPeer.cs` in the helper's `Assets/Editor`, and its
   `manifest.json` in the helper's `Packages` folder. Import with the same Unity version.
5. In the game Editor, stop Play Mode, open Forgotten_Menu, then choose
   Forgotten / Validate Chat / Online Room Delivery. The harness creates a private
   room and writes the helper's `peer.json`. Treat that file as local configuration:
   it includes the configured Photon App ID and must not be committed.
6. Launch a **second Unity process** with `-batchmode -nographics -projectPath`
   pointing at the helper and `-executeMethod ChatPeer.Run`. Do not pass `-quit`:
   the helper exits itself when it receives the game's reply or times out.
7. Both the game log and helper `result.txt` must report PASS. The game exits Play
   Mode and restores saved preferences. A helper-only PASS is not enough.

The helper uses a distinct test app-version channel and synthetic usernames. It
does not join public rooms or change the game's PhotonServerSettings asset.
