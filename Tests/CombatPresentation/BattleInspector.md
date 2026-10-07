# Battle character inspector

Hold an active character for 0.45 seconds during player planning. Both teams are inspectable; allied models below the former 40% screen cutoff are supported. Only actual card controls block holding. Picking checks fighter colliders and a small screen-space proximity fallback; hidden reserve models are excluded. Team portraits include SUB and defeated members. Close with X or Escape. Card planning is blocked while the inspector is open.

## Presentation

- Overview: primary stat tiles, current HP/shield, two-column secondary stats, own passive. Zero changes have no modifier label. Increases are green, decreases red; totals remain their real combat values.
- Effects: buffs/debuffs and passive contributions use the same compact row design, with source ownership, description, and portrait. Status icons reuse CoreFighterHud's authored StatusIcon prefab and duration overlay. Rows grow to fit long descriptions. Both sections scroll when needed.
- Cards: select a skill or ultimate, then inspect skill ranks 1–3 or ultimate levels 0–6 in a separate compact popup. The popup calls the same card tooltip detail builder as the hand cards, including card type, highlighted description, extracted status icons, keyword explanations, and character portrait. Missing authored levels are identified explicitly. Existing card art is used, with the character portrait as fallback.

Stats use StatusSystem.GetEffectiveStats over the same committed snapshot used by combat. Basic modifiers are relative percentage changes (flat differences when base is zero); secondary modifiers are percentage-point changes. StatusVisualData descriptions take priority; missing text is derived from status rules.

King's authored passive uses TeamTurnEnded, matching its source description and Core fallback. LivingRoster allows SUB effects. Refresh removes derived contributions from defeated owners.

## Verification

- `Tests/CombatPresentation/Run.ps1`: 7,002 assertions, including SUB turn counts, five-stack cap, both supported passive clocks, and active/reserve owner death cleanup.
- `Temp/CardKindCompile/Runtime.csproj`: compile against the installed Unity assemblies.
- `InspectorPreviewChecks.cs`: isolated Unity fixture using actual inspector/HUD/picking scripts and prefab. Execute `InspectorPreviewChecks.Run` in the separate preview project. Includes 25 checks for totals, negative coloring, hidden zero changes, matching status/passive rows, prefab cooldown overlay, both skills' three ranks, seven ultimate levels, SUB selection, close cleanup, and allied/enemy collider picking. Landscape screenshots use fixture stats and descriptions with real project artwork; they are not captures of a live encounter.
- Fixture dependencies: BattleCharacterInspector, BattleInspectorPicking, CoreFighterHud, WorldBillboardFollower, StatusVisualData, Core/Contracts DLLs, UGUI 2.0.0, project TMP resources, FighterAttribute enum, StatusIcon prefab/cooldown sprite, King portrait, Attack buff sprite. Art paths in the fixture are local to the preview project.
- Evidence: `Logs/BattleInspectorEvidence/*landscape.png`.
- Device check: short taps select without opening; stationary holds open without selecting; drags and multitouch cancel. Test actual battle UI hit areas and character positions. Starting hold threshold is 0.45 seconds, with 24 screen pixels of drag tolerance; increase threshold for accidental opens, decrease it if deliberate holds feel slow.

