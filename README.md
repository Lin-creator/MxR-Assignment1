# MxR Assignment 1 – First Steps in Mixed Reality

Mixed Reality Praktikum – Assignment 1 (Passthrough, wall-anchored board, interactive machine) for **Meta Quest**.

| Task | Status |
|------|--------|
| 01 Passthrough enabled | ⬜ |
| 02 Wall board (MRUK key wall) with name + description | ⬜ |
| 03 Imported machine model placed in the room | ⬜ |
| 04 Interaction with the machine | ⬜ |

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

## Repository layout

```
Assets/            Project content (scenes, scripts, prefabs, models) – versioned
SourceAssets/      Unmodified third-party originals (not imported by Unity)
Tools/Blender/     Blender scripts that generate assets in Assets/Models
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
| `Display`, `DisplayAnchor` | static | centre of front screen |

All parts are direct children of `Printer3D` (Blender's *Apply Transform* FBX option breaks nested
children), so the controller script moves `PrintHead` together with `Gantry`.

## Author

Linus Pihan
