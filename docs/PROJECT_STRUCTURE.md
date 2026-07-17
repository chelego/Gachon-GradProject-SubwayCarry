# Project Structure

Project-owned Unity assets are stored under `SubwayCarry/Assets/_Project`.

```text
_Project/
|-- Art/                 Project graphics and source art
|-- Audio/               Music and sound assets
|-- Data/                Game definitions and ScriptableObjects
|-- Prefabs/             Reusable GameObjects
|-- Scenes/              Runtime and prototype scenes
|-- Scripts/
|   |-- AI/              Passenger decisions and crowd simulation
|   |-- Core/            Bootstrap, shared services, save, and scene flow
|   |-- Delivery/        Delivery, economy, progression, and route state
|   |-- Gameplay/        Player, package, train motion, and interactions
|   |-- Transit/         Stations, trains, doors, and route runtime
|   `-- UI/              Menus, HUD, map, settlement, and feedback
|-- Tests/               Edit Mode and Play Mode tests
`-- UI/                  UI documents, sprites, and prefabs
```

Unity template and package-owned assets may remain outside `_Project`.

## Scenes

```text
Assets/_Project/Scenes/Bootstrap.unity
Assets/_Project/Scenes/MainMenu.unity
Assets/_Project/Scenes/GachonHub.unity
Assets/_Project/Scenes/StationGameplay.unity
Assets/_Project/Scenes/TrainGameplay.unity
Assets/_Project/Scenes/Prototype_Core.unity
```

Only `Bootstrap.unity` exists at the start of development. Add the other scenes when their milestone begins.

## Namespaces

Use `SubwayCarry` as the root namespace.

Examples:

- `SubwayCarry.Core`
- `SubwayCarry.Gameplay`
- `SubwayCarry.AI`
- `SubwayCarry.Transit`
- `SubwayCarry.Delivery`
- `SubwayCarry.UI`

## Ownership

- Put runtime code in the matching script area.
- Put shared interfaces and service contracts in `Core` only when multiple areas use them.
- Keep content values in data assets instead of hard-coding delivery, route, passenger, or balance values.
- Keep test-only scenes and data clearly named and separate from release content.
