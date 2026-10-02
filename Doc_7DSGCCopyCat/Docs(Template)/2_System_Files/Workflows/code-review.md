---
description: Code review workflow using targeted project conventions
---

# Code Review Workflow

## Steps

### 1. Load Context
 
- Read `Docs/1_Inputs_Templates/project-stack.md` for project context.
- Check `Docs/3_Outputs/ADRs/README.md` for relevant architecture decisions.
- Load the diff or PR being reviewed.
- If a task card, design spec, architecture plan, or test plan exists, load only the artifact for this slug.
- Read only the relevant `Docs/0_User_Manual/RULES_AND_POLICY.md` sections:
  - Section 2 for naming and documentation style.
  - Section 4 if scene hierarchy changed.
  - Section 5 for performance and optimization.
  - Sections 6-7 for agent behavior and safety.
 
### 2. Identify Scope
 
- What files/diff are being reviewed?
- What feature, bugfix, or refactor does this implement?
- What GDD `@tag:` section does it relate to?
- Which approved spec or ADR should this follow?
 
### 3. Run the Review Checklist
 
Load `2_System_Files/Agent_Prompts/code-review-agent.md` and run through the **Review Checklist** section.
 
Key areas:
 
- Architecture: ADR compliance, god-object check, state/presentation separation.
- Naming and conventions: `Docs/0_User_Manual/RULES_AND_POLICY.md` Section 2 compliance.
- Performance: `Docs/0_User_Manual/RULES_AND_POLICY.md` Section 5 compliance, no hot-path allocations.
- Hierarchy: `Docs/0_User_Manual/RULES_AND_POLICY.md` Section 4 compliance if scene objects changed.
- Edge cases: null safety, rapid input, scene transitions.
 
### 4. Write Review
 
Use the format from `2_System_Files/Agent_Prompts/code-review-agent.md`:

- Summary: approve, request changes, or block.
- Issues with severity.
- Positive feedback.

### 5. Done

Review is ready for the developer to act on.
