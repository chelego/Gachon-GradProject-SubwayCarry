# Gachon GradProject SubwayCarry

SubwayCarry is a Unity game project for the 2026-2 Gachon University graduation project.

The player carries a fragile cake through crowded subway trains. Each delivery has a route, transport cost, item value, crowd conditions, and financial risk. The run ends when the player completes the final scholarship delivery or goes bankrupt.

## Project

| Item | Value |
| --- | --- |
| Unity project | `SubwayCarry` |
| Template | Universal 2D |
| Unity Editor | `6000.3.7f1` |
| Language | C# |
| Code editor | VSCode |
| Version control | Git / GitHub |
| Large file storage | Git LFS |

## Documents

All detailed project documents are stored in [`docs/`](docs/README.md).

- [Game Design](docs/GAME_DESIGN.md)
- [Development Breakdown](docs/DEVELOPMENT_BREAKDOWN.md)
- [Project Structure](docs/PROJECT_STRUCTURE.md)
- [Contribution Rules](docs/CONTRIBUTING.md)

## Repository Layout

```text
graduation-game-project/
|-- SubwayCarry/        Unity project
|-- docs/               Project documents
|-- .vscode/            VSCode recommendations
|-- .gitignore          Unity ignore rules
|-- .gitattributes      Text and Git LFS rules
`-- README.md           Repository entry point
```

## Branches

| Branch | Purpose |
| --- | --- |
| `main` | Stable baseline |
| `develop` | Integration branch |
| `feature/*` | Individual feature work |
| `fix/*` | Individual bug fixes |

Development work starts from `develop` and returns through a pull request.
