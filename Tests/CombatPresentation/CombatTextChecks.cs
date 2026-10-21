#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Content;
using UnityEditor;
using UnityEngine;

public static class CombatTextChecks
{
    private static int assertions;
    private static readonly List<string> Failures = new List<string>();
    private static void Check(bool passed, string message)
    { assertions++; if (!passed) Failures.Add(message); }
    private static string Plain(string text) => Regex.Replace(text ?? "", "<[^>]+>", "");

    public static void Run()
    {
        var report = new StringBuilder("# Combat description audit\n\n");
        try
        {
            Check(CombatDetailFormatter.BuildKeywordsExplanation("Increases allies' Recovery Rate by 40%.") == "", "Recovery Rate must not create a status glossary.");
            Check(CombatDetailFormatter.BuildKeywordsExplanation("Increases Pierce Rate by 20%.") == "", "Pierce Rate must not create a Pierce glossary.");
            Check(Regex.Matches(CombatDetailFormatter.BuildKeywordsExplanation("Weak Point and Weakpoint; Removes Buffs and Remove Buff."), "※").Count == 2,
                "Keyword aliases must share one explanation.");
            Check(CombatDetailFormatter.KeywordExplanations["Cleave"].Contains("cannot critically"), "Cleave explanation must match its runtime rule.");
            Check(CombatDetailFormatter.KeywordExplanations["Quell"].Contains("Stance"), "Quell must describe Stance scaling, not PG loss.");
            Check(!CombatTextCatalog.Highlight("Disable Attack; Pierce Rate; Recovery Rate").Contains("<b>Disable <"), "Compound terms must not contain nested token highlighting.");
            Check(CombatTextCatalog.CanonicalKeyword("attack.co-destruction") == "Co-Destruction", "Attack IDs must resolve canonical names.");
            Check(CombatTextCatalog.CanonicalKeyword("attack.power-strike") == "Power Strike", "Multi-word attack IDs must resolve canonical names.");
            foreach (var attack in StandardEffectDatabase.CreateAttackEffects())
                Check(CombatDetailFormatter.KeywordExplanations.ContainsKey(CombatTextCatalog.CanonicalKeyword(attack.Id)), attack.Id + ": source-backed attack keyword missing from glossary.");
            var colored = CombatTextCatalog.Highlight("Disable Attack; Recovery Rate 40%; Pierce Rate 20% for 2 turns");
            Check(CombatTextCatalog.Highlight(colored) == colored, "Repeated formatting must preserve rich text without nesting new colors.");
            var beforeDamage = new EffectDefinition { Kind = EffectKind.Damage, CoefficientBp = 30000 };
            beforeDamage.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.BeforeAction,
                Effect = new EffectOperationDefinition { Kind = EffectKind.RemoveBuffs } });
            beforeDamage.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterAction,
                Effect = new EffectOperationDefinition { Kind = EffectKind.ChangePowerGauge, PowerGaugeAmount = -1 } });
            var ordered = CardDescriptionText.Format("Removes Buffs, then deals 300% ATK damage to one enemy, then decreases their Power Gauge.", beforeDamage);
            Check(ordered.StartsWith("Remove Buffs from ") && ordered.Contains(", then inflict damage equal to 300% of the hero's Attack on one enemy"), "Pre-action effects must lead attacking cards.");
            Check(ordered.Contains(", then decrease "), "After-action effects must follow damage.");
            var sharedTargetSkill = new SkillDefinition { Id = "shared-target", Slot = 1, TargetScope = EffectTargetScope.AllEnemies };
            sharedTargetSkill.Ranks.Add(new SkillRankDefinition { Rank = 1, TargetScope = EffectTargetScope.AllEnemies,
                Effect = new EffectDefinition { Kind = EffectKind.Damage, Target = EffectTargetScope.AllEnemies, Scaling = StatScaling.Attack, CoefficientBp = 16000 } });
            sharedTargetSkill.Ranks.Add(new SkillRankDefinition { Rank = 2, TargetScope = EffectTargetScope.SelectedEnemy,
                Description = "Deals 240% Attack damage to one enemy.",
                Effect = new EffectDefinition { Kind = EffectKind.Damage, Target = EffectTargetScope.SelectedEnemy, Scaling = StatScaling.Attack, CoefficientBp = 24000 } });
            sharedTargetSkill.Ranks.Add(new SkillRankDefinition { Rank = 3, TargetScope = EffectTargetScope.SelectedEnemy,
                Description = "Deals 325% Attack damage to one enemy.",
                Effect = new EffectDefinition { Kind = EffectKind.Damage, Target = EffectTargetScope.SelectedEnemy, Scaling = StatScaling.Attack, CoefficientBp = 32500 } });
            var sharedTargetOwner = new FighterState { Definition = new CharacterDefinition { Skills = new List<SkillDefinition> { sharedTargetSkill } } };
            foreach (var rank in sharedTargetSkill.Ranks.Where(r => r.Rank > 1))
            {
                var details = CombatDetailFormatter.ExtractCardDetails(new CardState { Kind = CardKind.Skill, SkillId = sharedTargetSkill.Id, Rank = rank.Rank }, sharedTargetOwner, null);
                var text = Plain(details.desc);
                Check(text.Contains("on all enemies") && !text.Contains("on one enemy"), "Skill root target must replace stale R" + rank.Rank + " target wording.");
                Check(rank.Effect.Target == EffectTargetScope.SelectedEnemy, "Description formatting must not mutate the authored rank effect.");
            }
            var normalizedSharedTarget = sharedTargetSkill.Clone();
            CardRules.ApplySharedSkillSettings(normalizedSharedTarget);
            Check(normalizedSharedTarget.Ranks.Where(r => r.Rank > 1).All(r => r.TargetScope == EffectTargetScope.AllEnemies && r.Effect.Target == EffectTargetScope.AllEnemies),
                "Normalizing a skill must synchronize every rank to the skill-root target.");
            var mentioned = new FighterState { Definition = new CharacterDefinition { Skills = new List<SkillDefinition> {
                new SkillDefinition { Id = "test", Ranks = new List<SkillRankDefinition> { new SkillRankDefinition { Rank = 1,
                    Description = "Deals 100% ATK damage. Removes Ignite from the target.", Effect = new EffectDefinition { CoefficientBp = 10000 } } } } } } };
            Check(CombatDetailFormatter.ExtractCardDetails(new CardState { SkillId = "test", Rank = 1 }, mentioned, null).statuses.Count == 0,
                "Mentioning/removing a status in prose must not advertise that the card applies it.");
            foreach (var scaling in new[] { StatScaling.Attack, StatScaling.Defense, StatScaling.MaxHealth, StatScaling.SpecificStat })
            {
                var effect = new EffectDefinition { Kind = EffectKind.Damage, Scaling = scaling, ScalingStat = StatId.Pierce,
                    CoefficientBp = 12345, KeywordId = "attack.spike", Target = EffectTargetScope.SelectedEnemy };
                Check(CardDescriptionText.DamageSentence(effect).StartsWith("Inflict Spike damage equal to 123.45% of the hero's "), "Damage wording/scaling must come from executable data.");
            }
            StatusVisualData.ResetRegistry();
            StatusVisualData.InitializeRegistry();
            var down = StatusVisualData.Get("Recovery Rate", StatusPolarity.Debuff);
            Check(down != null && down.Polarity == StatusPolarity.Debuff, "Recovery debuff art must remain discoverable.");
            Check(StatusVisualData.Get("Recovery Rate", StatusPolarity.Buff)?.Polarity != StatusPolarity.Debuff, "A Recovery buff must never use debuff art.");
            Check(StatusVisualData.GetSprite("Increase Recovery Rate", StatusPolarity.Buff) != down?.Icon, "Recovery up/down sprite fallback must differ.");
            var rejuvenation = StatusVisualData.Get("status.buff.rejuvenation", StatusPolarity.Neutral);
            Check(rejuvenation != null && rejuvenation.Polarity == StatusPolarity.Neutral && rejuvenation.Icon != null, "Neutral Rejuvenation art must remain exact.");
            var missingElementIcon = ScriptableObject.CreateInstance<StatusVisualData>();
            missingElementIcon.SetData("status.element.missing-icon-check", "Missing Element Icon", null, StatusPolarity.Neutral, Array.Empty<string>());
            StatusVisualData.Register(missingElementIcon);
            var statDown = StatusVisualData.Get("stat_down", StatusPolarity.Debuff);
            Check(statDown?.Icon != null && StatusVisualData.GetSprite("status.element.missing-icon-check", StatusPolarity.Neutral) == statDown.Icon,
                "Elemental statuses without dedicated art must use the normal StatDown debuff icon.");
            UnityEngine.Object.DestroyImmediate(missingElementIcon);
            StatusVisualData.ResetRegistry();
            StatusVisualData.InitializeRegistry();
            var captionRule = typeof(CoreBattleSceneController).GetMethod("ShouldShowReactionCaption", BindingFlags.NonPublic | BindingFlags.Static);
            Check(captionRule != null, "Reaction caption rule is missing.");
            foreach (var reaction in new[] { ElementalReactionId.Superconduct, ElementalReactionId.ElectroCharged, ElementalReactionId.Burning })
            {
                var repeated = new BattleEvent { ElementalReaction = reaction };
                Check(!(bool)captionRule.Invoke(null, new object[] { repeated }), reaction + " should hide its caption when no condition was added.");
                repeated.ElementalReactionStatusApplied = true;
                Check((bool)captionRule.Invoke(null, new object[] { repeated }), reaction + " should show its caption when a condition was added.");
            }
            Check(!(bool)captionRule.Invoke(null, new object[] { new BattleEvent { ElementalReaction = ElementalReactionId.Burning,
                ElementalReactionStatusApplied = true, ElementalReactionStatusAlreadyActive = true } }),
                "An added Burning stack must not repeat the caption while Burning is already active.");
            Check((bool)captionRule.Invoke(null, new object[] { new BattleEvent { ElementalReaction = ElementalReactionId.Vaporize } }),
                "One-shot reaction captions must remain visible.");

            void CheckStatusIcon(StatusRecipeDefinition recipe, string artAsset, int potency = 0)
            {
                var effect = new EffectDefinition { Kind = EffectKind.ApplyStatus, StatusRecipe = recipe, StatusPotencyBp = potency };
                var fighter = new FighterState { Definition = new CharacterDefinition { Skills = new List<SkillDefinition> {
                    new SkillDefinition { Id = "icon-check", Ranks = new List<SkillRankDefinition> {
                        new SkillRankDefinition { Rank = 1, Effect = effect } } } } } };
                var details = CombatDetailFormatter.ExtractCardDetails(new CardState { Kind = CardKind.Skill, SkillId = "icon-check", Rank = 1 }, fighter, null);
                var expected = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/" + artAsset + ".asset");
                Check(expected?.Icon != null && details.statuses.Count == 1 && details.statuses[0].Icon == expected.Icon,
                    artAsset + ": tooltip must use the authored sprite, not a generic fallback or opposite effect.");
            }
            void CheckModifierIcon(StatModifierDefinition modifier, StatusPolarity polarity, string artAsset, int potency = 0)
            {
                CheckStatusIcon(new StatusRecipeDefinition { Id = "test.inline.modifier", Behavior = StatusBehavior.Stat,
                    Polarity = polarity, Modifiers = new List<StatModifierDefinition> { modifier } }, artAsset, potency);
            }
            CheckModifierIcon(new StatModifierDefinition { Stat = StatId.Attack, Amount = 2000 }, StatusPolarity.Buff, "IncreaseATK");
            CheckModifierIcon(new StatModifierDefinition { Stat = StatId.Attack, Amount = -2000 }, StatusPolarity.Debuff, "DecreaseATK");
            CheckModifierIcon(new StatModifierDefinition { Stat = StatId.Defense, Amount = 2000 }, StatusPolarity.Buff, "IncreaseDEF");
            CheckModifierIcon(new StatModifierDefinition { Stat = StatId.MaxHealth, Amount = 2000 }, StatusPolarity.Buff, "IncreaseHP");
            CheckModifierIcon(new StatModifierDefinition { Stat = StatId.Pierce, Amount = 2000 }, StatusPolarity.Buff, "IncreasePierce");
            CheckModifierIcon(new StatModifierDefinition { Target = ModifierTarget.StatBundle, Bundle = StatBundleKind.AttackRelated, Amount = 2000 }, StatusPolarity.Buff, "IncreaseATK-Related");
            CheckModifierIcon(new StatModifierDefinition { Target = ModifierTarget.StatBundle, Bundle = StatBundleKind.HpRelated, Amount = 2000 }, StatusPolarity.Buff, "IncreaseHP-Related");
            CheckModifierIcon(new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Amount = 5000 }, StatusPolarity.Buff, "IncreaseDamageDealt");
            CheckModifierIcon(new StatModifierDefinition { Target = ModifierTarget.AnyDamageReceived, Amount = 4000 }, StatusPolarity.Debuff, "IncreaseDamageTaken");
            CheckModifierIcon(new StatModifierDefinition { Target = ModifierTarget.FlatDamageReduction, Amount = 5000 }, StatusPolarity.Buff, "RuduceDamageTaken");
            CheckModifierIcon(new StatModifierDefinition { Stat = StatId.Attack, Operation = ModifierOperation.Multiplier, Amount = 8000 }, StatusPolarity.Debuff, "DecreaseATK");
            CheckModifierIcon(new StatModifierDefinition { Stat = StatId.Recovery, ScaleByStatusPotency = true, PotencyCoefficientBp = -10000 }, StatusPolarity.Debuff, "DecreaseRecovery", 4000);
            CheckModifierIcon(new StatModifierDefinition { Stat = StatId.Recovery, Amount = 4000 }, StatusPolarity.Buff, "StatUp");
            CheckStatusIcon(new StatusRecipeDefinition { Id = "status.debuff.poison", Polarity = StatusPolarity.Debuff }, "Poison");
            CheckStatusIcon(new StatusRecipeDefinition { Id = "status.debuff.disable-debuff", Polarity = StatusPolarity.Debuff }, "DisableDebuff");
            CheckStatusIcon(new StatusRecipeDefinition { Id = "status.buff.rejuvenation", Polarity = StatusPolarity.Neutral }, "Rejuvenation");

            var characters = AssetDatabase.FindAssets("t:CharacterObject", new[] { "Assets/Project/Data/Character" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<CharacterObject>)
                .Where(c => c != null && c.RuntimeReady).ToArray();
            int cards = 0;
            foreach (var character in characters)
            {
                var definition = CharacterObjectCatalogBuilder.CreateDefinition(character);
                var owner = new FighterState { Definition = definition };
                var passive = CombatDetailFormatter.ExtractPassiveDetails(owner, character);
                report.AppendLine("## " + character.FighterName + "\n\n" + passive.RawDescription + "\n");
                Check(!passive.RawDescription.Contains("allies's"), definition.Id + ": malformed possessive in passive.");
                if (definition.Id == "fighter.scii-cassandra")
                    Check(definition.Skills[0].Ranks[0].Effect.Sequence.Any(s => s.Effect?.StatusRecipe?.Id == "status.increase.damage-taken" &&
                        s.Effect.StatusRecipe.Polarity == StatusPolarity.Debuff), "Increasing damage taken must preserve its authored Debuff classification.");
                if (definition.Id == "fighter.kensou94")
                {
                    Check(passive.Statuses.Count == 0, "Kensou's passive aura must not create a status chip.");
                    Check(passive.Keywords == "", "Kensou's Recovery Rate must not generate a keyword effect.");
                    Check(passive.RawDescription.Contains("40%"), "Kensou's source value must remain unchanged.");
                }
                foreach (var skill in definition.Skills)
                    foreach (var rank in skill.Ranks)
                    {
                        var sharedEffect = CardRules.TryGetSkill(definition, skill.Id, rank.Rank, out var resolvedSkillEffect) ? resolvedSkillEffect : rank.Effect;
                        var formatted = CardDescriptionText.Format(rank.Description, sharedEffect, rank.Wording);
                        Check(CardDescriptionText.Format(formatted, sharedEffect, rank.Wording) == formatted,
                            definition.Id + " R" + rank.Rank + ": repeated catalog/card formatting changed the sentence.");
                        if (definition.Id == "fighter.pre-release.billy97" && skill.Slot == 1 && rank.Rank > 1)
                            Check(rank.TargetScope == EffectTargetScope.AllEnemies && Plain(formatted).Contains("on all enemies") && !Plain(formatted).Contains("on one enemy"),
                                "Billy97 Skill 1 R" + rank.Rank + ": rank preview must inherit the shared all-enemy target.");
                        Audit(new CardState { Kind = CardKind.Skill, SkillId = skill.Id, Rank = rank.Rank }, sharedEffect,
                            "Skill " + skill.Slot + " R" + rank.Rank);
                    }
                foreach (var tier in definition.UltimateTiers)
                {
                    Check(CardDescriptionText.Format(tier.Description, tier.Effect) == tier.Description,
                        definition.Id + " C" + tier.Tier + ": repeated catalog/card formatting changed the sentence.");
                    Audit(new CardState { Kind = CardKind.Ultimate, UltimateTier = tier.Tier }, tier.Effect, "Ultimate C" + tier.Tier);
                }

                void Audit(CardState card, EffectDefinition effect, string label)
                {
                    var before = SnapshotJson.Serialize(effect);
                    var payload = CombatDetailFormatter.ExtractCardDetails(card, owner, character);
                    Check(SnapshotJson.Serialize(effect) == before, definition.Id + " " + label + ": formatting changed executable data.");
                    var text = Plain(payload.desc); cards++;
                    Check(!Regex.IsMatch(text, @"\bin \d+ hits\b", RegexOptions.IgnoreCase), definition.Id + " " + label + ": animation hit count leaked into card information.");
                    report.AppendLine("- **" + label + ":** " + text + "\n  Icons: " + string.Join(", ", payload.statuses.Select(s => s.Name + " (" + s.Polarity + ")")));
                    var damage = effect?.Kind == EffectKind.Damage ? effect : effect?.Sequence?.Select(s => s?.Effect).FirstOrDefault(e => e?.Kind == EffectKind.Damage);
                    if (damage != null)
                        Check(text.IndexOf(Plain(CardDescriptionText.DamageSentence(damage)), StringComparison.OrdinalIgnoreCase) >= 0,
                            definition.Id + " " + label + ": damage sentence not normalized: " + text);
                    Check(text.IndexOf("selected enemy", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("selected ally", StringComparison.OrdinalIgnoreCase) < 0,
                        definition.Id + " " + label + ": old selected-target wording.");
                    Check(!text.Contains("status.") && !text.Contains("attack."), definition.Id + " " + label + ": internal ID leaked into description.");
                    Check(!Regex.IsMatch(text, @"Heal [^.]+? for 0 HP"), definition.Id + " " + label + ": no-op support coordinator leaked into wording.");
                    foreach (var badge in payload.statuses)
                    {
                        Check(badge.Icon != null, definition.Id + " " + label + ": missing " + badge.Name + " icon.");
                        Check(badge.Polarity != StatusPolarity.Buff || !badge.Name.StartsWith("Decrease", StringComparison.Ordinal),
                            definition.Id + " " + label + ": buff/debuff name mismatch.");
                    }
                }
                report.AppendLine();
            }
            report.Insert(0, (Failures.Count == 0 ? "PASS" : "FAIL") + ": " + assertions + " assertions; " + characters.Length + " characters; " + cards + " card ranks/tiers.\n\n");
        }
        catch (Exception error) { Failures.Add(error.ToString()); }
        if (Failures.Count > 0) report.AppendLine("\n## Failures\n\n" + string.Join("\n", Failures.Select(f => "- " + f)));
        Directory.CreateDirectory("Artifacts/CombatText");
        File.WriteAllText("Artifacts/CombatText/Review.md", report.ToString());
        Debug.Log(report.ToString().Split('\n')[0] + "\n" + string.Join("\n", Failures));
        EditorApplication.Exit(Failures.Count == 0 ? 0 : 1);
    }
}
#endif
