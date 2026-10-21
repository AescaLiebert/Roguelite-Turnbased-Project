#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Content;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Explicit acceptance operation: authors the existing Joe96 native set through Studio controls.</summary>
    public static class StudioJoe96AuthoringTrial
    {
        public static object Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use stable Edit Mode.");
            var joe = CharacterObjectRegistrySO.LoadAll().Single(c => c.DefinitionId == "fighter.joe96");
            var original = CharacterObjectCatalogBuilder.CreateDefinition(joe);
            var paths = new UnityEngine.Object[] { joe, joe.Skill1, joe.Skill2, joe.Ultimate, joe.PassiveDefinition }.Select(AssetDatabase.GetAssetPath).ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var folder = "Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96"; Directory.CreateDirectory(folder);
            var backup = Path.Combine(folder, "BeforeNativeAuthoring");
            if (!Directory.Exists(backup)) { Directory.CreateDirectory(backup); foreach (var path in paths) File.Copy(path, Path.Combine(backup, Path.GetFileName(path))); }
            var drafts = AssetDatabase.FindAssets("t:StudioDraftAsset").Length;
            var w = CharacterCreatorStudioWindow.Open(); var session = StudioProductionBridge.OpenSource(joe); w.LoadDraft(session);
            w.ShowWorkspace("identity");
            Set(w.rootVisualElement, "Display name", original.DisplayName);
            Set(w.rootVisualElement, "Attack", 250); Set(w.rootVisualElement, "Defense", 200); Set(w.rootVisualElement, "Max Health", 4500);
            Set(w.rootVisualElement, "Attack growth (bp)", original.StatGrowth.AttackBp);
            Set(w.rootVisualElement, "Defense growth (bp)", original.StatGrowth.DefenseBp);
            Set(w.rootVisualElement, "Health growth (bp)", original.StatGrowth.HealthBp);
            for (var slot = 1; slot <= 2; slot++)
            {
                w.ShowWorkspace("skill" + slot);
                var source = original.Skills.Single(s => s.Slot == slot);
                for (var rank = 1; rank <= 3; rank++)
                {
                    Click(w, "studio-kit-rank-" + rank);
                    var sourceRank = source.Ranks.Single(r => r.Rank == rank);
                    var primary = w.rootVisualElement.Q<VisualElement>("studio-kit-primary");
                    Set(primary, "Coefficient (bp)", sourceRank.Effect.CoefficientBp);
                    Set(primary, "Logical hit count", slot == 1 ? 2 : 4);
                    var wording = sourceRank.Description;
                    if (slot == 2 && rank == 3) wording = "Inflict damage equal to 450% of the hero's Attack on one enemy, then increase all allies' Block Chance and Defense by 60% for 3 turns.";
                    Set(w.rootVisualElement, "Player wording", wording);
                }
            }
            w.ShowWorkspace("ultimate");
            for (var tier = 0; tier <= 5; tier++)
            {
                Click(w, "studio-kit-tier-" + tier);
                Set(w.rootVisualElement.Q<VisualElement>("studio-kit-primary"), "Coefficient (bp)", 50000 + tier * 3000);
                Set(w.rootVisualElement, "Player wording", original.UltimateTiers.Single(t => t.Tier == tier).Description);
            }
            w.ShowWorkspace("passive");
            var aura = w.rootVisualElement.Q<VisualElement>("studio-kit-aura");
            aura.Query<Foldout>().ToList().Single(f => f.text == "Targets & availability").value = true;
            Set(aura, "Attribute ID", "attribute.blue"); Set(aura, "Amount (bp for ratios)", 6000);
            var stat = Find<EnumField>(aura, "Stat"); stat.SetValueWithoutNotify(StatId.Attack); stat.value = StatId.Defense;
            var operation = Find<EnumField>(aura, "Operation"); operation.SetValueWithoutNotify(ModifierOperation.Flat); operation.value = ModifierOperation.PercentOfBase;
            Set(w.rootVisualElement, "Passive wording", "Increases Blue attribute allies' Defense by 60%.");
            if (w.Compilation?.Success != true) throw new InvalidOperationException(string.Join("\n", w.Compilation.Diagnostics));
            Click(w, "studio-save");
            var native = CharacterObjectCatalogBuilder.CreateDefinition(joe);
            var checks = new List<string>(); void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
            Check(joe.StudioOwned && joe.Skill1.StudioOwned && joe.Skill2.StudioOwned && joe.Ultimate.StudioOwned && joe.PassiveDefinition.StudioOwned, "Joe96 is authored in his existing CharacterObject and four data SOs.");
            Check(paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids), "All five existing Joe96 GUIDs and references are preserved.");
            Check(native.BaseStats.Attack == 250 && native.BaseStats.Defense == 200 && native.BaseStats.MaxHealth == 4500, "Native Joe96 identity/stats are saved through Studio fields.");
            Check(native.Skills[0].Ranks.Select(r => r.Effect.CoefficientBp).SequenceEqual(new[] { 12000, 12000, 18000 }) && native.Skills[0].Ranks.All(r => r.Effect.Attack.ResolvedHitCount == 2), "Skill 1 retains all-enemy damage 120/120/180 percent and two hits.");
            Check(native.Skills[0].Ranks.Select(r => r.Effect.Sequence.Count).SequenceEqual(new[] { 0, 1, 2 }), "Skill 1 retains rank-dependent Disable Debuff/Disable Buff steps.");
            Check(native.Skills[1].Ranks.Select(r => r.Effect.CoefficientBp).SequenceEqual(new[] { 20000, 36000, 45000 }) && native.Skills[1].Ranks.All(r => r.Effect.Attack.ResolvedHitCount == 4), "Skill 2 retains single-target damage 200/360/450 percent and four hits.");
            Check(native.Skills[1].Ranks.Select(r => r.Effect.Sequence.Count).SequenceEqual(new[] { 1, 1, 2 }) && native.Skills[1].Ranks[2].Description.Contains("60%"), "Skill 2 retains team protection steps; rank-three wording displays 60 percent instead of raw 6000.");
            Check(native.UltimateTiers.OrderBy(t => t.Tier).Select(t => t.Effect.CoefficientBp).SequenceEqual(new[] { 50000, 53000, 56000, 59000, 62000, 65000 }), "Ultimate C0–C5 retains 500/530/560/590/620/650 percent and the imported Rupture recipe.");
            Check(native.Passive.Auras[0].Targets.AttributeId == "attribute.blue" && native.Passive.Auras[0].Modifiers[0].ResolvedStat == StatId.Defense && native.Passive.Auras[0].Modifiers[0].ResolvedOperation == ModifierOperation.PercentOfBase && native.Passive.Auras[0].Modifiers[0].Amount == 6000, "Joe96 passive grants Blue allies 60 percent Defense.");
            Check(AssetDatabase.FindAssets("t:StudioDraftAsset").Length == drafts, "Actual Joe96 authoring creates no separate draft asset.");
            var reopened = StudioProductionBridge.OpenSource(joe); w.LoadDraft(reopened); w.ShowWorkspace("overview");
            Check(w.Compilation.Success && w.Compilation.Character.Id == "fighter.joe96", "Joe96 reopens and compiles directly from his native assets.");
            var content = ScriptableObject.CreateInstance<FightingAllstar.Presentation.Combat.CombatContentSource>();
            try { Check(content.LoadCatalog().Characters.Count(c => c.Id == "fighter.joe96") == 1, "Normal game loading discovers the existing Joe96 identity exactly once."); } finally { UnityEngine.Object.DestroyImmediate(content); }
            var evidence = new { Success = true, Checks = checks, Paths = paths, Guids = guids, DraftAssetsCreated = AssetDatabase.FindAssets("t:StudioDraftAsset").Length - drafts, CapturedUtc = DateTime.UtcNow };
            File.WriteAllText(Path.Combine(folder, "native-authoring.json"), JsonConvert.SerializeObject(evidence, Formatting.Indented));
            return evidence;
        }
        private static T Find<T>(VisualElement root, string label) where T : VisualElement
        {
            if (label == "Player wording" || label == "Source wording") root.Query<Foldout>().ToList().Single(f => f.text == "Description & source wording").value = true;
            return root.Query<T>().ToList().Single(f => (string)f.GetType().GetProperty("label").GetValue(f) == label);
        }
        private static void Set(VisualElement root, string label, int value) { var f = Find<IntegerField>(root, label); f.SetValueWithoutNotify(value + 1); f.value = value; }
        private static void Set(VisualElement root, string label, string value) { var f = Find<TextField>(root, label); f.SetValueWithoutNotify((value ?? "") + " "); f.value = value ?? ""; }
        private static void Click(CharacterCreatorStudioWindow w, string name) { var button = w.rootVisualElement.Q<Button>(name); button.Focus(); using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit); }
    }
}
#endif
