# Contribution Rules

## Branch Workflow

1. Update `develop` before starting work.
2. Create `feature/<short-name>` or `fix/<short-name>`.
3. Keep one feature or fix in each branch.
4. Open a pull request into `develop`.
5. Do not push feature work directly to `main`.

## Before Committing

- Open the project with Unity `6000.3.7f1`.
- Wait for asset import and compilation to finish.
- Check the Unity Console for errors.
- Run the affected scene or tests.
- Review `git status` and exclude generated or unrelated files.
- Include matching `.meta` files for added, moved, or deleted Unity assets.

## Commit Messages

```text
feat: add player movement
fix: prevent duplicate passenger spawn
docs: update game design
chore: reorganize project assets
test: add package damage tests
```

## Unity Assets

- Store project-owned assets under `SubwayCarry/Assets/_Project`.
- Announce large shared scene or prefab changes before editing.
- Prefer prefabs and data definitions over one large shared scene.
- Do not edit or commit `Library`, `Temp`, `Logs`, `UserSettings`, `Build`, or `Builds`.
- Do not delete or recreate `.meta` files manually.

## Pull Requests

Every pull request states:

- What changed
- How it was tested
- Changed scenes, prefabs, data, and project settings
- Known limitations or follow-up work
- Temporary ownership or merge-conflict warnings
