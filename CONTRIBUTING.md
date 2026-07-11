# Contributing

## Branch Workflow

1. Update `develop` before starting work.
2. Create a branch named `feature/<short-name>` or `fix/<short-name>`.
3. Keep one feature or fix in each branch.
4. Open a pull request into `develop`.
5. Do not push feature work directly to `main`.

## Before Committing

- Open the project with Unity `6000.3.7f1`.
- Wait for asset import and compilation to finish.
- Check the Unity Console for errors.
- Run the affected scene or tests.
- Review `git status` and exclude generated or unrelated files.
- Include matching `.meta` files for new, moved, or deleted Unity assets.

## Commit Messages

Use a short type and description.

```text
feat: add player movement
fix: prevent duplicate NPC spawn
docs: update setup instructions
chore: reorganize project assets
test: add item damage tests
```

## Unity Scene and Prefab Work

- Keep project-owned assets under `Assets/_Project`.
- Tell the team before making large changes to a shared scene or prefab.
- Prefer prefabs for reusable objects.
- Do not edit files under `Library`, `Temp`, `Logs`, or `UserSettings`.
- Do not delete or recreate `.meta` files manually.

## Pull Requests

Describe what changed, how it was tested, and any scene or prefab that other team members should avoid editing until the pull request is merged.
