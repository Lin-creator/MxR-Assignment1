# MxR Assignment 1 – First Steps in Mixed Reality

Mixed Reality Praktikum – Assignment 1 (Passthrough, wall-anchored board, interactive machine) for **Meta Quest**.

| Task | Status |
|------|--------|
| 01 Passthrough enabled | ✅ |
| 02 Wall board (MRUK key wall) with name + description | ✅ |
| 03 Imported machine model placed in the room | ✅ |
| 04 Interaction with the machine | ✅ |

## Requirements

| Tool | Version |
|------|---------|
| Unity | **6000.3.27f1** (with *Android Build Support*: OpenJDK, Android SDK & NDK) |
| Render pipeline | Universal Render Pipeline 17.3 |
| Git + **Git LFS** | Git LFS ≥ 3.x |
| Headset | Meta Quest 3 / 3S (developer mode enabled) |

## Setup

```bash
# 1. Install Git LFS once per machine (otherwise binaries are checked out as text pointers)
git lfs install

# 2. Clone (LFS files are downloaded automatically)
git clone <repo-url>
cd <repo>
git lfs pull   # only needed if LFS was installed after cloning
```

3. Open the folder in **Unity Hub → Add → Add project from disk**, using editor version `6000.3.27f1`.
   Unity regenerates `Library/` on first import (takes a few minutes).

## Build & run on Quest

1. Connect the Quest via USB-C and allow USB debugging in the headset.
2. *File → Build Profiles →* select **Android**, then *Switch Platform*.
3. Pick the Quest under *Run Device*, then **Build And Run**.

## Scene overview (`Assets/Scenes/MRScene.unity`)

| Object | Purpose |
|--------|---------|
| `[BuildingBlock] Camera Rig` | OVRCameraRig (head + controller tracking). OVRManager: passthrough + scene support required, target devices Quest 2/3/3S |
| `[BuildingBlock] Passthrough` | OVRPassthroughLayer as underlay; the camera clears to transparent black so the real room is visible |
| `[BuildingBlock] MR Utility Kit` | MRUK loads the room model from Space Setup (walls, floor, furniture) |
| `WallSign` | `WallSign` + `WallSignPlacer` – information board on the key wall |
| `[BuildingBlock] Interactions Rig` | Interaction SDK interactors (poke, grab, ray) for hands and controllers |
| `Printer3D` | the printer model + `PrinterController`, `PrinterPlacer`, `PrinterButton`, `PrinterFeedback`, `PrinterColliders` |
| `[BuildingBlock] Effect Mesh` | invisible colliders for the real room (walls, floor, furniture) so released parts land on them |

### Wall sign (`Assets/Scripts/Sign/`)

- `WallSign` builds the board from code: dark backboard, blue accent bars and TextMeshPro texts
  (title, author name, description, live status line). All texts are editable in the Inspector.
- `WallSignPlacer` waits for MRUK's *scene loaded* callback, picks the room's **key wall**
  (`MRUKRoom.GetKeyWall` – the longest wall with no other wall behind it) and centres the board on it
  1.5 m above the floor, 1 cm in front of the wall, facing into the room.
  Without room data (no Space Setup / permission denied) the board is placed 2 m in front of the user.
- On Quest 2 the room must be set up manually (*Settings → Physical Space → Space Setup*);
  Quest 3 scans it automatically.

### Printer interaction (`Assets/Scripts/Printer/`)

| Component | Role |
|-----------|------|
| `PrinterController` | Simulated print job: moves gantry (Z), print head (X) and bed (Y) procedurally so the nozzle draws circles while a cylinder grows layer by layer. API: `TogglePrint()`, `CancelPrint()`, `onStateChanged` (Idle → Printing ⇄ Paused → Finished) |
| `PrinterPlacer` | After MRUK loaded the room: on the largest table if big enough (facing the user), else on a virtual stand in front of the key wall beside the sign, else in front of the user |
| `PrinterButton` | Spawns the Interaction SDK sample poke button next to the printer; *Select* on its `PokeInteractable` toggles the print; label shows the next action; controller vibration |
| `PrinterFeedback` | Status light (blue ready, pulsing green printing, blinking amber paused, white done), text on the printer display, spatial motor hum + beeps + chime, status line on the wall sign |

### Picking up the printed part

- `Assets/Prefabs/PrintedPart.prefab` was set up with the Interaction SDK Quick Action
  *Add Grab Interaction* (Rigidbody, collider, `Grabbable`, hand + controller grab interactables).
- `PrintedPart` disables grabbing while the part is being printed and enables it when the print is done.
  On release the part is detached from the bed; `Grabbable.ForceKinematicDisabled` turns it into a
  physics object, so it falls or can be thrown and lands on the room's Effect Mesh colliders.
- `PrinterController` measures the prefab's size (layer height + nozzle circle), grows it with its
  bottom on the bed, clears untouched parts on the next print and keeps up to 5 taken parts in the room.
- `PrinterColliders` adds a MeshCollider to the frame and a BoxCollider to the bed, so parts can be put back.

### Moving the sign and the printer

