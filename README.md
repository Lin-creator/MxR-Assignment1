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

_Model sources and licences will be listed here._

## Author

Linus Pihan
