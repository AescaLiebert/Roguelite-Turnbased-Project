# Artifact hub refinement

Reviewed native UI Toolkit captures at 1920×1080 and 1280×720. No portrait captures or gameplay tests were run.

- The artifact tab retains CharacterHub’s existing portrait and character header. Its authored six-button slot group is reparented onto the hub hero; there is no second artifact portrait.
- Artifact Inspect uses the shared tooltip Name / Preview / Content builder and rarity/paper palette. Its preview contains Change, Equip-or-Unequip, and Enhance, plus a favourite property icon. The separate equipment dock and Slots action are removed.
- The active-set overview contains only active stat bonuses; it is hidden if no set has two pieces. Detailed item set thresholds remain in the inspector, using the tooltip’s green/grey feedback.
- Header Back uses an owner stack for inventory → fighter details → enhancement. Closing a child view restores the previous header action. Removed inventory and enhancement Back controls no longer have stale UI queries.
- Compact content shows all four substats before scrolling for remaining set/ownership details.

16 captures cover empty slots, one equipped piece without an active set, two active sets, a four-piece set, unequipped inspection, enhancement, and returning to inventory. The fixture uses detached sample equipment; no live account is equipped, upgraded or saved. The isolated Unity project compiled without C# errors. Existing serialization/deprecation warnings remain; live-editor verification was unavailable.
