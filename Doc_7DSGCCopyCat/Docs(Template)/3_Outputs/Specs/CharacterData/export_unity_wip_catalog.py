"""Export the reviewed WIP roster into Unity's authoring catalog schema.

This preserves source text and provenance. It deliberately emits no executable
effects and keeps every character RuntimeReady=false.
"""
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
DEFAULT_SOURCE = Path(__file__).resolve().with_name("wip-character-drafts.json")
DEFAULT_OUTPUT = ROOT / "Assets/FightingAllstar/Content/WipCharacterCatalog.json"
STAT_MAP = {
    "Class_Combat": ("CombatClass", "points"), "Attack": ("Attack", "points"),
    "Defense": ("Defense", "points"), "Health": ("MaxHealth", "points"),
    "Pierce_Rate": ("PierceBp", "percent"), "Resistance": ("ResistanceBp", "percent"),
    "Regeneration": ("RegenerationBp", "percent"), "Critical_Chance": ("CritChanceBp", "percent"),
    "Critical_Damage": ("CritDamageBp", "percent"), "Critical_Resistance": ("CritResistanceBp", "percent"),
    "Critical_Defense": ("CritDefenseBp", "percent"), "Recovery_Rate": ("RecoveryBp", "percent"),
    "Block_Chance": ("BlockChanceBp", "percent"), "Block_Power": ("BlockPowerBp", "percent"),
    "Lifesteal": ("LifeStealBp", "percent"), "Avoidance_Rate": ("AvoidanceBp", "percent"),
    "Evade_Rate": ("EvadeBp", "percent"), "Control_Rate": ("ControlBp", "percent"),
    "Perception_Rate": ("PerceptionBp", "percent"),
}
PROVENANCE = {
    "source": 0, "source-rank3": 0, "source-base-normalized-to-C0": 0,
    "guide-page-6-default": 0, "generated-starting-value": 3,
    "generated-starting-value-with-source-riders": 3,
}


def slug(value):
    return re.sub(r"[^a-z0-9]+", "-", value.strip().lower()).strip("-")


def provenance(value):
    return PROVENANCE.get(value, 2)


def convert(character):
    stats = character["stats"]
    normalized = {}
    source_stats = []
    for source_field, (stat_id, unit) in STAT_MAP.items():
        source = stats[source_field]
        value = float(source["value"])
        internal_value = int(round(value * 100)) if unit == "percent" else int(round(value))
        normalized[stat_id] = internal_value
        source_stats.append({"StatId": stat_id, "SourceField": source.get("sourceField", source_field),
                             "SourceValue": value, "Unit": source.get("unit", unit),
                             "Provenance": source["provenance"], "Value": internal_value})

    definition_id = character["definitionId"]
    skills = []
    for card in character["cards"]:
        ranks = []
        for rank in card["ranks"]:
            ranks.append({"Rank": rank["rank"], "Effect": None, "DescriptionKey": "",
                          "SourceDescription": rank["description"], "Provenance": provenance(rank["provenance"])})
        skills.append({"Id": f"{definition_id}.card.{card['slot']}", "Slot": card["slot"],
                       "SourceTarget": card["sourceTarget"], "SourceType": card["sourceType"],
                       "SourceEffectTags": card["sourceEffectTags"], "Ranks": ranks})

    tiers = [{"Tier": tier["tier"], "SourceLabel": tier["label"], "SourceDescription": tier["description"],
              "Effect": None, "Provenance": provenance(tier["provenance"])}
             for tier in character["constellations"]]
    passive = character["passive"]
    relic = character.get("holyRelic")
    base_stats = {"Attack": normalized["Attack"], "Defense": normalized["Defense"],
                  "MaxHealth": normalized["MaxHealth"], "CombatClass": normalized["CombatClass"],
                  "PierceBp": normalized["PierceBp"], "ResistanceBp": normalized["ResistanceBp"],
                  "RegenerationBp": normalized["RegenerationBp"], "CritChanceBp": normalized["CritChanceBp"],
                  "CritDamageBp": normalized["CritDamageBp"], "CritResistanceBp": normalized["CritResistanceBp"],
                  "CritDefenseBp": normalized["CritDefenseBp"], "RecoveryBp": normalized["RecoveryBp"],
                  "BlockChanceBp": normalized["BlockChanceBp"], "BlockPowerBp": normalized["BlockPowerBp"],
                  "LifeStealBp": normalized["LifeStealBp"], "AvoidanceBp": normalized["AvoidanceBp"],
                  "EvadeBp": normalized["EvadeBp"], "ControlBp": normalized["ControlBp"],
                  "PerceptionBp": normalized["PerceptionBp"]}
    return {
        "SchemaVersion": 1, "RuntimeReady": False, "Id": definition_id,
        "SourceId": f"source.{character['sourceId']}", "SourceRecord": character["sourceRecord"],
        "DisplayName": character["name"], "FamilyId": f"family.{slug(character['familyId'])}",
        "Role": character["role"], "SeriesId": "series.kof",
        "AttributeId": f"attribute.{slug(character['attribute'])}", "RarityId": f"rarity.{slug(character['rarity'])}",
        "TraitIds": [f"trait.{slug(x)}" for x in character["traits"]] + ["trait.series-kof"], "SourceTraits": character["traits"],
        "BaseStats": base_stats, "StatsProvenance": 0 if all(x["provenance"] in ("source", "guide-page-6-default") for x in stats.values()) else 3,
        "SourceStats": source_stats, "Skills": skills,
        "PassiveId": f"{definition_id}.passive.source",
        "PassiveSource": {"SourceType": passive["sourceType"], "Description": passive["description"],
                          "Restriction": passive["restriction"], "Provenance": "source"},
        "HolyRelicSource": None if relic is None else {"Description": relic["description"],
            "EnabledInPrototype": relic["enabledInPrototype"], "Provenance": "source"},
        "ReviewNotes": character.get("reviewNotes", []), "UltimateTiers": tiers,
    }


def main():
    source_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_SOURCE
    output_path = Path(sys.argv[2]) if len(sys.argv) > 2 else DEFAULT_OUTPUT
    raw = source_path.read_bytes()
    source = json.loads(raw.decode("utf-8"))
    catalog = {"SchemaVersion": 1, "ContentVersion": source["schemaVersion"],
               "ContentHash": hashlib.sha256(raw).hexdigest(),
               "Characters": [convert(character) for character in source["characters"]]}
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    ranks = sum(len(card["ranks"]) for character in source["characters"] for card in character["cards"])
    tiers = sum(len(character["constellations"]) for character in source["characters"])
    print(f"Exported {len(catalog['Characters'])} WIP characters, {ranks} card ranks, {tiers} constellation tiers to {output_path}")


if __name__ == "__main__":
    main()
