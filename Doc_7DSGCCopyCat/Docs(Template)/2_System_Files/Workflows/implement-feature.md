---
description: End-to-end feature implementation following the multi-agent pipeline
---

# Implement Feature Workflow

## Prerequisites
- `Docs/1_Inputs_Templates/GDD_Fighting_Allstar.md` is filled in with your project's design
- `Docs/1_Inputs_Templates/project-stack.md` is filled in with your tech stack
- `Docs/0_User_Manual/RULES_AND_POLICY.md` is in place
- `Docs/3_Outputs/Specs/{feature-name}-task-card.md` exists and follows `Docs/2_System_Files/Handoff_Contracts/README.md`
 
## Steps
 
### 0. Validate Task Card (Router)
Use `2_System_Files/Agent_Prompts/orchestrator-agent.md` to validate:
- Task slug, type, owner, source, and GDD tags are present
- `status` allows feature work to start
- `blocked_by` is empty
- The workflow selected is `/implement-feature`
 
If the task card is missing, create it from `Docs/1_Inputs_Templates/Task_Card_Template.md` before continuing.

### 1. Identify the GDD Section
Read the relevant `@tag:` section from `Docs/1_Inputs_Templates/GDD_Fighting_Allstar.md` that motivates this feature.
- Quote the specific lines that define the intended behavior
- Note any experience pillars this feature must support
 
### 2. Check Existing ADRs
Read `Docs/3_Outputs/ADRs/README.md` and check if any existing ADR governs this area.
- If yes, follow the ADR's decisions
- If the feature requires a new architecture decision, draft an ADR later in Step 5
 
### 3. Write the Design Spec (🎮 Game Design Agent Role)
Using `2_System_Files/Agent_Prompts/game-design-agent.md` as your guide, produce:
- Player experience description
- Interaction flow diagram
- Juice specification (screen shake, particles, audio, timing)
- Edge cases from player perspective
- GDD alignment check
 
**Save to:** `Docs/3_Outputs/Specs/{feature-name}-design-spec.md`
 
**Human checkpoint:** Design/game-feel approval is required before architecture work. If rejected, update the task card or design spec and rerun Step 3 only.
 
### 4. Write the Architecture Plan (🏗️ Architect Agent Role)
Using `2_System_Files/Agent_Prompts/architect-agent.md` as your guide, produce:
- System diagram (mermaid)
- Interface definitions
- Class responsibility table
- Data flow
- Which existing scripts need modification
 
**Save to:** `Docs/3_Outputs/Specs/{feature-name}-arch-plan.md`
 
**Human checkpoint:** Architecture approval is required before implementation. If rejected, update the architecture plan or ADR draft and rerun Step 4 only.
 
### 5. Write ADR (if architecture changed)
Copy `Docs/1_Inputs_Templates/ADR_TEMPLATE.md` → `Docs/3_Outputs/ADRs/{NNN}-{kebab-title}.md`
- Document the decision, alternatives, and consequences
- Update `Docs/3_Outputs/ADRs/README.md` registry

### 6. Create Feature Branch
```bash
git checkout -b feat/{feature-name}
```

### 7. Implement (💻 Implementation Agent Role)
Using `2_System_Files/Agent_Prompts/implementation-agent.md` as your guide:
- Follow the architecture spec exactly
- Follow `Docs/0_User_Manual/RULES_AND_POLICY.md` naming and optimization rules
- Follow hierarchy convention for any new scene objects
- Commit with conventional format: `feat(scope): description`
 
### 8. Self-Review (👀 Code Review Agent Role)
Using `2_System_Files/Agent_Prompts/code-review-agent.md`, review your own code for:
- Architecture violations
- Naming convention issues
- Performance concerns (no Update allocations, cached refs)
- Hierarchy convention compliance
- Edge cases
 
### 9. Write Test Plan (🧪 QA Agent Role)
Using `2_System_Files/Agent_Prompts/qa-agent.md`, create:
- Functional test cases
- Edge case tests
- Platform-specific tests
- Regression checklist
 
**Save to:** `Docs/3_Outputs/TestPlans/{feature-name}-test-plan.md`
 
### 10. Write DevLog Entry
Copy `Docs/1_Inputs_Templates/DevLog_Template.md` → `Docs/3_Outputs/DevLog/{YYYY-MM-DD}-{feature}.md`
- Document what was done, key decisions, bugs found, game feel notes
 
### 11. Final Commit and PR
```bash
git add -A
git commit -m "feat(scope): description"
git push origin feat/{feature-name}
```
Create PR using `.github/PULL_REQUEST_TEMPLATE.md`
 
### 11.5 Team Sync
Follow `Docs/0_User_Manual/TEAM_SYNC_POLICY.md`.
- Discord/Notion updates are allowed only after the PR or review artifact exists
- Official status publishing requires human approval
- Link back to the repo artifact or PR as the source of truth
 
### 12. Done
Feature is ready for human review and merge.
