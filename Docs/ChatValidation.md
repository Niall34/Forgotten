# Room chat and settings checks — 30 September 2026

Environment: Windows Editor, Unity 6000.5.4f1, Forgotten_Menu → Forgotten_Map.
No scenes, prefabs, networking credentials or package versions were changed.
Pre-existing local changes to Photon/project settings were preserved.

## Results

- Scripts compiled in the open Editor.
- Offline integration check passed with zero first-party runtime errors: settings
  opens, Team Chat tab opens, actual input/Send displays `ChatHost: Hello team`.
- Empty messages, rapid repeated sends and unknown sender IDs were rejected.
- Saving chat OFF cleared history, closed the composer and prevented sending.
- Reopening/closing released the input-capture flag; sending outside a room failed.
- A second **separate Unity process**, using Photon Realtime, joined a private test
  room. Its `<b>Peer hello</b>` message appeared literally in the gameplay chat.
  The game replied `Host reply`; the second process confirmed receipt.
- Online validation finished PASS with zero first-party runtime errors.
- All four settings tabs were visually reviewed in Play Mode. The existing touch
  chat icon was checked; manual typing and Enter displayed the message with the
  username. BACK returned to gameplay.
- Test preference values and the original username preference were restored after
  exiting Play Mode. Test messages are synthetic and scoped to a private room.

## Repeat local checks

Open Forgotten_Menu with Play Mode stopped. Use **Forgotten → Validate Chat →
Offline UI and Settings**. This enters Play Mode, runs the checks, exits Play Mode
and restores the original preferences. Preview Settings / Preview In-Game Chat
leave Play Mode running for manual inspection; stop Play Mode to restore preferences.

The Online Room Delivery command is a developer harness requiring a separate
Photon Realtime test process. The local helper used for this run is outside the
game repository under `../work/ChatValidationPeer-2026-09-30`; it is not shipped in
the game. Its reproducible setup/source is under `Tools/ChatValidationPeer`.
For normal team testing, run two game copies, join the same room, start the match,
tap the speech bubble and exchange messages in both directions. Test OFF, Save,
leave/rejoin and disconnect too. Both game copies need the new scripts.

## Limits

This verifies room-message delivery, not all multiplayer gameplay. No iPhone build,
physical-device keyboard, safe-area, touch typing or performance test was performed.
The UI accommodates the reported mobile keyboard area, but that still needs an
iPhone playtest. Chat is session-only text: no voice, cross-room history, moderation
service, message persistence or offline delivery queue. Existing Unity/asset warnings
and missing optional lobby music are outside this change.
