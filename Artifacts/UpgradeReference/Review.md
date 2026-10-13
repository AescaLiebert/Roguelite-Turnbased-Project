# Upgrade and instance tooltip review

Native Unity UI Toolkit captures were generated in an isolated preview project at 1920×1080 and 1280×720. No portrait review was performed. No gameplay tests or player-account writes were performed.

## Artifact instances

- The +level badge follows the artifact name in the title panel.
- The colored preview panel contains slot/type, the main-stat name/value, icon, and rarity stars.
- The parchment panel contains bullet substats, structured set effects, ownership when equipped, and favourite protection.
- Rarity R and SSR examples include +0 and +4; set-basket previews retain the stat-free preview layout.

## Character upgrades

- One panel displays CC, ATK, DEF, HP and level/cap.
- Below cap: next-level values and Coin cost; Level Up action.
- At cap: cap comparison, phase/sub-phase/series requirements, Coin cost; Limit Break action. Stats remain unchanged under existing progression rules.
- Missing resources use red frames/counts and disable the action.
- At maximum level: no resource cost section and a disabled Max Level action.

All 12 captures completed. The isolated Unity project compiled without C# errors; existing serialization/deprecation warnings were emitted. The main Unity MCP connection was unavailable during this update, so live-editor console verification was not performed.
