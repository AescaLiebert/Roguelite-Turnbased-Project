# Multi-Agent Workflow — Simulated Test & Review

> [!WARNING]
> Superseded by `Docs/Reports/2026-05-18-multi-agent-workflow-test-review.md`.
> This file is kept as historical audit context only. Do not treat its findings as current truth without re-validating the present folder state.

> **Scope:** Every file in `Docs(Template)/` was read, cross-referenced, and dry-run through 4 simulated workflows. This document reports findings.

---

## 1. Workflow Simulation Results

### Simulation Method

I traced each workflow path as if a real feature/bug were being processed, tracking:
- **Every file read** (context load cost)
- **Every file written** (output artifacts)
- **Every dependency** (does the referenced file exist? Is the template complete?)
- **Every handoff** (does Agent A's output format match Agent B's expected input?)

---

### 🔬 Test 1: `/implement-feature` — "Add Parry Timing System"

```
Step 1: Read GDD/GDD_Fighting_Allstar.md @tag:mechanics          ✅ File exists (_TEMPLATE.md, needs fill)
Step 2: Read ADRs/README.md                      ✅ File exists
Step 3: Game Design Agent role                   ✅ Prompt exists (game-design-agent.md)
        → Output: Design Spec                    ⚠️ No target path defined — where does it go?
Step 4: Architect Agent role                     ✅ Prompt exists (architect-agent.md)
        → Output: Technical Plan                 ⚠️ No target path defined — where does it go?
Step 5: Write ADR                                ✅ Template exists (_TEMPLATE.md)
Step 6: Create feature branch                    ✅ Convention defined (COMMIT_CONVENTION.md)
Step 7: Implementation Agent role                ✅ Prompt exists (implementation-agent.md)
Step 8: Code Review Agent (self-review)          ✅ Prompt exists (code-review-agent.md)
Step 9: QA Agent                                 ✅ Prompt exists (qa-agent.md)
        → Output: Test Plan                      ⚠️ No target path defined — where does it go?
Step 10: Write DevLog                            ✅ Template exists (_TEMPLATE.md)
Step 11: Final Commit + PR                       ✅ PR template exists
Step 12: Done                                    ✅
```

````carousel
**Files READ during this workflow (token cost):**
```
Docs/GDD/GDD_Fighting_Allstar.md                          (~7,900 bytes)
Docs/GDD/AI-Context/project-stack.md     (~1,500 bytes)
Docs/RULES_AND_POLICY.md                 (~10,300 bytes)
Docs/ADRs/README.md                      (~1,200 bytes)
AgentPrompts/game-design-agent.md        (~2,700 bytes)
AgentPrompts/architect-agent.md          (~2,600 bytes)
AgentPrompts/implementation-agent.md     (~2,600 bytes)
AgentPrompts/code-review-agent.md        (~3,000 bytes)
AgentPrompts/qa-agent.md                 (~3,000 bytes)
──────────────────────────────────────
TOTAL CONTEXT LOAD:                      ~34,800 bytes (~8,700 tokens)
```
<!-- slide -->
**Files WRITTEN during this workflow:**
```
Docs/???/parry-timing-design-spec.md     ❌ PATH UNDEFINED
Docs/???/parry-timing-arch-plan.md       ❌ PATH UNDEFINED
Docs/ADRs/NNN-parry-timing.md           ✅
Assets/_Project/Gameplay/*.cs            ✅ (via Implementation Agent)
Docs/???/parry-timing-test-plan.md       ❌ PATH UNDEFINED
Docs/DevLog/YYYY-MM-DD-parry-timing.md  ✅
```
````

> [!WARNING]
> **BLOCKED: 3 output artifacts have no defined storage path.** The workflow tells you to produce Design Specs, Architecture Plans, and Test Plans but never says where to save them. This breaks automation (Level 3+) because the next agent can't reliably find the previous agent's output.

---

### 🔬 Test 2: `/fix-bug` — "Flashlight drains during pause"

```
Step 1: Read bug report                          ⚠️ No standard input path (issue tracker? Discord?)
Step 2: Read ADRs/ + project-stack.md            ✅
Step 3: Reproduce                                ✅ (human step)
Step 4: Root Cause Analysis                      ✅
Step 5: Fix Implementation                       ✅
Step 6: Regression Check                         ✅ Checklist provided
Step 7: Edge Case Verification                   ✅ Checklist provided
Step 8: Commit + DevLog                          ✅
Step 9: Done                                     ✅
```

