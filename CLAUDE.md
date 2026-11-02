# Sumo Game (Unity)

Local multiplayer mobile sumo game. Up to 4 players share one screen and shove each other out of a shrinking ring.

## Project facts

- Unity version: **6000.3.9f1**. Open the project in this version only. Do not upgrade it by accident.
- Main scene: `Assets/Scenes/3 Round Game Start.unity` (the only scene enabled in the build).
- Scripts: `Assets/Scripts/`. All game scripts compile into one assembly, `Assembly-CSharp`.
- Prefabs: `Assets/Prefabs/`.
- Main script: `SumoGameManager.cs` (singleton: menus, rounds, scoring, match end).
- Pause: `PauseManager.cs` sits on `Canvas / PauseButton`, outside the gameplay panel.

## Unity C# rules

- Private fields use `camelCase`. Public methods and properties use `PascalCase`.
- Use `[SerializeField] private` for Inspector fields, not `public`, unless another script needs the field.
- Never rename a `[SerializeField]` field without `[FormerlySerializedAs]`. Renaming loses the value saved in the scene.
- Check Inspector references for null before using them. This project uses `if (x != null)`.
- Do not use `GameObject.Find` or look objects up by name. Keep direct references.
- Use `TryGetComponent` or cache the result. Do not call `GetComponent` every frame in `Update`.
- Keep gameplay rules in `SumoGameManager`. Do not spread match logic across UI scripts.
- Use `FindObjectsByType`, `Rigidbody2D.linearVelocity` and other current Unity 6 APIs, not older ones.

## Time and pausing

- `Time.timeScale` is changed by pause, `JuiceManager` hit-stop and round start. Set it back to `1f` on every path that changes it.
- `WaitForSeconds` stops while `timeScale` is 0. Use `WaitForSecondsRealtime` for anything that must run during a pause.
- A match can end in 4 places. Any change to match end must cover all of them: two team wins and two versus wins.

## Files and assets

- Every file in `Assets/` needs its `.meta` file. Move or rename both together, or do it inside Unity.
- Do not hand-edit `.unity` or `.prefab` files unless the change is small and checked in Unity afterwards.
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/` or `UserSettings/`.
- Close Unity or save the scene before switching branches.

## Before saying something is done

1. Unity compiles with no errors in the Console.
2. Test in Play mode. A code change that looks right is not proof.
3. Test both paths: the normal path, and the failure path (for example, pressing pause after a win).
4. Tests of 2-player Versus do not cover Team mode. Say which modes were tested.

## Git

- Work on a branch, not on `main`.
- Do not push, publish or open a pull request unless asked.
