# Project Structure

Project-owned Unity assets are stored under `SubwayCarry/Assets/_Project`.

```text
_Project/
|-- Art/                 Project graphics and source art
|-- Audio/               Music and sound assets
|-- Data/                ScriptableObjects and game data
|-- Prefabs/             Reusable GameObjects
|-- Scenes/              Project scenes
|-- Scripts/
|   |-- AI/              NPC and decision-making code
|   |-- Core/            Shared runtime systems
|   |-- Gameplay/        Player and gameplay code
|   `-- UI/              UI behavior code
|-- Tests/               Edit Mode and Play Mode tests
`-- UI/                  UI documents, sprites, and prefabs
```

Unity template and package-owned assets may remain outside `_Project`.

The initial build scene is:

```text
Assets/_Project/Scenes/Bootstrap.unity
```

Use the root namespace `SubwayCarry` for new C# scripts. Add a more specific namespace when useful, such as `SubwayCarry.Gameplay` or `SubwayCarry.AI`.
