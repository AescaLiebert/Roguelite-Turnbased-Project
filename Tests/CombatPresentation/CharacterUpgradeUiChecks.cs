#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.EditorTools;
using FightingAllstar.Presentation;
using FightingAllstar.Presentation.Content;
using FightingAllstar.Presentation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Isolated native UI, persisted transactions and full level/cap boundary verification.</summary>
public static class CharacterUpgradeUiChecks
{
    private static readonly Type Preview = typeof(ScreenLayoutPreview);
    private const BindingFlags SP = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags IP = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector2Int[] Sizes = { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(768,1024) };
    private static int index, assertions;
    private static double next;
    private static PlayerInventoryService inventory;
    private static string accountPath;
    private static readonly List<string> notes = new List<string>();
    private const string Folder = "Artifacts/CharacterUpgrade";

    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        EditorApplication.update -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), Preview.GetMethod("Tick", SP));
        ScreenLayoutPreview.Run();
        inventory = (PlayerInventoryService)Preview.GetField("_inventory", SP).GetValue(null);
        try
        {
            var subject = "guest:__upgrade-check__" + Guid.NewGuid().ToString("N");
            Set(inventory, "_loadedSubjectId", subject); inventory.Snapshot.subjectId = subject;
            accountPath = PlayerInventoryService.GetSavePath(subject);
            Progression();
            Preview.GetField("Folder", SP).SetValue(null, Folder);
            Preview.GetField("Screens", SP).SetValue(null, new[] { "MainMenu" });
            Build(); EditorApplication.update += Tick;
        }
        catch (Exception ex) { Finish(ex); }
    }

    private static void Progression()
    {
        var characters = inventory.GetOwnedDefinitions();
        Check(characters.Count == 29, "Expected all 29 runtime-ready fighters.");
        var signatures = new HashSet<string>();
        foreach (var character in characters)
        {
            var definition = CharacterObjectCatalogBuilder.CreateDefinition(character);
            var growth = character.LevelGrowth;
            Check(growth != null && growth.AttackBp > 0 && growth.DefenseBp > 0 && growth.HealthBp > 0 && growth.CombatClassBp > 0, "Growth must be authored for " + character.DefinitionId);
            signatures.Add(JsonUtility.ToJson(growth));
            var original = SnapshotJson.Serialize(definition.BaseStats);
            for (var level = 1; level <= 90; level++)
            {
                var stats = CharacterProgression.StatsAtLevel(definition.BaseStats, definition.StatGrowth, level);
                foreach (StatId stat in Enum.GetValues(typeof(StatId)))
                    Check(stat >= StatId.Pierce ? stats.Get(stat) == definition.BaseStats.Get(stat) : level == 1 ? stats.Get(stat) == definition.BaseStats.Get(stat) : stats.Get(stat) > definition.BaseStats.Get(stat), "Growth scope mismatch.");
                Check(CharacterProgression.NormalizeCap(level, 0) == (level + 9) / 10 * 10, "Legacy cap migration mismatch.");
            }
            Check(SnapshotJson.Serialize(definition.BaseStats) == original, "Stat resolution mutated content.");
            Check(CharacterProgression.TryCosts(character.ID, character.SeriesId, 10, 10, true, out var costs, out _), "Missing fighter material identity.");
            Check(costs.Count == 4, "Limit break requires all four resource types.");
        }
        Check(signatures.Count == characters.Count, "Each fighter should have a distinct growth profile.");
        for (var level = 2; level < 90; level++) Check(CharacterProgression.LevelCoinCost(level) > CharacterProgression.LevelCoinCost(level - 1), "Coin costs must increase each level.");
        var split = new ItemInventory(); split.Materials.Add(new ItemAmount { ItemId = ItemCatalog.PhaseId(1), Amount = 2 });
        split.Materials.Add(new ItemAmount { ItemId = ItemCatalog.PhaseId(1), Amount = 4 });
        Check(split.TrySpend(new[] { new ItemAmount { ItemId = ItemCatalog.PhaseId(1), Amount = 5 } }, UpgradeResourceUse.CharacterUpgrade, out var spentSplit, out _) &&
            spentSplit.Amount(ItemCatalog.PhaseId(1)) == 1 && spentSplit.Materials.All(m => m.Amount >= 0) && split.Amount(ItemCatalog.PhaseId(1)) == 6, "Split material stacks caused a negative quantity or mutated the source.");
        for (var phase = 1; phase <= 9; phase++) for (var sub = 0; sub < 5; sub++)
        {
            Check(CharacterProgression.TryCosts(phase * 1000 + sub * 100, "series.kof", 10, 10, true, out var costs, out _), "Valid material identity rejected.");
            Check(costs.Any(c => c.ItemId == ItemCatalog.PhaseId(phase)) && costs.Any(c => c.ItemId == ItemCatalog.SubPhaseId(sub)), "Incorrect phase material mapping.");
        }
        foreach (var item in ItemCatalog.UpgradeMaterials().Concat(new[] { ItemCatalog.Find(ItemCatalog.SeriesId("series.kof")) }))
            Check(!string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Description) && !string.IsNullOrWhiteSpace(item.Appearance) && !string.IsNullOrWhiteSpace(item.IconColor), "Material design missing.");
        var iori = characters.Single(c => c.DefinitionId == "fighter.iori95");
        var record = inventory.FindOwnedByDefinition(iori.DefinitionId);
        Check(iori.ID == 1406, "Iori95 identity changed.");
        var wallet = new ItemInventory { Coins = 1000000000 };
        foreach (var item in ItemCatalog.UpgradeMaterials()) wallet.Materials.Add(new ItemAmount { ItemId = item.Id, Amount = 10000 });
        wallet.Materials.Add(new ItemAmount { ItemId = ItemCatalog.SeriesId("series.kof"), Amount = 10000 });
        inventory.Snapshot.items = wallet;
        var unchanged = JsonUtility.ToJson(inventory.Snapshot);
        Check(!inventory.TryUpgradeCharacter("unknown", false, out _) && JsonUtility.ToJson(inventory.Snapshot) == unchanged, "Unowned upgrade changed resources.");
        Check(!inventory.TryUpgradeCharacter(record.instanceId, true, out _) && JsonUtility.ToJson(inventory.Snapshot) == unchanged, "Early limit break changed resources.");
        for (var expected = 1; expected <= 90; expected++)
        {
            record = inventory.FindOwnedByDefinition(iori.DefinitionId);
            Check(record.level == expected, "Level skipped.");
            if (expected == 90) break;
            if (expected == record.levelCap)
            {
                var before = JsonUtility.ToJson(inventory.Snapshot);
                Check(!inventory.TryUpgradeCharacter(record.instanceId, false, out _) && JsonUtility.ToJson(inventory.Snapshot) == before, "Cap can be bypassed.");
                var statsBefore = JsonUtility.ToJson(inventory.GetCharacterStats(iori));
                var cap = record.levelCap;
                CharacterProgression.TryCosts(iori.ID, iori.SeriesId, record.level, cap, true, out var costs, out _);
                if (cap == 10)
                {
                    foreach (var id in new[] { ItemCatalog.PhaseId(1), ItemCatalog.SubPhaseId(4), ItemCatalog.SeriesId("series.kof"), ItemCatalog.Coin })
                    {
                        var held = inventory.GetItemAmount(id);
                        if (id == ItemCatalog.Coin) inventory.Snapshot.items.Coins = 0; else inventory.Snapshot.items.Materials.Find(m => m.ItemId == id).Amount = 0;
                        var lacking = JsonUtility.ToJson(inventory.Snapshot);
                        Check(!inventory.TryUpgradeCharacter(record.instanceId, true, out _) && JsonUtility.ToJson(inventory.Snapshot) == lacking, "Missing specific resource was ignored: " + id);
                        if (id == ItemCatalog.Coin) inventory.Snapshot.items.Coins = held; else inventory.Snapshot.items.Materials.Find(m => m.ItemId == id).Amount = held;
                    }
                    notes.Add("Iori95: WIP-Phase Prototype Crest + META Apex Sigil + KOF Tournament Insignia + Coins.");
                }
                var holdings = costs.ToDictionary(c => c.ItemId, c => inventory.GetItemAmount(c.ItemId));
                Check(inventory.TryUpgradeCharacter(record.instanceId, true, out var error), error);
                Check(record.levelCap == cap + 10 && record.level == expected && JsonUtility.ToJson(inventory.GetCharacterStats(iori)) == statsBefore, "Limit break changed stats or level.");
                foreach (var cost in costs) Check(inventory.GetItemAmount(cost.ItemId) == holdings[cost.ItemId] - cost.Amount, "Incorrect limit break deduction.");
                inventory.ReloadFromDisk(); record = inventory.FindOwnedByDefinition(iori.DefinitionId);
                Check(record.levelCap == cap + 10 && record.level == expected, "Limit break did not survive reload.");
            }
            var coins = inventory.Coins; var costLevel = CharacterProgression.LevelCoinCost(expected);
            Check(inventory.TryUpgradeCharacter(record.instanceId, false, out var levelError), levelError);
            Check(record.level == expected + 1 && inventory.Coins == coins - costLevel, "Incorrect level deduction.");
        }
        var maximum = JsonUtility.ToJson(inventory.Snapshot);
        Check(!inventory.TryUpgradeCharacter(record.instanceId, false, out _) && !inventory.TryUpgradeCharacter(record.instanceId, true, out _) && JsonUtility.ToJson(inventory.Snapshot) == maximum, "Max level is bypassable.");
        inventory.ReloadFromDisk(); Check(inventory.FindOwnedByDefinition(iori.DefinitionId).level == 90, "Level did not persist.");
        var ioriDef = CharacterObjectCatalogBuilder.CreateDefinition(iori);
        var seed = inventory.CreateRunFighterSeed(ioriDef, 3);
        Check(seed.IsReserve && seed.ResolvedStats.Attack == inventory.GetCharacterStats(iori).Attack && seed.ResolvedStats.MaxHealth > ioriDef.BaseStats.MaxHealth, "Formation uses level-one stats.");
        TrainingContext.Prepare(iori, 0); Check(TrainingContext.TryCreateEncounter(out var encounter, out _), "Training failed.");
        Check(encounter.PlayerTeam[0].Stats.Attack == seed.ResolvedStats.Attack && encounter.PlayerTeam[0].CurrentHealth == seed.ResolvedStats.MaxHealth, "Battle snapshot uses level-one stats."); TrainingContext.Clear();
        record = inventory.FindOwnedByDefinition(iori.DefinitionId); record.level = 1; record.levelCap = 10;
        inventory.Snapshot.items.Coins = 0;
        var noCoins = JsonUtility.ToJson(inventory.Snapshot);
        Check(!inventory.TryUpgradeCharacter(record.instanceId, false, out _) && JsonUtility.ToJson(inventory.Snapshot) == noCoins, "Coin shortage mutated state.");
        inventory.Snapshot.items.Coins = 1000000;
        inventory.SaveCurrent();
        var failure = JsonUtility.ToJson(inventory.Snapshot);
        var savedBeforeFailure = File.ReadAllText(accountPath);
        // Only this unique fixture account is touched. Force the temporary save write to fail.
        Directory.CreateDirectory(accountPath + ".tmp");
        try { Check(!inventory.TryUpgradeCharacter(record.instanceId, false, out _) && JsonUtility.ToJson(inventory.Snapshot) == failure && File.ReadAllText(accountPath) == savedBeforeFailure, "Save failure did not preserve disk and memory state."); }
        finally { Directory.Delete(accountPath + ".tmp"); }
        notes.Add("Verified every level 1–90, all eight cap unlocks, reload persistence, shortages, unknown ownership, failed-save rollback, all 45 phase/sub-phase identities, and battle stat resolution.");
    }

    private static void Build()
    {
        Preview.GetField("_index", SP).SetValue(null, index); Preview.GetMethod("Build", SP).Invoke(null, null);
        next = EditorApplication.timeSinceStartup + 1; 
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            var root = ((UIDocument)Preview.GetField("_document", SP).GetValue(null)).rootVisualElement;
            var size = Sizes[index]; var iori = inventory.FindDefinition("fighter.iori95");
            var record = inventory.FindOwnedByDefinition(iori.DefinitionId); record.level = 10; record.levelCap = 10;
            CharacterInventoryView.Show(root); Settle(root); Render(size, "CharacterInventory");
            Check(root.Q("inventory-detail-icon") == null && root.Q("inventory-inspect") == null, "Preview still exists.");
            root.Q<TextField>("inventory-search").value = iori.FighterName;
            var card = root.Q<Button>("character-" + iori.DefinitionId); Click(card); Settle(root); Render(size, "CharacterKit");
            var hub = root.Q("character-hub");
            Check(hub.style.display == DisplayStyle.Flex && root.Q("character-inventory").style.display == DisplayStyle.None, "Grid click failed to enter inspector.");
            Check(root.Q("hub-stats").childCount == 4 && root.Q("hub-kit-icons").childCount == 4, "Kit panels are incomplete.");
            Check(root.Q("hub-passive-description") == null, "Passive prose remains permanently visible.");
            var passiveIcon = root.Q<Image>("hub-passive-image");
            Check(passiveIcon.sprite != null && passiveIcon.worldBound.width >= 60 && passiveIcon.worldBound.height >= 60, "Passive icon has no visible artwork: sprite=" + passiveIcon.sprite + " bounds=" + passiveIcon.worldBound);
            Check(hub.worldBound.y >= 84 && hub.worldBound.yMax <= size.y + 1, "Inspector exceeds viewport.");
            var kitArt = root.Q("hub-card-1").Q<Image>("card-rank-frame"); Check(kitArt.image != null, "Battle card frame reference is missing.");
            Click(root.Q<Button>("hub-stat-details")); Render(size, "CharacterStats");
            Check(root.Q("hub-detail-overlay").Query(className:"hub-detail-stat").ToList().Count == Enum.GetValues(typeof(StatId)).Length, "Stats popup is incomplete.");
            CheckDialog(root, size); Escape(root.Q<Button>("hub-detail-close"));
            Check(root.Q("hub-detail-overlay") == null && hub.style.display == DisplayStyle.Flex, "Popup Escape closed the inspector.");
            Click(root.Q("hub-card-1").Q<Button>("card-button")); Click(root.Q<Button>("hub-detail-rank-3")); Render(size, "CharacterCardTooltip");
            Check(root.Q<Label>(className:"hub-tooltip-description").text.Contains("<color=") && root.Q(className:"hub-tooltip-header") != null, "Combat formatting is missing.");
            Check(!root.Q<Label>(className:"hub-tooltip-keywords").text.Contains("Rejuvenation"), "Reused status art introduced a different mechanic into the glossary.");
            CheckDialog(root, size); Click(root.Q<Button>("hub-detail-close"));
            Click(root.Q("hub-card-2").Q<Button>("card-button"));
            Check(root.Q<Label>(className:"hub-tooltip-description").text.Contains("<color=") && root.Q<Button>("hub-detail-rank-1") != null, "Second skill popup is missing.");
            Click(root.Q<Button>("hub-detail-close"));
            Click(root.Q("hub-card-0").Q<Button>("card-button")); Click(root.Q<Button>("hub-detail-rank-5")); Render(size, "CharacterUltimateTooltip");
            Check(root.Q<Label>(className:"hub-tooltip-description").text.Contains("<color=") && record.constellationTier == 0, "Ultimate preview changed ownership or lacks formatting.");
            CheckDialog(root, size); Click(root.Q<Button>("hub-detail-close"));
            Click(root.Q<Button>("hub-passive-button")); Render(size, "CharacterPassiveTooltip"); CheckDialog(root, size); Click(root.Q<Button>("hub-detail-close"));
            Click(root.Q<Button>("hub-tab-upgrade")); Settle(root); Render(size, "CharacterUpgrade");
            Check(root.Q("hub-break-materials").childCount == 4 && root.Q("hub-level-materials").childCount == 1, "Upgrade materials are incomplete.");
            Check(!root.Q<Button>("hub-level-up").enabledSelf && root.Q<Button>("hub-limit-break").enabledSelf, "Cap actions have wrong enabled state.");
            var breakAction = root.Q<Button>("hub-limit-break").worldBound;
            Check(breakAction.yMin >= 84 && breakAction.yMax <= root.Q<ScrollView>("hub-scroll").contentViewport.worldBound.yMax + 1, "Required limit-break action is below the fold.");
            var stats = JsonUtility.ToJson(inventory.GetCharacterStats(iori)); Click(root.Q<Button>("hub-limit-break"));
            Check(record.levelCap == 20 && record.level == 10 && stats == JsonUtility.ToJson(inventory.GetCharacterStats(iori)), "UI limit break changed stats.");
            Settle(root); Render(size, "CharacterLevelUp");
            Click(root.Q<Button>("hub-level-up")); Check(record.level == 11 && root.Q<Label>("hub-upgrade-result").text.Contains("10 → 11"), "UI level up did not commit or report.");
            Render(size, "CharacterUpgradeResult");
            record.level = 90; record.levelCap = 90; inventory.SaveCurrent();
            Check(!root.Q<Button>("hub-level-up").enabledSelf && !root.Q<Button>("hub-limit-break").enabledSelf, "Max-level UI enables progression.");
            Render(size, "CharacterMaxLevel");
            Click(root.Q<Button>("hub-close")); Check(root.Q("character-inventory").style.display == DisplayStyle.Flex && root.Q<TextField>("inventory-search").value == iori.FighterName, "Back lost grid filters.");
            Click(root.Q<Button>("character-" + iori.DefinitionId)); Escape(root.Q<Button>("hub-tab-kit")); Check(hub.style.display == DisplayStyle.None, "Inspector Escape did not return to grid.");
            notes.Add(size + ": grid click, stats/card/passive popups, combat formatting, persisted upgrade clicks, back/escape and filter preservation passed.");
            index++; if (index < Sizes.Length) { Build(); return; }
            Finish(null);
        }
        catch (Exception ex) { Finish(ex); }
    }
    private static void CheckDialog(VisualElement root, Vector2Int size)
    {
        var bound = root.Q(className:"hub-detail-dialog").worldBound;
        Check(bound.xMin >= 0 && bound.yMin >= 84 && bound.xMax <= size.x + 1 && bound.yMax <= size.y + 1, "Popup is clipped.");
    }
    private static void Click(Button button)
    {
        Check(button != null && button.enabledInHierarchy, "Clickable control missing or disabled.");
        typeof(Clickable).GetMethod("Invoke", IP).Invoke(button.clickable, new object[] { null });
    }
    private static void Escape(VisualElement target)
    {
        using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape })) { evt.target = target; target.SendEvent(evt); }
    }
    private static void Settle(VisualElement root)
    {
        foreach (var element in root.Query(className:"animated-panel").ToList()) { element.RemoveFromClassList("animated-panel"); element.RemoveFromClassList("panel-enter"); }
    }
    private static void Render(Vector2Int size, string name) => Preview.GetMethod("Render", SP).Invoke(null, new object[] { size, name, true });
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, IP).SetValue(target, value);
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        if (error != null) notes.Add("FAIL: " + error);
        else notes.Add("PASS: " + assertions + " assertions. Native Unity render + isolated account transactions.");
        File.WriteAllLines(Folder + "/Verification.txt", notes);
        if (accountPath != null) { if (File.Exists(accountPath)) File.Delete(accountPath); if (File.Exists(accountPath + ".tmp")) File.Delete(accountPath + ".tmp"); }
        Preview.GetMethod("Cleanup", SP).Invoke(null, null);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
