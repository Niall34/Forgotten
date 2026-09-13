# Combined-branch validation

Date: 13 September 2026. Unity: 6000.5.4f1 on Windows.

**Status: ready to open and test in Unity, with limitations. Not a mobile release validation.**

## Git integration

Baseline main: c2f1909. Included branch tips: Generator 3cace16, Lobby 6ff71ca,
Map 09bc8c0, Player 1fb3a36, Settings a5a7fd8. Every tip was verified as an ancestor
of the integration branch after fetching the remote again. Original branches and
commit authorship are retained. No force-push or fabricated development history.

## Checks completed

The editor-only `CombinedProjectValidation.Run` check compiled and ran in Unity
batch mode. The final log reported the following results:

| Check | Result |
| --- | --- |
| C# compilation, including imported Player health/spectator scripts | Passed |
| Missing-script scan of both enabled scenes and player/monster prefabs | Passed |
| Player prefab has its touch-control prefab assigned | Passed |
| Settings validation clamps invalid values | Passed |
| Lobby starts and settings open | Passed |
| Back without saving returns to the lobby | Passed |
| Offline map spawns local player and monster | Passed |
| Generator and five pieces exist | Passed |
| Network player receives generator inventory | Passed |
| Saved FOV applies to the player camera | Passed |
| Exactly one active audio listener after player spawning | Passed |
| Healthy-to-injured health transition | Passed |
| Generator installation RPCs complete the generator in an offline room | Passed |
| Completion opens the door; exit registers escape | Passed |
| Game-code runtime errors during the checked flow | None |

The generator check invokes interaction methods directly; it does not prove
physical reachability of every piece or mouse/touch hold behaviour throughout the map.
No scene or settings preference was saved by the smoke test. No NUnit suite or
player build was run. Validation logs are outside the repository at
`D:/AUT ESCAP/work/CombinedValidation/`; the final run is `ValidationPassed.log`.

## Failures found and retained evidence

- Integration inspection found malformed health code and a missing state definition;
  Unity's first compilation also caught missing spectator imports after moving those
  scripts under Assets. These were corrected.
- Runtime checks identified multiple enabled scene cameras/listeners. The spare
  preview camera is inactive and the local player takes camera/audio ownership.
- Generator completion originally targeted an absent WinScreen and the exit lacked
  a PhotonView. The merged exit has a scene PhotonView and an in-map escape fallback.
- An early escape assertion incorrectly inspected the active-player registry after
  escaping had disabled the player. It now checks the actual scene player component.
- Unity's **editor search indexing** threw an `ArgumentOutOfRangeException` in
  `UnityEditor.Search.SearchDatabase.EnumerateAll` / `SearchInit.IndexationOnStartup`
  during each batch Play Mode entry. This remains unresolved. The exception is kept
  in the logs and counted separately from game-code errors (final count: 1); the
  checks do not clear or suppress the Unity Console exception.
- Existing vendor/package deprecation and serializer warnings remain. Optional
  lobby music is absent from the merged branches and generates a warning.

## Not verified

Two-peer Photon/cloud sessions, simultaneous pickup arbitration, reconnect/late join,
spectating another device, phone touch gestures, audio listening, visual layout and
performance on hardware, iOS/IL2CPP builds and signing. No claim of a complete revive
or game-chat system is made. These still need team testing and development.

## Reproduce

Open the project with Unity 6000.5.4f1. For normal play, select
**Forgotten > Open Game Menu**, then press Play. The current solo button needs a
Photon connection. For the automated offline check, launch Unity with
`-batchmode -nographics -projectPath "<project folder>" -executeMethod CombinedProjectValidation.Run -logFile "<log file>"`.
Do not add `-quit`: the check exits after its Play Mode steps finish.
