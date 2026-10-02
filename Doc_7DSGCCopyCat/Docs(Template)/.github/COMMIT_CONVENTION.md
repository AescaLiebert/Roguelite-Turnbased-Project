# Conventional Commits Guide

## Format

```
type(scope): short description (imperative mood, lowercase)

[optional body — explain WHY, not WHAT]

[optional footer — breaking changes, issue refs]
```

## Types

| Type       | When to use                                      | Example |
|------------|--------------------------------------------------|---------|
| `feat`     | New feature or behavior for the player           | `feat(combat): add parry timing window` |
| `fix`      | Bug fix                                          | `fix(inventory): prevent pickup when slots full` |
| `refactor` | Code change that doesn't add features or fix bugs | `refactor(player): extract stamina into own component` |
| `docs`     | Documentation only                               | `docs: add ADR-002 for inventory refactor` |
| `style`    | Formatting, naming fixes, no logic change        | `style(ui): rename HealthBar → HealthBarUI` |
| `test`     | Adding or fixing tests                           | `test(spawner): add spawn distribution test` |
| `chore`    | Build, CI, dependencies, tooling                 | `chore: update URP to latest` |
| `juice`    | Game feel / polish / VFX / SFX only              | `juice(player): add screen shake on damage` |

## Scopes (customize per project)

`player` · `inventory` · `enemy` · `ui` · `audio` · `scene` · `build` · `docs` · `{your-system}`

> Replace with your project's actual system names. See `Docs/0_User_Manual/RULES_AND_POLICY.md` §2 for naming conventions.

## Examples

```
feat(enemy): implement shadow phase before chase

The enemy now follows silently for 3-5 seconds with audio cues
before entering chase mode, matching the GDD design intent.

Ref: GDD @tag:enemies
```

```
fix(flashlight): prevent drain while paused

Flashlight was losing battery during pause menu.
This broke the tension loop by punishing menu usage.
```

```
juice(player): add heartbeat audio on low health

- 3 intensity levels based on health percentage
- BPM increases: 60 → 90 → 120
- Fades in/out smoothly
```

## Branch Naming

- Format: `{prefix}/{short-description}`
- Examples: `feat/parry-system`, `fix/inventory-overflow`, `refactor/scene-loading`
- Use kebab-case for the description

## Quick Rule
 
> Before committing, check your DevLog entry in `Docs/3_Outputs/DevLog/`.
> The commit message should be a condensed version of what you wrote there.
