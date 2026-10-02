---
name: Bug Report
about: Report a bug to help improve the project
title: "bug({scope}): {brief description}"
labels: bug
---

## Bug Description

_{Clear, concise description of the bug.}_

## Reproduction Steps

1. {step 1}
2. {step 2}
3. {step 3}

## Expected Behavior

_{What should happen.}_

## Actual Behavior

_{What actually happens.}_

## Environment

- **Platform:** {Mobile / PC}
- **Device:** {device name}
- **Unity Version:** {version}
- **Scene:** {which scene}
- **Build:** {editor / development build / release}

## Severity

- [ ] Critical - crash, data loss, blocks progression
- [ ] Major - broken feature, bad UX
- [ ] Minor - cosmetic, workaround exists
- [ ] Cosmetic - visual only, no gameplay impact

## Agent Workflow

- [ ] Converted into `Docs/Specs/{slug}-task-card.md`
- [ ] Routed with `AgentPrompts/orchestrator-agent.md`
- [ ] Regression test plan created or updated if player-facing risk exists

## Additional Context

- **Screenshot / Video:** {attach or link}
- **Console Log:** {error message if applicable}
- **GDD Reference:** @tag:{section} - _{does the bug violate intended GDD behavior?}_
- **Frequency:** Always / Sometimes / Rare / Once
