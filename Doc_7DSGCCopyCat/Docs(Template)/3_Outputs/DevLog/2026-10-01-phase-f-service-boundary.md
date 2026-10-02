# Phase F service boundary — 2026-10-01

Started Phase F after reviewing the implementation plan, Phase E playable-refactor task card, existing Phase A–E code, and architecture proposal.

## Implemented

- Added provider-neutral `FightingAllstar.Contracts` DTO project.
- Added command envelope, command/result receipt shapes, and canonical SHA-256 request fingerprinting.
- Added verified-identity DTO and atomic compare-and-commit document-store interface. Client request models contain no account subject field.
- Added `BattleProjectionBuilder` in the shared Core to create side-scoped views without exposing the authoritative state object, the opponent hand, or Core RNG state. Opponent card-draw events omit the drawn card ID.
- Added `Server/FightingAllstar.Server.Application`, a host-neutral application layer. Its `BattleCommandService` validates identity through an injected token validator, enforces match membership and acting side, applies plans through `BattleEngine`, fingerprints requests, and delegates atomic state-plus-receipt commits to `IBattleCommandRepository`.
- Added the Phase F task card with the remaining provider/host decisions and acceptance evidence.

## Not implemented or claimed

No authentication SDK, persistence SDK implementation, server host, gateway, credentials, cloud resources, Unity package, build setting, or service deployment was introduced. The architecture plan explicitly marks Cloud Run/Cloudflare/Firebase/Firestore as proposed pending approval; Phase F's authenticated Windows/APK and transaction/recovery acceptance evidence remains outstanding. Phase G depends on completing those Phase F gates.

No tests, Unity build, or runtime verification was run in this slice.