**Verdict:** ✅ Mostly clean. The fix-bug workflow is **self-contained** and well-scoped. Only gap: no formal QA agent step (unlike implement-feature, which includes one).

---

### 🔬 Test 3: `/code-review` — "Review inventory PR"

```
Step 1: Read RULES_AND_POLICY.md                 ✅
Step 2: Read project-stack.md                    ✅
Step 3: Read ADRs/README.md                      ✅
Steps 3-7: Checklist verification                ✅ Comprehensive
Step 8: Write Review                             ✅ References code-review-agent.md format
Step 9: Done                                     ✅
```

**Verdict:** ✅ Clean workflow. **But:** the `.agent/workflows/code-review.md` duplicates ~70% of the checklist in `AgentPrompts/code-review-agent.md`. The workflow should reference the agent prompt, not restate it.

---

### 🔬 Test 4: Sprint Report CI — `.github/workflows/sprint-report.yml`

```
Step 1: Checkout repo                            ✅
Step 2: Extract conventional commits             ✅ (grep for feat/fix/refactor/juice)
Step 3: Save raw report                          ✅ (Option A)
Step 4: AI-formatted report                      ⚠️ Option B commented out, no scripts/ dir exists
Step 5: Create PR                                ✅
```

**Verdict:** ⚠️ Option A works standalone. Option B references `scripts/format_sprint_report.py` which **does not exist** anywhere in the template. This is a **dead reference**.

---

## 2. Findings: Overlap / Conflict / Duplicate / Useless / Blocked / Missing

### 🟡 OVERLAPS (Redundant but not harmful — waste tokens)

| # | Finding | Files Involved | Impact |
|---|---------|---------------|--------|
| O-1 | **Code review checklist duplicated** | `.agent/workflows/code-review.md` steps 3-7 ↔ `AgentPrompts/code-review-agent.md` "Review Checklist" | Agent loads both → **~3,000 duplicate tokens** |
| O-2 | **Juice spec template duplicated** | `multi_agent_workflow_design.md` §4 ↔ `AgentPrompts/game-design-agent.md` §4 | Same template in 2 places → drift risk |
| O-3 | **Naming convention stated 3 times** | `RULES_AND_POLICY.md` §2 ↔ `implementation-agent.md` "Code Style" ↔ `code-review-agent.md` checklist | 3 copies → if you change one, you forget the others |
| O-4 | **Pipeline order described 3 times** | `multi_agent_workflow_design.md` §2 ↔ `AgentPrompts/README.md` "Pipeline Order" ↔ `.agent/workflows/implement-feature.md` steps 1-12 | 3 descriptions of the same pipeline |

---

### 🔴 CONFLICTS (Contradictory instructions)

| # | Finding | Details |
|---|---------|---------|
| C-1 | **Game Design Agent says "Mobile-first"** but GDD template says `{primary platform}` | `game-design-agent.md` line 77: "Mobile-first: Every interaction must be described for touch input" — but the GDD template is platform-agnostic. If a project is PC-first, this agent gives wrong instructions. |
| C-2 | **implement-feature step 4 has `// turbo`** on the Architect step, meaning it auto-runs — but `RULES_AND_POLICY.md` §6 says "Wait for human decision — do not proceed with assumptions." | Turbo annotation bypasses the human checkpoint that the policy demands for architecture decisions. |

---

### 🟠 DUPLICATES (Exact or near-exact copies — token waste)

| # | Finding | Resolution |
|---|---------|-----------|
| D-1 | `feature-prompt-template.md` ↔ `.github/ISSUE_TEMPLATE/feature_request.md` | ~60% overlap in structure (Systems Affected checklist, GDD Reference, Acceptance Criteria). Merge into one canonical template referenced by both. |
| D-2 | `Reports/_TEMPLATE.md` ↔ `pm-report-agent.md` output format | The PM agent prompt restates the entire report template. Should just say "Follow `Docs/Reports/_TEMPLATE.md`" (which it does in line 20, then duplicates it anyway lines 22-73). |

---

### ⚪ USELESS / LOW VALUE (Can be removed or consolidated)

