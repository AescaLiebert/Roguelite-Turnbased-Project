# Cassandra kit checks

Run `Fighting Allstar/Content/Configure SCII Cassandra` in Unity to update the existing character assets.
Export `CharacterObjectCatalogBuilder.CreateDefinition(character)` with `SnapshotJson.Serialize` to `Temp/CassandraChecks/character.json`.
Compile `Program.cs` as a .NET 8 console application referencing `Shared/FightingAllstar.Core/FightingAllstar.Core.csproj`; pass the exported JSON path as the first argument.

Checks exercise the authored kit through the combat engine, including all C0–C5 Ultimates, attack-only 12% ATK, DEF snapshots after Skill 2, buff refresh and expiry, single-target isolation, AOE damage, vulnerability and Debuff-card seal.

Design choices: all damage coefficients and durations come from the requested kit. Electrified is the specified 40% damage vulnerability without an extra unspecified damage-over-time tick. Crit protection is Crit Resistance. The DEF conversion snapshots current DEF after each damaging card, refreshes without stacking, and has no cooldown. The Ultimate buffs Cassandra before dealing damage. Its self buff and enemy card seal each last two turns.

Playtest: inspect the card tooltip before using each skill; verify one target for Skill 1 and all living enemies for Skill 2/Ultimate. Use Skill 2 followed by Skill 1 to observe the DEF-to-ATK synergy and a single refreshed buff. Confirm enemy Debuff cards are blocked while other categories remain usable. Pass turns to confirm buffs/seals expire. Repeated clicks during playback should follow the existing action queue and produce one passive activation per card. Confirm the usual damage text, status badges, hit feedback and audio remain readable during AOE playback.
