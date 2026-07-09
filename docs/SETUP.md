# Setup

## Required Tools

Install the following tools before working on the project.

| Tool | Version / Note |
| --- | --- |
| Unity Hub | Required |
| Unity Editor | `6000.3.7f1` |
| VSCode | Recommended code editor |
| Git | Required |
| Git LFS | Required for binary assets |
| GitHub account | Required for collaboration |

## VSCode Extensions

Recommended extensions:

- `ms-dotnettools.csharp`
- `ms-dotnettools.csdevkit`
- `visualstudiotoolsforunity.vstuc`
- `eamodio.gitlens`

The repository includes `.vscode/extensions.json`.

## Unity Project

Current project configuration:

| Item | Value |
| --- | --- |
| Project name | `SubwayCarry` |
| Template | Universal 2D |
| Unity Editor | `6000.3.7f1` |
| Project root | `E:\UnityProjects\graduation-game-project` |
| Unity project path | `E:\UnityProjects\graduation-game-project\SubwayCarry` |
| GitHub repository | `https://github.com/chelego/Gachon-GradProject-SubwayCarry` |

Open this folder in Unity Hub:

```text
E:\UnityProjects\graduation-game-project\SubwayCarry
```

## Unity Editor Settings

Verify the following settings after opening the project.

Path:

```text
Edit > Project Settings > Editor
```

Required settings:

| Setting | Value |
| --- | --- |
| Version Control Mode | Visible Meta Files |
| Asset Serialization Mode | Force Text |

These settings are required for Git-based Unity collaboration.

## Git LFS

Git LFS must be installed before cloning or adding large assets.

```powershell
git lfs install
```

The repository tracks common binary asset formats through `.gitattributes`.

Examples:

- `.png`
- `.jpg`
- `.psd`
- `.wav`
- `.mp3`
- `.ogg`
- `.fbx`
- `.blend`

## Clone

```powershell
git clone https://github.com/chelego/Gachon-GradProject-SubwayCarry.git
cd Gachon-GradProject-SubwayCarry
git checkout develop
git lfs pull
```

Open the Unity project folder:

```text
Gachon-GradProject-SubwayCarry\SubwayCarry
```

## Branch Workflow

Use `develop` as the base branch for feature work.

```powershell
git checkout develop
git pull
git checkout -b feature/example-feature
```

After implementing a feature:

```powershell
git add .
git commit -m "feat: add example feature"
git push -u origin feature/example-feature
```

Create a pull request into `develop`.

## Commit Message Format

Use short English commit messages.

Examples:

- `feat: add player movement`
- `feat: implement object damage`
- `feat: add npc state machine`
- `fix: prevent collision bug`
- `docs: update setup guide`

## Repository Policy

This repository is for confirmed team members.

Allowed:

- Source code
- Unity project settings
- Setup documents
- Collaboration documents
- Design documents
- Task documents
- Planning notes

Not allowed:

- Unapproved external assets
- Secrets or credentials
