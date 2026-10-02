---
description: Structured refactoring workflow with risk assessment and regression safety
---

# Refactor Workflow

> **When to use:** Codebase restructuring that doesn't add features or fix bugs — renaming, splitting, extracting, decoupling, or migrating systems.

## Why Refactors Need Their Own Workflow

Refactors have a **different risk profile** than features or bugfixes:
- No new behavior → harder to test "did it work?"
- High regression surface → things that worked before might break
- Architecture decisions → often need ADRs
- Scope creep risk → "while I'm here..." is the enemy

## Steps

### 0. Validate Task Card (Router)
Use `2_System_Files/Agent_Prompts/orchestrator-agent.md` to validate:
- Task slug, type, owner, source, and GDD/ADR references are present
- The task is a refactor, not a hidden feature or bugfix
- `status` allows refactor work to start
- `blocked_by` is empty
 
If the task card is missing, create it from `Docs/1_Inputs_Templates/Task_Card_Template.md` before continuing.
 
### 1. Define the Refactor Goal
Write a one-sentence goal:
- ✅ "Extract stamina logic from PlayerController into its own StaminaSystem component"
- ❌ "Clean up the player code" (too vague — scope will creep)
 
Reference the GDD section or ADR that motivates this refactor.
 
### 2. Check Existing ADRs
Read `Docs/3_Outputs/ADRs/README.md` — does an ADR already govern this area?
- If yes, ensure the refactor **follows** the existing decision
- If the refactor **changes** an architecture decision, draft a new ADR (step 4)
 
### 3. Impact Analysis (🏗️ Architect Agent Role)
Using `2_System_Files/Agent_Prompts/architect-agent.md` as your guide, analyze:
- Which scripts are affected?
- Which systems depend on the code being refactored?
- What is the blast radius if something breaks?
- Can the refactor be done incrementally (prefer this), or must it be atomic?
 
Produce a **Class Responsibility Table** showing before → after ownership.
 
**Save to:** `Docs/3_Outputs/Specs/{refactor-name}-impact-analysis.md`
 
**Human checkpoint:** Refactor scope and architecture impact must be approved before implementation.
 
### 4. Write ADR (if architecture changes)
Copy `Docs/1_Inputs_Templates/ADR_TEMPLATE.md` → `Docs/3_Outputs/ADRs/{NNN}-{kebab-title}.md`
- Document the decision, alternatives considered, and migration path
- Mark the status as **Proposed** until human approves

### 5. Create Refactor Branch
```bash
git checkout -b refactor/{refactor-name}
```

### 6. Implement Incrementally (💻 Implementation Agent Role)
Using `2_System_Files/Agent_Prompts/implementation-agent.md` as your guide:
- **One commit per logical change** — don't lump everything together
- Follow `Docs/0_User_Manual/RULES_AND_POLICY.md` naming and optimization rules
- **Do NOT add features** in the same PR — refactor only
- **Do NOT change behavior** — before and after should be functionally identical
- Commit with conventional format: `refactor(scope): description`
 
### 7. Self-Review (👀 Code Review Agent Role)
Using `2_System_Files/Agent_Prompts/code-review-agent.md`, review with focus on:
- [ ] **No behavior change** — output is functionally identical to before
- [ ] **No god-object growth** — refactor should reduce coupling, not move it
- [ ] **Naming conventions** — all renamed items follow `Docs/0_User_Manual/RULES_AND_POLICY.md` §2
- [ ] **Hierarchy convention** — scene objects still follow separator convention (§4)
- [ ] **No orphaned references** — all `using`, `[SerializeField]`, and scene references updated
- [ ] **No silent side effects** — if something moved, all callers are updated
 
### 8. Regression Checklist
This is the **critical step** for refactors. Verify:
- [ ] All existing functionality still works exactly as before
- [ ] No new console errors or warnings
- [ ] No missing references in the Inspector (prefabs, scenes, ScriptableObjects)
- [ ] No broken scene hierarchies
- [ ] Performance is not degraded (check Profiler if touching hot paths)
- [ ] All tests pass (if tests exist)
 
### 9. Write DevLog Entry
Copy `Docs/1_Inputs_Templates/DevLog_Template.md` → `Docs/3_Outputs/DevLog/{YYYY-MM-DD}-{refactor}.md`
- Emphasis on: what moved, what was renamed, what coupling was broken
- List any follow-up refactors this enables
 
### 10. Final Commit and PR
```bash
git add -A
git commit -m "refactor(scope): description
 
Motivation: {why this refactor was needed}
ADR: ADR-{NNN} (if applicable)"
git push origin refactor/{refactor-name}
```
Create PR using `.github/PULL_REQUEST_TEMPLATE.md`. Mark type as `refactor`.
 
### 10.5 Team Sync
Follow `Docs/0_User_Manual/TEAM_SYNC_POLICY.md`.
- Share impact analysis and PR links, not loose summaries
- Do not announce behavior changes unless the approved task explicitly included them

### 11. Done
Refactor is ready for human review and merge.

---

## Anti-Patterns for Refactors

> [!CAUTION]
> - **"While I'm here..."** — Do NOT add features, fix bugs, or polish in the same PR
> - **Big bang refactor** — Prefer incremental commits over rewriting everything at once
> - **Rename without grep** — Always search the entire project for references before renaming
> - **Refactor without tests** — If the system has no tests, write a minimal regression test *before* refactoring, not after
> - **Refactor without ADR** — If you're changing how systems relate to each other, it's an architecture decision — write the ADR first