| # | Finding | Reasoning |
|---|---------|-----------|
| U-1 | `multi_agent_workflow_design.md` §5 bash snippet | One-liner `git log` command doesn't warrant a whole section. The CI workflow already implements this properly. |
| U-2 | `CHANGELOG.md` "Auto-Generation" section | Lists 3 tools but no project configuration. Either commit to one tool and configure it, or remove the advice. Currently just noise. |

---

### 🔴 BLOCKED (Workflow breaks without this)

| # | Finding | Why It Blocks |
|---|---------|--------------|
| B-1 | **No `Docs/Specs/` folder** — Design Specs have no home | The implement-feature workflow (step 3) produces a Design Spec but doesn't define where to save it. At Level 3+ (automation), the Architect Agent can't find the Design Agent's output. **Blocks automation.** |
| B-2 | **No `Docs/TestPlans/` folder** — QA output has no home | Same problem for QA Agent output. Test plans float unanchored. |
| B-3 | **No `scripts/` directory** | `sprint-report.yml` Option B references `scripts/format_sprint_report.py` which doesn't exist. CI would fail if Option B is uncommented. |
| B-4 | **`project-stack.md` is all placeholders** — zero usable context | Every agent prompt says "receive `project-stack.md` as context" but the file is 100% `{placeholder}`. If an agent actually loads this, it gets zero useful context. **This is the bootstrap bottleneck.** |

---

### 🔴 MISSING (Components the workflow needs but don't exist)

| # | Finding | What's Needed |
|---|---------|--------------|
| M-1 | **No Orchestrator / Router document** | The workflow design describes an "Agent Orchestrator" (§2 diagram) but there's no file defining how to route tasks to agents. Who decides which workflow to run? No `orchestrator-agent.md` prompt exists. |
| M-2 | **No Discord/Notion integration spec** | You mentioned using Discord/Notion with your team, but no document describes how agent outputs flow to Discord (webhooks) or Notion (API sync). This is a gap for team visibility. |
| M-3 | **No "Rejection / Rework" path** | Every workflow ends with "Done — ready for human review." But what happens when the human **rejects**? No re-entry point is defined. This matters for Level 3+ where agents need to handle feedback loops. |
| M-4 | **No token budget / context window policy** | `RULES_AND_POLICY.md` §6 says "Never feed the entire repo to an agent" but doesn't define a token budget or context loading strategy. Agents still load ~8,700 tokens of context before doing anything. |
| M-5 | **No inter-agent contract schema** | The pipeline assumes Design Agent → Architect Agent → Implementation Agent, but there's no formal schema for the handoff artifact. Each agent defines its own output format independently, so there's no guarantee of compatibility. |
| M-6 | **No `refactor` workflow** | You have `/implement-feature`, `/fix-bug`, `/code-review` — but refactoring (which you do frequently per conversation history) has no dedicated workflow. Refactors have different risk profiles than features. |

---

## 3. Token Optimization Analysis

### Current Context Loading Cost Per Workflow

| Workflow | Files Loaded | Estimated Tokens | Notes |
|----------|-------------|-----------------|-------|
| `/implement-feature` | 9 files | ~8,700 | Loads ALL 6 agent prompts sequentially |
| `/fix-bug` | 3 files | ~3,250 | Lean, only loads what it needs |
| `/code-review` | 4 files | ~4,300 | Includes duplicate checklist |
| Sprint Report CI | 2 files | ~700 | Efficient — template-driven |

### Token Waste Sources

```
Source                                    Wasted Tokens    Fix
────────────────────────────────────────────────────────────────
Duplicate naming conventions (3 copies)   ~400 tokens     Single source, §-reference
Duplicate review checklist                ~750 tokens     Workflow references agent
Duplicate report template in PM prompt    ~600 tokens     Remove from prompt, reference file
Duplicate pipeline description (3x)       ~300 tokens     Single source in README
Juice spec template (2 copies)            ~200 tokens     Single source in game-design-agent
────────────────────────────────────────────────────────────────
TOTAL RECOVERABLE:                        ~2,250 tokens/run
```

### Proposed Token Optimization Strategy

