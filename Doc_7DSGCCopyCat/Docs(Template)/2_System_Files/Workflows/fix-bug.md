---
description: Structured bug investigation and fix workflow
---

# Fix Bug Workflow

## Steps

### 0. Validate Intake
- If the bug came from Discord, Notion, GitHub, or a direct prompt, convert it into `Docs/3_Outputs/Specs/{bug-name}-task-card.md`
- Validate the task card against `Docs/2_System_Files/Handoff_Contracts/README.md`
- Route with `2_System_Files/Agent_Prompts/orchestrator-agent.md` and confirm the workflow is `/fix-bug`
 
### 1. Understand the Bug
- Read the bug report (issue or description)
- Identify: severity, affected system, reproduction steps
- Reference `Docs/1_Inputs_Templates/GDD_Fighting_Allstar.md` @tag section for intended behavior
 
### 2. Check Related Systems
- Read `Docs/3_Outputs/ADRs/` for architecture decisions affecting this area
- Read `Docs/1_Inputs_Templates/project-stack.md` for system locations
- Identify the specific files involved

// turbo
### 3. Reproduce
- Follow the exact reproduction steps
- Confirm the bug exists in the current build
- Note: does it happen on primary platform, secondary, or both?

### 4. Root Cause Analysis
- Read the relevant source files
- Trace the execution path from trigger to failure
- Identify the root cause (not just the symptom)
- Document: "The bug occurs because {X} when {Y} happens"

### 5. Fix Implementation
- Create a fix branch: `git checkout -b fix/{bug-name}`
- Apply the minimal fix that addresses the root cause
- Follow `Docs/0_User_Manual/RULES_AND_POLICY.md` naming and optimization rules
- Do NOT refactor unrelated code in the same commit
 
### 6. Regression Check
Verify these are NOT broken by the fix:
- [ ] The original feature still works as designed
- [ ] Related systems are unaffected
- [ ] No new console errors or warnings
- [ ] Performance is not degraded
- [ ] Works on primary platform
 
### 7. Edge Case Verification
- [ ] Bug does not recur under similar conditions
- [ ] Rapid input doesn't trigger it
- [ ] Scene transitions don't trigger it
- [ ] Null/empty states are handled
 
### 7.5 QA Update
If the bug affects a feature with an existing test plan:
- Update `Docs/3_Outputs/TestPlans/{feature-name}-test-plan.md`, or
- Create `Docs/3_Outputs/TestPlans/{bug-name}-regression.md`
 
Use `2_System_Files/Agent_Prompts/qa-agent.md` for regression coverage if the bug has meaningful player-facing risk.

### 8. Commit and DevLog
```bash
git add -A
git commit -m "fix(scope): brief description of what was fixed

Root cause: {explanation}
Ref: #{issue-number}"
```
Add a brief note to the current DevLog entry under **Bugs Found**.

### 9. Done
Fix is ready for human review and merge.