Point at the sign or the printer with the ray (controller: trigger, hand: pinch), hold and move:

| Object | Behaviour |
|--------|-----------|
| Wall sign | Slides along the wall under the ray, stays inside the wall, jumps to another wall when the ray points at it (`SignRepositioner`, uses `MRUKRoom.Raycast` against the wall anchors) |
| Printer | Follows the ray at the grabbed distance (thumbstick pushes/pulls). A downward `MRUKRoom.Raycast` finds what is below: on a table/cabinet top it stands on it and the stand disappears, on the floor it stands on the stand, outside the room it stays. It turns towards the user while moving (`PrinterRepositioner`) |

`RayDraggable` (`Assets/Scripts/Common/`) adds the Interaction SDK parts at runtime: a trigger `BoxCollider`
as handle, a `ColliderSurface` and a `RayInteractable`. Feedback: the object lightens when hovered and turns
blue while it is dragged (`HoverTint`), pick-up/drop sounds, controller vibration, and a short tick when the
sign changes walls or the printer changes surface.

### Changing rooms (`Assets/Scripts/Room/`)

MRUK loads the room model once at startup. If the user walks into another space (other boundary),
the old room can no longer be located and the content would stay at the old coordinates.

| Component | Role |
|-----------|------|
| `RoomManager` | Reloads the room (`MRUK.LoadSceneFromDevice`) when the app regains focus or the view is recentred – the Quest pauses the app for its boundary prompt. If the user is in a **different room** (other room UUID), the Effect Mesh colliders are rebuilt, taken parts removed and the sign and printer placed again; otherwise nothing moves, so the user's repositioning is kept |
| `WristMenu` | Raise the left hand/controller into view: a panel appears above it with the room status and two poke buttons – **Re-place** (reload and place sign + printer again) and **Scan room** (`OVRScene.RequestSpaceSetup`, then reload). Left controller menu button (≡) = Re-place |

MRUK's *Data Source* is set to **Device**: without Space Setup data the content is placed in front of
the user instead of on a random sample room (*Device With Prefab Fallback*). In the Editor (no headset
room data) the fallback placement is used.

Sounds are synthesised by `Tools/Audio/generate_sounds.py` (no third-party audio).
Materials in `Assets/Materials` are derived from URP's `Lit.mat`; emission is enabled in the asset
so the emissive shader variant is included in the Quest build.

## Repository layout

```
Assets/            Project content (scenes, scripts, prefabs, models) – versioned
SourceAssets/      Unmodified third-party originals (not imported by Unity)
Tools/Blender/     Blender scripts that generate assets in Assets/Models
Tools/Audio/       Python script that generates the sound effects in Assets/Audio
Packages/          manifest.json + packages-lock.json (dependencies) – versioned
ProjectSettings/   Unity project settings – versioned
Library/ Temp/ Logs/ UserSettings/   regenerated locally – ignored (.gitignore)
.gitattributes     Git LFS rules for binaries + LF line endings for Unity YAML
```

## Version control

- `.gitignore` is based on the official [GitHub Unity template](https://github.com/github/gitignore/blob/main/Unity.gitignore).
- Binary assets (models, textures, audio, fonts, native libraries) are stored with **Git LFS**, see `.gitattributes`.
  Check which files are in LFS with `git lfs ls-files`.
- Workflow: `main` stays runnable; each task is developed on a `feature/*` branch and merged with `--no-ff`.

## Third-party assets

| Asset | Author | Source | License |
|-------|--------|--------|---------|
| 3D Printer (machine model) | [Gregsterius](https://sketchfab.com/Gregsterius) | [Sketchfab](https://sketchfab.com/3d-models/3d-printer-0e825a8d48b14bee883a131fe029d4fc) | [CC BY 4.0](http://creativecommons.org/licenses/by/4.0/) |

The model was modified for this project (see below); the unmodified original is kept in `SourceAssets/Printer3D/`.

## Model pipeline (Blender)

`Assets/Models/Printer3D/Printer3D.fbx` is generated from the original download with a Blender script (Blender 5.2):

```bash
blender --background --factory-startup --python Tools/Blender/prepare_printer.py -- \
    SourceAssets/Printer3D/Printer.fbx Assets/Models/Printer3D/Printer3D.fbx
```

The script groups the ~80 loose parts into the printer's moving assemblies, joins each into one mesh
(fewer draw calls on Quest), scales the model to a real-world height of 0.45 m and sets pivots for animation:

| Object | Moves along | Pivot |
|--------|-------------|-------|
| `Frame` | static | floor centre |
| `Bed` | Y axis (front/back) | centre of bed surface |
| `Gantry` | Z axis (up/down) | centre of beam |
| `PrintHead` | X axis on the gantry | nozzle tip |
| `ZCable`, `FilamentTube` | stretched to follow gantry/head | fixed end |
| `Display`, `DisplayAnchor` | static, screen has a soft blue emissive glow | centre of front screen |

All parts are direct children of `Printer3D` (Blender's *Apply Transform* FBX option breaks nested
children), so the controller script moves `PrintHead` together with `Gantry`.

## Author

Linus Pihan