#### Rule 1: **"Load-on-Demand, Not Load-All"**
```
CURRENT (implement-feature):
  Load GDD_Fighting_Allstar.md (all)           → 7,900 bytes
  Load RULES_AND_POLICY.md    → 10,300 bytes
  Load project-stack.md       → 1,500 bytes
  Load agent prompt           → 2,700 bytes
  ──────────────────────────
  TOTAL: ~22,400 bytes before any work starts

PROPOSED:
  Load project-stack.md       → 1,500 bytes  (always — it's the bootstrap)
  Load GDD @tag:{section}     → ~500 bytes   (only the relevant section)
  Load agent prompt           → 2,700 bytes  (only the active agent)
  Load RULES_AND_POLICY.md §N → ~1,500 bytes (only relevant sections)
  ──────────────────────────
  TOTAL: ~6,200 bytes — 72% reduction
```

#### Rule 2: **"Single Source of Truth, Reference Everywhere Else"**
- Naming conventions: defined **only** in `RULES_AND_POLICY.md §2`
- All agent prompts say: "Follow naming conventions in `RULES_AND_POLICY.md §2`" (no inline copy)
- Review checklists: defined **only** in the agent prompt
- Workflows say: "Run checklist from `AgentPrompts/code-review-agent.md`" (no inline copy)

#### Rule 3: **"Context Budget Per Agent Call"**

Add to `RULES_AND_POLICY.md`:
```markdown
## 8. Token Budget Policy

### Per-Agent Context Budget
| Agent Role | Max Context | Strategy |
|-----------|-------------|----------|
| Game Design | 4,000 tokens | GDD section + prompt |
| Architect | 6,000 tokens | GDD section + ADRs + prompt |
| Implementer | 8,000 tokens | Specs + affected files + prompt |
| Reviewer | 4,000 tokens | Diff + prompt (no full files) |
| QA | 4,000 tokens | Spec + implementation summary + prompt |
| PM Reporter | 2,000 tokens | Git log + template + prompt |

### Context Loading Rules
1. Always load `project-stack.md` first (~400 tokens)
2. Load only the `@tag:` section of GDD relevant to the task
3. Never load the full GDD (too large for context window)
4. Load agent prompt for the CURRENT role only (not all roles)
5. Load only the specific ADRs referenced by the task
```

---

## 4. Can This Achieve Your Target Automation Level?

### Your Current Setup vs. Required Infrastructure

