# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Conventional Commits](COMMIT_CONVENTION.md).

---

## [Unreleased]

### Added
- {New features — from `feat()` commits}

### Fixed
- {Bug fixes — from `fix()` commits}

### Changed
- {Refactors and behavior changes — from `refactor()` commits}

### Polished
- {Game feel / juice — from `juice()` commits}

### Removed
- {Deprecated features or dead code removed}

---

## [{version}] — {YYYY-MM-DD}

### Added
- {feature description} (`feat(scope): commit message`)

### Fixed
- {fix description} (`fix(scope): commit message`)

### Changed
- {change description} (`refactor(scope): commit message`)

---

## Auto-Generation

### From Git Log (Manual)
```bash
# Generate raw changelog from conventional commits
git log --pretty=format:"- %s (%h)" --no-merges --since="{last-release-date}"
```

### Recommended Tools
| Tool | How |
|------|-----|
| [conventional-changelog](https://github.com/conventional-changelog/conventional-changelog) | `npx conventional-changelog -p angular -i CHANGELOG.md -s` |
| [git-cliff](https://github.com/orhun/git-cliff) | `git-cliff -o CHANGELOG.md` |
| [standard-version](https://github.com/conventional-changelog/standard-version) | `npx standard-version` |

### Mapping Commit Types to Changelog Sections
| Commit Type | Changelog Section |
|------------|-------------------|
| `feat` | Added |
| `fix` | Fixed |
| `refactor` | Changed |
| `juice` | Polished |
| `docs` | _(not included unless significant)_ |
| `chore` | _(not included unless significant)_ |
| `style` | _(not included)_ |
| `test` | _(not included)_ |
