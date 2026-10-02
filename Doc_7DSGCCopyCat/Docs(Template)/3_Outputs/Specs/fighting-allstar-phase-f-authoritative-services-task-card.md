---
slug: fighting-allstar-phase-f-authoritative-services
status: in-progress
source: manual
gdd_tags: [architecture, roadmap, economy, dungeons]
owner: codex
human_checkpoint: required-for-provider-and-host
next_agent: codex
blocked_by: [hosting-provider-decision, authentication-provider-configuration]
---

# Phase F task card — authoritative services

## Goal

Replace the Phase A–E local fakes with an authenticated service boundary while retaining the shared Core as the only battle and run rules implementation. Online requests must derive account identity from verified authentication, use idempotency IDs and expected revisions, persist economic/run mutations atomically, and return recipient-specific projections.

## Current Phase F slice

- Added `Shared/FightingAllstar.Contracts` as a portable, provider-neutral DTO assembly.
- Defined command envelopes with request ID and expected revision. The authenticated subject is intentionally absent from client command bodies.
- Added stable request fingerprinting for binding an idempotency key to an operation, revision, and canonical payload.
- Defined a transactional document-store port that compares versions and applies writes atomically. Provider adapters must prepare deterministic outcomes before commit retries.
- Added a recipient-scoped battle projection in Core. It carries only the recipient's own hand and omits Core RNG state; opponent hand size remains visible.
- Added a host-neutral server application service for authenticated battle-plan submission. It validates bearer tokens through an injected identity adapter, checks persisted match membership and turn ownership, invokes `BattleEngine`, and atomically saves the new state with its idempotency receipt through a repository port.
- Preserved the local practice authority as a development harness. It does not grant online currency or entitlements.

## Next implementation work

1. Implement a provider token validator and transactional match repository, then expose the command service through the approved HTTP host.
2. Add equivalent trusted command services for runs, settlements, summons, and guarantee entitlements.
3. Implement durable run/profile repositories and receipt recovery against an approved store.
4. Replace Unity local adapters through composition with remote clients and explicit offline-practice mode.
5. Add authenticated Windows/APK configuration and the Phase F acceptance evidence from the implementation plan.
6. Begin Phase G AI work only after the Phase F authority boundary is deployed and validated.

## Owner decisions still required

- Approve the hosting target and server runtime. The existing architecture document recommends Cloud Run behind Cloudflare, but labels that proposal pending review.
- Approve the authentication provider setup and provide configuration through the normal project secret/configuration process. No credentials belong in source or Unity content.
- Approve the persistence provider, project/region, and operational transaction limits. Firestore is described as the proposed store, not an approved deployment decision.

No provider SDK, endpoint, credential, cloud resource, Unity package, or build setting has been added in this slice.

## Validation and evidence

The Phase F acceptance gate remains: authenticated online PvE on Windows and APK; concurrent-spend and replay behavior; lost-receipt recovery; rejection of client-supplied results; and private two-seat battle parity. This task-card update records the implementation boundary only; it does not claim those acceptance checks have passed.
