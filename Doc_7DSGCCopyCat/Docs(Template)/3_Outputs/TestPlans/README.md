# Test Plans

Output directory for the QA Agent.

## Naming Convention

| Type | Pattern | Example |
| --- | --- | --- |
| Test Plan | `{feature-name}-test-plan.md` | `parry-timing-test-plan.md` |
| Regression Report | `{feature-name}-regression.md` | `inventory-refactor-regression.md` |

## Lifecycle

1. QA Agent reads the design spec, architecture plan, and implementation summary.
2. QA Agent writes a test plan and saves it here.
3. Developer or tester executes the test plan during playtesting.
4. Failed tests become bug reports using `.github/ISSUE_TEMPLATE/bug_report.md`.
5. After feature ships, test plans remain as regression reference.

## Handoff Contract
 
Test plans should reference the task card, design spec, architecture plan, and GDD tags for the same slug.
 
Use `Docs/2_System_Files/Handoff_Contracts/README.md` for status and checkpoint rules.
 
## Context Loading Rule
 
Agents should load only the test plan relevant to their current task, not the entire directory.
 
See `Docs/0_User_Manual/RULES_AND_POLICY.md` Section 8 for token budget policy.