| Capability | Level 2 (You're targeting) | Your Current State | Gap |
|-----------|--------------------------|-------------------|-----|
| Agent prompts defined | ✅ Required | ✅ **Done** (6 agents) | None |
| GDD as context source | ✅ Required | ⚠️ Template only (not filled) | **B-4: Fill project-stack.md** |
| Structured I/O contracts | ✅ Required | ❌ **Missing** | **M-5: Define handoff schemas** |
| Output artifact paths | ✅ Required | ❌ **Missing** | **B-1, B-2: Add Specs/, TestPlans/** |
| Rejection/rework flow | ⚠️ Nice to have | ❌ **Missing** | **M-3: Add re-entry points** |
| Token budgets | ⚠️ Nice to have | ❌ **Missing** | **M-4: Add to RULES_AND_POLICY** |
| Orchestrator definition | ❌ Level 3+ only | ❌ Not needed yet | Future work |
| Discord/Notion sync | ❌ Level 3+ only | ❌ Not needed yet | Future work |
| CI/CD triggers | ❌ Level 4 only | ⚠️ Sprint report only | Future work |

### Honest Assessment

> [!IMPORTANT]
> **Your template system is solid for Level 1 → 2 transition.** The agent prompts, templates, workflows, and policy documents are well-structured and internally consistent (with the exceptions noted above).
>
> **But Level 2 requires you to actually fill in the templates.** Right now, `project-stack.md` and `GDD_Fighting_Allstar.md` are 100% placeholders. Until those are filled, the entire system is a beautiful empty shell. The agents can't do their jobs without concrete context.
>
> **Level 3 (automation) is blocked** by the missing output paths (Specs/, TestPlans/) and the missing inter-agent handoff contracts. Without these, you can't automate the pipeline because each agent doesn't know where to find the previous agent's work.

### For Your Specific Setup (Discord/Notion Team + Solo Dev + AI Agents)

Your workflow assumes a **solo developer + AI agents** model. With a team on Discord/Notion, you need to add:

1. **Discord webhook integration points** — Where in the pipeline does Discord get notified? (After PR? After QA? After report?)
2. **Notion sync strategy** — Does the GDD live in Notion and get synced to markdown? Or does markdown remain the source of truth?
3. **Team review vs. AI review** — The code-review workflow assumes self-review via AI. With a team, you need to distinguish between AI pre-review and human team review.

---

## 5. Proposed Changes

### New Files to Create

| File | Purpose | Priority |
|------|---------|----------|
| `Docs/Specs/` (directory) | Home for Design Specs and Architecture Plans | 🔴 P0 — Unblocks pipeline |
| `Docs/TestPlans/` (directory) | Home for QA test plans | 🔴 P0 — Unblocks pipeline |
| `Docs/RULES_AND_POLICY.md` §8 | Token Budget Policy (new section) | 🟡 P1 — Optimization |
| `.agent/workflows/refactor.md` | Dedicated refactoring workflow | 🟡 P1 — Missing workflow |
| `Docs/Handoff-Contracts/` | Inter-agent I/O schemas | 🟡 P1 — Enables Level 3 |
| `scripts/format_sprint_report.py` | AI report formatter (stub) | 🔵 P2 — Unblocks CI Option B |

### Files to Edit

| File | Change | Priority |
|------|--------|----------|
| `AgentPrompts/game-design-agent.md` | Remove hardcoded "Mobile-first", use `{primary platform}` | 🔴 P0 — Conflict fix |
| `AgentPrompts/pm-report-agent.md` | Remove duplicate template (lines 22-73), keep reference to `Reports/_TEMPLATE.md` | 🟡 P1 — Token savings |
| `.agent/workflows/code-review.md` | Replace inline checklist with reference to `AgentPrompts/code-review-agent.md` | 🟡 P1 — Token savings |
| `.agent/workflows/implement-feature.md` | Remove `// turbo` from step 4 (conflicts with policy), add output paths for specs/test plans | 🔴 P0 — Conflict + blocker fix |
| `multi_agent_workflow_design.md` | Remove duplicate juice spec template, reference `game-design-agent.md` | 🔵 P2 — Cleanup |
| `RULES_AND_POLICY.md` | Add §8 Token Budget Policy | 🟡 P1 |

### Files That Could Be Merged

| Merge From | Merge Into | Rationale |
|-----------|-----------|-----------|
| `GDD/AI-Context/feature-prompt-template.md` | `.github/ISSUE_TEMPLATE/feature_request.md` | ~60% overlap. Keep GitHub template as canonical, add a "For AI agents, use this template" note. |

---

## 6. Verification Plan

### Automated Validation Checklist

After applying the proposed changes, re-run this simulation:

- [ ] Every workflow step has a defined output path
- [ ] No file is referenced that doesn't exist
- [ ] No duplicate content blocks > 200 tokens exist across files
- [ ] `// turbo` annotations don't conflict with `RULES_AND_POLICY.md` checkpoints
- [ ] `{placeholder}` tokens in agent prompts match GDD template `{placeholder}` tokens
- [ ] Every agent prompt's "Output Format" matches the next agent's "Input" description
- [ ] Token budget per workflow ≤ defined limit

### Manual Verification

- [ ] Fill in `project-stack.md` for your Dog project, then dry-run `/implement-feature` with a real feature
- [ ] Have a team member on Discord follow the workflow independently — note where they get stuck
- [ ] Run `sprint-report.yml` with `workflow_dispatch` on a repo with real commits

---

## Open Questions

> [!IMPORTANT]
> **Q1: Where does your GDD live — Markdown in repo or Notion?**
> This determines whether we need a Notion→Markdown sync step, or if Notion is just for team discussion and the `.md` file is the source of truth.

> [!IMPORTANT]
> **Q2: Do you want AI agents to post to Discord automatically?**
> If yes, we need to add webhook URLs and notification points to the workflow. If no, Discord remains purely human coordination.

> [!IMPORTANT]
> **Q3: Should the `/refactor` workflow be separate, or is `/implement-feature` sufficient?**
> Your conversation history shows frequent refactoring (StatKeys conflict, inventory merge, scene decoupling). These have different risk profiles than new features. Separate workflow recommended.

> [!IMPORTANT]
> **Q4: Do you want to proceed with implementing these fixes, or review the findings first?**
> I can apply the P0 changes (conflict fixes, missing directories, output paths) immediately, or you can review this analysis first.
