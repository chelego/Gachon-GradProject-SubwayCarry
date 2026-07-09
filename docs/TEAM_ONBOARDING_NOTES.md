# Team Onboarding

## Project Status

| Item | Status |
| --- | --- |
| Engine | Unity |
| Template | Universal 2D |
| Unity project | `SubwayCarry` |
| Unity Editor | `6000.3.7f1` |
| Language | C# |
| Code editor | VSCode |
| Version control | Git / GitHub |
| Git LFS | Enabled |
| Repository | `https://github.com/chelego/Gachon-GradProject-SubwayCarry` |
| Main branch | `main` |
| Development branch | `develop` |

## Local Project Path

Current local path on the project owner's PC:

```text
E:\UnityProjects\graduation-game-project
```

Unity project path:

```text
E:\UnityProjects\graduation-game-project\SubwayCarry
```

Team members do not need to use the same local path.

## Required Setup

Each team member should install:

1. Unity Hub
2. Unity Editor `6000.3.7f1`
3. VSCode
4. Git
5. Git LFS

Recommended VSCode extensions:

- C# Dev Kit
- C#
- Unity
- GitLens

## Initial Clone

```powershell
git clone https://github.com/chelego/Gachon-GradProject-SubwayCarry.git
cd Gachon-GradProject-SubwayCarry
git checkout develop
git lfs pull
```

Open this folder through Unity Hub:

```text
Gachon-GradProject-SubwayCarry\SubwayCarry
```

## Branch Rules

Use `develop` for integration.

Use feature branches for individual tasks.

Examples:

- `feature/player-movement`
- `feature/object-damage`
- `feature/npc-ai`
- `feature/train-system`
- `feature/ui`

Do not work directly on `main`.

## Pull Request Flow

1. Start from `develop`.
2. Create a feature branch.
3. Implement the task.
4. Commit changes.
5. Push the feature branch.
6. Open a pull request into `develop`.
7. Merge after review.

## Unity Collaboration Rules

Unity settings:

| Setting | Value |
| --- | --- |
| Version Control Mode | Visible Meta Files |
| Asset Serialization Mode | Force Text |

Do not delete `.meta` files manually.

Do not commit Unity generated folders:

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `Build/`
- `Builds/`

These folders are ignored by `.gitignore`.

## Asset Rules

Binary assets are managed by Git LFS through `.gitattributes`.

Examples:

- Images
- Audio
- Video
- Source art files
- 3D source files

Run this once after installing Git LFS:

```powershell
git lfs install
```

## Design Document Policy

Design documents may be stored in this repository.

This repository is intended for confirmed team members only.

Allowed:

- Code
- Unity project files
- Setup documents
- Collaboration rules
- Task descriptions
- Game design documents
- Planning notes
- System design notes

Not allowed:

- Unapproved assets
- Credentials

Access to the repository should be limited to project members.
