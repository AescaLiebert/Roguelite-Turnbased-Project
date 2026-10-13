#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Run;
using FightingAllstar.EditorTools;
using FightingAllstar.Presentation;
using FightingAllstar.Presentation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Native layout, equipment transactions, material limits and interruptible CC feedback.</summary>
public static class ArtifactImprovementsUiChecks
{
    private static readonly Type Preview = typeof(ScreenLayoutPreview);
    private const BindingFlags SP = BindingFlags.Static | BindingFlags.NonPublic, IP = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector2Int[] Sizes = { new Vector2Int(1920,1080), new Vector2Int(1280,720) };
    private static bool DetailOnly => Environment.GetCommandLineArgs().Contains("-skillDetails");
    private static string Folder => DetailOnly ? "Artifacts/SkillDetails" : "Artifacts/ArtifactImprovements";
    private static readonly List<string> Notes = new List<string>();
    private static readonly Queue<Action> Steps = new Queue<Action>();
    private static PlayerInventoryService inventory;
    private static CharacterObject fighter, other;
    private static VisualElement root;
    private static string accountPath, fighterId, otherId;
    private static int index, assertions;
    private static double next;
    private static Vector2Int Size => Sizes[index];

    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        EditorApplication.update -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), Preview.GetMethod("Tick", SP));
        ScreenLayoutPreview.Run();
        inventory = (PlayerInventoryService)Preview.GetField("_inventory", SP).GetValue(null);
        try
        {
            var subject = "guest:__artifact-improvements__" + Guid.NewGuid().ToString("N");
            Set(inventory, "_loadedSubjectId", subject); inventory.Snapshot.subjectId = subject;
            accountPath = PlayerInventoryService.GetSavePath(subject);
            fighter = inventory.GetOwnedDefinitions().FirstOrDefault(c => c.DefinitionId.Contains("cassandra")) ?? inventory.GetOwnedDefinitions().First();
            other = inventory.GetOwnedDefinitions().First(c => c != fighter);
            fighterId = inventory.FindOwnedByDefinition(fighter.DefinitionId).instanceId; otherId = inventory.FindOwnedByDefinition(other.DefinitionId).instanceId;
            Transactions();
            Preview.GetField("Folder", SP).SetValue(null, Folder); Preview.GetField("Screens", SP).SetValue(null, new[] { "MainMenu" });
            Build(); EditorApplication.update += Tick;
        }
        catch (Exception error) { Finish(error); }
    }

    private static ArtifactInstance Piece(string id, ArtifactSlot slot, int level = 0, ArtifactRarity rarity = ArtifactRarity.SSR)
    {
        var piece = ArtifactRules.Generate(id, rarity, 491);
        piece.Slot = slot; piece.Set = ArtifactSet.Striker; piece.ItemId = ArtifactRules.ItemId(piece.Set, slot); piece.Level = level;
        piece.MainStat.Stat = StatId.Attack; piece.MainStat.Percent = false; ArtifactRules.Normalize(piece); return piece;
    }
    private static void Seed()
    {
        var owned = inventory.FindOwnedByDefinition(fighter.DefinitionId); owned.level = 1; owned.levelCap = 10;
        var wallet = new ItemInventory { Coins = 10000000 };
        var weak = Piece("weak-flower", ArtifactSlot.Flower, rarity: ArtifactRarity.Common); weak.EquippedCharacterId = fighterId;
        var strong = Piece("strong-flower", ArtifactSlot.Flower, 19); strong.EquippedCharacterId = otherId; strong.Favourite = true;
        wallet.Artifacts.Add(weak); wallet.Artifacts.Add(strong);
        for (var slot = 1; slot < 5; slot++) wallet.Artifacts.Add(Piece("equipped-" + slot, (ArtifactSlot)slot));
        for (var i = 0; i < 35; i++) wallet.Artifacts.Add(Piece("material-" + i.ToString("D2"), (ArtifactSlot)(i % 5), rarity: ArtifactRarity.Common));
        foreach (var item in ItemCatalog.UpgradeMaterials()) wallet.Materials.Add(new ItemAmount { ItemId = item.Id, Amount = 10000 });
        wallet.Materials.Add(new ItemAmount { ItemId = ItemCatalog.SeriesId(fighter.SeriesId), Amount = 10000 });
        inventory.Snapshot.items = wallet; inventory.SaveCurrent();
    }
    private static void Transactions()
    {
        Seed(); var count = inventory.GetItemInventorySnapshot().Artifacts.Count; var balance = inventory.Coins;
        Check(inventory.TryEquipArtifact("strong-flower", fighterId, out _), "Equipped artifact swap was rejected.");
        Check(inventory.FindArtifact("strong-flower").EquippedCharacterId == fighterId && inventory.FindArtifact("weak-flower").EquippedCharacterId == otherId, "Swap did not exchange owners.");
        Check(inventory.GetItemInventorySnapshot().Artifacts.Count == count && inventory.Coins == balance && inventory.FindArtifact("strong-flower").Favourite, "Swap consumed resources or changed favourite protection.");
        inventory.ReloadFromDisk(); Check(inventory.FindArtifact("weak-flower").EquippedCharacterId == otherId, "Swap was not persisted.");
        Check(inventory.TryUnequipArtifact(fighterId, ArtifactSlot.Flower, out _) && inventory.TryEquipArtifact("weak-flower", fighterId, out _), "Transfer into an empty destination failed.");
        Check(inventory.GetEquippedArtifacts(otherId).Count == 0, "Transfer left the artifact on two fighters.");
        Check(inventory.TryEquipArtifact("strong-flower", fighterId, out _) && string.IsNullOrEmpty(inventory.FindArtifact("weak-flower").EquippedCharacterId), "Backpack replacement did not free the previous artifact.");
        var before = JsonUtility.ToJson(inventory.Snapshot); var saved = File.ReadAllText(accountPath);
        Check(inventory.TryEquipArtifact("strong-flower", fighterId, out _) && JsonUtility.ToJson(inventory.Snapshot) == before, "Equipping the same piece was not idempotent.");
        Check(!inventory.TryEquipArtifact("unknown", fighterId, out _) && !inventory.TryEquipArtifact("strong-flower", "unknown", out _) && JsonUtility.ToJson(inventory.Snapshot) == before, "Invalid equipment requests mutated inventory.");
        Directory.CreateDirectory(accountPath + ".tmp");
        try { Check(!inventory.TryEquipArtifact("weak-flower", fighterId, out _) && JsonUtility.ToJson(inventory.Snapshot) == before && File.ReadAllText(accountPath) == saved, "Failed save did not roll back the swap."); }
        finally { Directory.Delete(accountPath + ".tmp"); }
        Notes.Add("Equipment: occupied swap, empty transfer, backpack replacement, favourite preservation, idempotency, reload and failed-save rollback passed.");
    }
    private static void Build()
    {
        Seed(); Preview.GetField("_index", SP).SetValue(null, index); Preview.GetMethod("Build", SP).Invoke(null, null);
        root = ((UIDocument)Preview.GetField("_document", SP).GetValue(null)).rootVisualElement;
        Steps.Clear(); Steps.Enqueue(InspectKit); Steps.Enqueue(InspectCardDetails);
        if (!DetailOnly) { Steps.Enqueue(Equip); Steps.Enqueue(Positive); Steps.Enqueue(Unequip);
            Steps.Enqueue(Negative); Steps.Enqueue(Materials); Steps.Enqueue(MaterialLimit); Steps.Enqueue(BackpackAndRewards); }
        next = EditorApplication.timeSinceStartup + .8;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (Steps.Count > 0) { next = EditorApplication.timeSinceStartup + .3; Steps.Dequeue()(); return; }
            Notes.Add(Size + (DetailOnly ? ": rank artwork sequence, stable rank switching, all ultimate levels/current marker, passive artwork/conditions, unchanged status badges and runtime skill glyph mapping passed." : ": kit ratio, additional stats, persistent picker, swap, five-slot strip, CC gain/loss, 15 materials and shared grid sizes passed."));
            index++; if (index < Sizes.Length) Build(); else Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void InspectKit()
    {
        CharacterInventoryView.Show(root); CharacterHubView.Show(root, fighter); Settle(); Render("CharacterKit");
        var card = root.Q("hub-card-1").Q<Button>("card-button"); var bounds = card.worldBound;
        Check(Math.Abs(bounds.width / bounds.height - 1461f / 2650) < .015f, "Kit card frame is deformed: " + bounds);
        var type = root.Q("hub-card-1").Q<Image>("card-skill-type").worldBound;
        Check(Math.Abs(card.worldBound.xMax - type.xMax - 5) < 2 && Math.Abs(type.yMin - card.worldBound.yMin - 5) < 2, "Kit skill-type icon no longer follows the card's top-right inset: " + type);
        ScrollbarsHidden();
        Check(root.Q("cc-change-feedback").style.display == DisplayStyle.None, "Opening an inspector triggered CC feedback.");
        Click(root.Q<Button>("hub-stat-details")); Render("AdditionalStats");
        Check(root.Query(className: "hub-detail-stat").ToList().Count == Enum.GetValues(typeof(StatId)).Length - 4, "Additional stats still repeat front-page stats.");
        Click(root.Q<Button>("hub-detail-close")); Click(root.Q<Button>("hub-tab-artifacts")); Settle(); Render("ArtifactTab");
        Check(root.Q("artifact-fighter-name") == null && root.Q("artifact-fighter-icon") == null, "Duplicate character identity remains on artifact tab.");
        Check(root.Q("artifact-detail-panel").Q(className: "artifact-detail-ownership") == null && root.Q("artifact-slot-0").Q(className: "artifact-owner-avatar") == null, "Selected fighter's equipped artifacts repeat ownership.");
        for (var i = 0; i < 6; i++)
        {
            var slot = root.Q("artifact-slot-" + i); Bounds(slot);
            var hero = root.Q(className: "hub-hero").worldBound;
            Check(slot.worldBound.yMax <= hero.yMax + 1, "Artifact slot is clipped by the character hero.");
        }
    }
    private static void Equip()
    {
        var originalPreview = root.Q("artifact-detail-panel");
        Click(root.Q<Button>("artifact-change")); Render("ArtifactInventory");
        Click(root.Q<Button>("picker-strong-flower")); Render("ArtifactSwapPreview");
        var preview = root.Q("artifact-detail-panel");
        var inventoryPanel = root.Q("artifact-equipment-inventory");
        Check(ReferenceEquals(originalPreview, preview) && root.Query(className: "artifact-inspection-card").ToList().Count == 1 && root.Q("artifact-equipment-overlay") == null, "Equipment inventory created another screen or duplicate artifact preview.");
        Check(root.Q(className: "hub-hero").Contains(inventoryPanel) && inventoryPanel.Contains(root.Q("artifact-equipped-scroll")), "Character preview must be at the bottom inside the left inventory.");
        Check(inventoryPanel != null && inventory.FindArtifact("strong-flower").EquippedCharacterId == otherId, "Selecting a tile closes inventory or equips before confirmation.");
        Check(preview.Q<Button>("artifact-equip").text == "SWAP & EQUIP", "Occupied slot does not communicate a swap.");
        Click(preview.Q<Button>("artifact-equip"));
        Check(root.Q("artifact-equipment-inventory") != null && inventory.FindArtifact("weak-flower").EquippedCharacterId == otherId, "Equip closed the picker or failed to swap.");
        Render("ArtifactEquipped");
        Check(root.Q("artifact-equipped-strip").childCount == 6, "Equipped strip needs selected character plus five slots.");
        Check(root.Q("artifact-strip-slot-0").Q(className: "artifact-owner-avatar") == null && preview.Q(className: "artifact-detail-ownership") == null, "Equipped preview repeats ownership.");
        Check(root.Q("picker-strong-flower").Q(className: "artifact-owner-avatar") != null, "Inventory omits the selected fighter's ownership indicator.");
        Bounds(root.Q("artifact-equipped-scroll")); Bounds(preview.Q<Button>("artifact-unequip"));
        var strip = root.Q("artifact-equipped-strip");
        var previewViewport = root.Q<ScrollView>("artifact-equipped-scroll").contentViewport.worldBound;
        foreach (var summarySlot in strip.Children()) Check(summarySlot.worldBound.xMin >= previewViewport.xMin && summarySlot.worldBound.xMax <= previewViewport.xMax, "Character preview extends outside its visible inventory row.");
        var summaryChildren = strip.Children().ToList();
        var previewCenter = (summaryChildren.First().worldBound.xMin + summaryChildren.Last().worldBound.xMax) / 2;
        var viewportCenter = root.Q<ScrollView>("artifact-equipped-scroll").contentViewport.worldBound.center.x;
        Check(Math.Abs(previewCenter - viewportCenter) < 12, "Character preview is not centered in the inventory panel.");
        var minimumSlotWidth = root.ClassListContains("compact-layout") ? 84 : 110;
        foreach (var summarySlot in strip.Children().Skip(1)) Check(summarySlot.worldBound.width >= minimumSlotWidth, "Character preview artifact slots were not enlarged.");
        Check(root.Q<ScrollView>("artifact-picker-scroll").contentViewport.worldBound.height >= ItemRewardView.TileSide + ItemRewardView.FooterHeight + 4, "Inventory cannot display a complete enlarged tile row.");
        ScrollbarsHidden();
        next = EditorApplication.timeSinceStartup + .35;
    }
    private static void InspectCardDetails()
    {
        Click(root.Q<Button>("hub-tab-kit"));
        var art = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
        Check(art.rankTwo != null && new[] { art.skillSingleTarget, art.skillAoe, art.skillBuff, art.skillRecovery, art.skillStance, art.skillUltimate, art.passiveOnField, art.passiveSub, art.passiveAll, art.passivePve, art.passivePvp }.All(s => s != null), "Reference artwork is not wired for runtime builds.");
        var owner = new FighterState { Definition = new CharacterDefinition() };
        foreach (CardCategory category in new[] { CardCategory.Attack, CardCategory.AttackDebuff, CardCategory.Debuff, CardCategory.Buff, CardCategory.Recovery, CardCategory.Stance })
        {
            var card = new CardState { Kind = CardKind.Skill, Category = category, EffectCategory = category, TargetScope = EffectTargetScope.SelectedEnemy };
            var expected = category == CardCategory.Buff ? art.skillBuff : category == CardCategory.Recovery ? art.skillRecovery : category == CardCategory.Stance ? art.skillStance : art.skillSingleTarget;
            Check(art.SkillIcon(card, owner) == expected, "Incorrect skill tooltip glyph for " + category);
            if (category == CardCategory.Attack || category == CardCategory.AttackDebuff || category == CardCategory.Debuff)
            { card.TargetScope = EffectTargetScope.AllEnemies; Check(art.SkillIcon(card, owner) == art.skillAoe, "AOE tooltip reused the single-target glyph."); }
        }
        Check(art.SkillIcon(new CardState { Kind = CardKind.Ultimate }, owner) == art.skillUltimate, "Ultimate tooltip is missing its own glyph.");
        Click(root.Q("hub-card-1").Q<Button>("card-button")); Render("SkillRanksR1");
        var overlay = root.Q("hub-detail-overlay"); Bounds(root.Q(className: "hub-detail-dialog"));
        Check(root.Query(className: "hub-rank-card-choice").ToList().Count == 3 && root.Query(className: "hub-rank-arrow").ToList().Count == 2, "Skill details do not show R1 to R3 as three artwork choices.");
        Check(root.Query<Image>(className: "hub-detail-card-art").ToList().All(i => i.sprite != null && i.worldBound.width > 0 && i.worldBound.height > 0), "Rank card artwork is blank.");
        Click(root.Q<Button>("hub-detail-rank-2")); Render("SkillRanksR2");
        Check(ReferenceEquals(overlay, root.Q("hub-detail-overlay")) && root.Q<Button>("hub-detail-rank-2").ClassListContains("selected"), "Rank comparison rebuilt the popup or lost selection.");
        Click(root.Q<Button>("hub-detail-rank-3")); Render("SkillRanksR3");
        var definition = FightingAllstar.Presentation.Content.CharacterObjectCatalogBuilder.CreateDefinition(fighter);
        var authoredOwner = new FighterState { Definition = definition };
        var skill = definition.Skills.First(s => s.Slot == 1);
        var details = FightingAllstar.Presentation.Combat.CombatDetailFormatter.ExtractCardDetails(new CardState { Kind = CardKind.Skill, Rank = 3, SkillId = skill.Id }, authoredOwner, fighter);
        Check(root.Q(className: "hub-tooltip-status-grid")?.Query<Image>().ToList().Select(i => i.sprite).SequenceEqual(details.statuses.Select(s => s.Icon)) ?? details.statuses.Count == 0, "Rank comparison changed status badges.");
        Click(root.Q<Button>("hub-detail-close")); Click(root.Q("hub-card-0").Q<Button>("card-button")); Render("UltimateLevels");
        Check(root.Query(className: "hub-ultimate-level").ToList().Count == 6 && root.Query(className: "hub-level-current-marker").ToList().Count == 1, "Ultimate details omit levels or the current level marker.");
        Click(root.Q<Button>("hub-detail-rank-5")); Render("UltimateLevel5");
        var currentTier = inventory.FindOwnedByDefinition(fighter.DefinitionId).constellationTier;
        Check(root.Q<Button>("hub-detail-rank-5").ClassListContains("selected") && root.Q<Button>("hub-detail-rank-" + currentTier).ClassListContains("current-level"), "Previewing a future ultimate level changed the current-level marker.");
        Click(root.Q<Button>("hub-detail-close")); Click(root.Q<Button>("hub-passive-button")); Render("PassiveReference");
        Check(root.Q<Image>("hub-passive-preview").sprite != null && root.Q<Image>("hub-passive-preview").sprite == (fighter.FighterPassive != null ? fighter.FighterPassive : fighter.FighterIcon) && root.Q("hub-passive-conditions").Query<Image>().ToList().Count == 2, "Passive details lack artwork or source/mode condition icons.");
        Check(root.Q("hub-detail-overlay").Query(className: "hub-note").ToList().Count == 0, "Passive details still display restriction/type text.");
        ScrollbarsHidden(); Bounds(root.Q(className: "hub-detail-dialog"));
        Click(root.Q<Button>("hub-detail-close")); Click(root.Q<Button>("hub-tab-artifacts"));
    }
    private static void Positive()
    {
        Render("CCIncrease");
        Check(root.Q("cc-change-feedback").resolvedStyle.opacity > .9f && root.Q<Label>("cc-change-delta").text.Contains("+"), "Positive CC feedback lacks visible gain.");
        next = EditorApplication.timeSinceStartup + 2.1;
    }
    private static void Unequip()
    {
        Render("CCIncreaseFinished"); Check(root.Q("cc-change-feedback").style.display == DisplayStyle.None, "CC feedback does not fade out.");
        Check(root.Q<Label>("cc-change-value").text == inventory.GetCharacterStats(fighter).CombatClass.ToString("N0"), "CC count did not settle at the committed value.");
        var preview = root.Q("artifact-detail-panel"); var namePanel = preview.Q("item-tooltip-name-panel");
        var imagePanel = preview.Q("item-tooltip-preview-panel"); var substats = preview.Q(className: "artifact-substats");
        var scroll = preview.Q<ScrollView>("item-tooltip-content-panel"); var offset = scroll.scrollOffset;
        Click(preview.Q<Button>("artifact-unequip"));
        Check(ReferenceEquals(namePanel, preview.Q("item-tooltip-name-panel")) && ReferenceEquals(imagePanel, preview.Q("item-tooltip-preview-panel")) && ReferenceEquals(substats, preview.Q(className: "artifact-substats")) && ReferenceEquals(scroll, preview.Q<ScrollView>("item-tooltip-content-panel")), "Unequip rebuilt or cleared the selected artifact preview.");
        Check(preview.Q<Label>("item-tooltip-name").text == ArtifactItemView.Name(inventory.FindArtifact("strong-flower")) && preview.Q<Button>("artifact-equip")?.enabledSelf == true && scroll.scrollOffset == offset, "Unequip lost the selected artifact, Equip action or scroll position.");
        Check(root.Q("picker-strong-flower").Q(className: "artifact-owner-avatar") == null, "Unequipped inventory tile retains stale ownership.");
        next = EditorApplication.timeSinceStartup + .35;
    }
    private static void Negative()
    {
        Render("CCDecrease"); Check(root.Q<Label>("cc-change-delta").text.Contains("−") && root.Q("cc-change-feedback").ClassListContains("cc-decrease"), "Negative CC feedback lacks signed loss.");
        Check(root.Q("artifact-equipment-inventory") != null, "Unequip closed the inventory.");
        Click(root.Q<Button>("account-back")); Check(root.Q("artifact-equipment-inventory") == null && root.Q("hub-portrait").style.display != DisplayStyle.None && root.Q("artifact-character-stage").style.display != DisplayStyle.None, "Back does not close inventory and restore the original character view.");
        next = EditorApplication.timeSinceStartup + 2.1;
    }
    private static void Materials()
    {
        Click(root.Q<Button>("artifact-change")); Click(root.Q<Button>("picker-strong-flower"));
        Click(root.Q("artifact-detail-panel").Q<Button>("artifact-enhance")); Render("EnhancementLab");
        Check(root.Q("artifact-equipment-inventory") == null && root.Q("enhance-materials").childCount == 15, "Lab lacks 15 material slots.");
        Click(root.Q<Button>("enhance-auto")); Render("Enhancement15Slots");
        Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 15, "Auto Add bypassed the 15-slot capacity.");
        ScrollbarsHidden();
        Click(root.Q<Button>("enhance-add"));
        var grid = root.Q("artifact-picker-grid"); Check(grid.Query<Button>().ToList().All(b => b.name != "picker-strong-flower" && b.name != "picker-weak-flower"), "Protected or equipped artifacts are eligible fodder.");
    }
    private static void MaterialLimit()
    {
        var grid = root.Q("artifact-picker-grid");
        var unselected = grid.Query<Button>().ToList().First(b => !b.ClassListContains("material-selected")); Click(unselected);
        Check(root.Q<Label>("enhance-note").text.Contains("15") && root.Q("enhance-materials").childCount == 15, "Manual material selection exceeds the cap.");
        var selected = grid.Query<Button>().ToList().First(b => b.ClassListContains("material-selected")); Click(selected);
        Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 14, "A full tray cannot remove materials.");
        Click(root.Q<Button>(unselected.name)); Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 15, "Replacing a tray material failed.");
        Click(root.Q<Button>("account-back")); Render("EnhancementMaterials"); Bounds(root.Q<Button>("enhance-commit"));
        Click(root.Q<Button>("enhance-commit")); var before = inventory.GetItemInventorySnapshot().Artifacts.Count;
        Click(root.Q<Button>("artifact-confirm-commit")); Check(inventory.GetItemInventorySnapshot().Artifacts.Count == before - 15 && inventory.FindArtifact("strong-flower").Favourite, "Enhancement did not consume exactly reviewed fodder or lost favourite protection.");
        Check(root.Q("enhance-materials").childCount == 15, "Tray does not reset after enhancement.");
        Click(root.Q<Button>("account-back"));
        // The same observer also catches enhancement of a worn artifact and level growth.
        Check(inventory.TryEquipArtifact("strong-flower", fighterId, out _), "Could not equip enhancement target.");
        var oldCc = inventory.GetCharacterStats(fighter).CombatClass;
        Check(inventory.TryUpgradeCharacter(fighterId, false, out _) && inventory.GetCharacterStats(fighter).CombatClass > oldCc, "Level up did not increase resolved CC.");
        Check(root.Q("cc-change-feedback").style.display == DisplayStyle.Flex && root.Q<Label>("cc-change-delta").text.Contains("+"), "Level growth did not trigger CC feedback.");
        Check(inventory.TryUnequipArtifact(fighterId, ArtifactSlot.Flower, out _), "Could not clear level-up test equipment.");
        next = EditorApplication.timeSinceStartup + 2.3;
    }
    private static void BackpackAndRewards()
    {
        Render("LevelCCFinished");
        Check(root.Q<Label>("cc-change-value").text == inventory.GetCharacterStats(fighter).CombatClass.ToString("N0"), "Consecutive CC changes did not settle at the latest committed value.");
        // Rapid reversals cancel the burst; unrelated wallet changes do not create a new animation.
        Check(inventory.TryEquipArtifact("strong-flower", fighterId, out _) && inventory.TryUnequipArtifact(fighterId, ArtifactSlot.Flower, out _), "Rapid equipment reversal failed.");
        Check(root.Q("cc-change-feedback").style.display == DisplayStyle.None, "A cancelled CC burst leaves stale feedback.");
        inventory.SaveCurrent(); Check(root.Q("cc-change-feedback").style.display == DisplayStyle.None, "Unchanged inventory replays CC feedback.");
        Click(root.Q<Button>("hub-tab-upgrade"));
        var owned = inventory.FindOwnedByDefinition(fighter.DefinitionId); owned.level = 10; owned.levelCap = 10; inventory.SaveCurrent(); Settle(); Render("LimitBreak");
        var upgradeTiles = root.Q("hub-upgrade-materials").Query<Button>().ToList();
        var tile = upgradeTiles.First(); GridSize(tile); Bounds(root.Q<Button>("hub-upgrade-action"));
        Check(upgradeTiles.Count > 1 && upgradeTiles.All(t => Math.Abs(t.worldBound.y - tile.worldBound.y) < 2), "Limit-break materials wrapped instead of staying on one standard upgrade row.");
        ScrollbarsHidden();
        BackpackView.Show(root); Render("Backpack"); GridSize(root.Q("backpack-grid").Query<Button>().ToList().First()); ScrollbarsHidden();
        Click(root.Q<Button>("backpack-close"));
        Preview.GetField("Screens", SP).SetValue(null, new[] { "DungeonRouteMap" }); Preview.GetMethod("Build", SP).Invoke(null, null);
        root = ((UIDocument)Preview.GetField("_document", SP).GetValue(null)).rootVisualElement;
        Settle();
        ScrollbarsHidden();
        root.Q<ScrollView>("stage-detail-scroll").ScrollTo(root.Q("stage-reward-scroll")); Render("DungeonRewards");
        var rewards = root.Q("stage-reward-grid").Query<Button>().ToList(); GridSize(rewards.First());
        var viewport = root.Q<ScrollView>("stage-reward-scroll").contentViewport.worldBound;
        Check(viewport.height >= ItemRewardView.TileSide + ItemRewardView.FooterHeight + 4, "Dungeon reward tiles are vertically clipped.");
        Bounds(root.Q<Button>("enter-stage-button"));
        var controller = ((GameObject)Preview.GetField("_screen", SP).GetValue(null)).GetComponent<FightingAllstar.Presentation.Route.DungeonRouteMapController>();
        var run = controller.GetRunSnapshot(); var receipt = DungeonRewards.Victory(run, run.Nodes.First(n => n.Type == RouteNodeType.Battle));
        run.Rewards.Add(receipt); inventory.Snapshot.items.ClaimedReceiptIds.Add(receipt.Id); Set(controller, "_run", run);
        controller.GetType().GetMethod("ShowPendingRewards", IP).Invoke(controller, null); Settle(); Render("VictoryRewards");
        var strip = root.Q<ScrollView>(className: "reward-item-strip"); Check(strip != null, "Victory reward strip is missing.");
        GridSize(strip.Query<Button>().ToList().First());
        Check(strip.contentViewport.worldBound.height >= ItemRewardView.TileSide + ItemRewardView.FooterHeight + 4, "Victory reward tiles are vertically clipped: " + strip.contentViewport.worldBound);
        ScrollbarsHidden();
        Bounds(root.Q<Button>("continue-after-reward"));
        Preview.GetField("Screens", SP).SetValue(null, new[] { "MainMenu" });
    }
    private static void GridSize(VisualElement tile)
    {
        Check(Math.Abs(tile.worldBound.width - (96 * 1.2f + 4)) < 1 && Math.Abs(tile.Q(className: "item-reward-frame").worldBound.height - 116 * 1.2f) < 1, "Shared item grid dimensions differ by screen: " + tile.worldBound);
    }
    private static void ScrollbarsHidden()
    {
        foreach (var scroll in root.Query<ScrollView>().ToList())
            Check(scroll.horizontalScrollerVisibility == ScrollerVisibility.Hidden && scroll.verticalScrollerVisibility == ScrollerVisibility.Hidden,
                "Visible scrollbar remains on " + scroll.name + ".");
    }
    private static void Bounds(VisualElement element)
    { var b = element.worldBound; Check(b.width > 0 && b.height > 0 && b.xMin >= 0 && b.yMin >= 84 && b.xMax <= Size.x + 1 && b.yMax <= Size.y + 1, element.name + " exceeds viewport: " + b); }
    private static void Click(Button button)
    { Check(button != null && button.enabledInHierarchy, "Clickable control missing or disabled."); typeof(Clickable).GetMethod("Invoke", IP).Invoke(button.clickable, new object[] { null }); }
    private static void Settle()
    { foreach (var element in root.Query(className: "animated-panel").ToList()) { element.RemoveFromClassList("animated-panel"); element.RemoveFromClassList("panel-enter"); } }
    private static void Render(string name) => Preview.GetMethod("Render", SP).Invoke(null, new object[] { Size, name, true });
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, IP).SetValue(target, value);
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Notes.Add(error == null ? "PASS: " + assertions + " assertions, native Unity rendering and isolated account transactions." : "FAIL: " + error);
        File.WriteAllLines(Folder + "/Verification.txt", Notes);
        if (accountPath != null && File.Exists(accountPath)) File.Delete(accountPath);
        Preview.GetMethod("Cleanup", SP).Invoke(null, null);
        if (error != null) Debug.LogException(error); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
