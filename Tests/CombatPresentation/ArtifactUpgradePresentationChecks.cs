#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.EditorTools;
using FightingAllstar.Presentation;
using FightingAllstar.Presentation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Real timed UI Toolkit rendering of artifact lookup, drafting and committed upgrade feedback.</summary>
public static class ArtifactUpgradePresentationChecks
{
    private const string Folder = "Artifacts/ArtifactUpgrade";
    private const BindingFlags SP = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags IP = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type Preview = typeof(ScreenLayoutPreview);
    private static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };
    private static readonly List<string> Notes = new List<string>();
    private static readonly Queue<Step> Steps = new Queue<Step>();
    private static PlayerInventoryService inventory;
    private static CharacterObject fighter;
    private static VisualElement root;
    private static string accountPath, fighterId, draftSnapshot, savedDraft;
    private static int index, assertions, inventoryCount;
    private static long coinsBefore, expectedCost;
    private static ArtifactInstance before;
    private static VisualElement workshopExit;
    private static Rect workshopControlsBeforeLookup;
    private static VisualElement motionProbe;
    private static int staleHideCallbacks;
    private static bool committed, sawSuccessSparkle, sawAffixPulse, sawLevelPulse, sawVisibleAffix, capturedSparkle;
    private static double sparkleStarted;
    private static string keyboardSnapshot;
    private static string pickerEntryName;
    private static bool sawPickerFade, sawPickerSlide, sawGridFade;
    private static float firstXp, arrowStartY;
    private static double next, deadline;
    private static string activeStep;
    private static Vector2Int Size => Sizes[index];

    private sealed class Step
    {
        public string Name;
        public Action Action;
        public double Wait;
    }

    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        Notes.Clear(); index = assertions = 0;
        EditorApplication.update -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
            typeof(EditorApplication.CallbackFunction), Preview.GetMethod("Tick", SP));
        ScreenLayoutPreview.Run();
        inventory = (PlayerInventoryService)Preview.GetField("_inventory", SP).GetValue(null);
        try
        {
            Check(inventory != null, "Screen preview could not acquire an isolated inventory.");
            var subject = "guest:__artifact-upgrade-presentation__" + Guid.NewGuid().ToString("N");
            Set(inventory, "_loadedSubjectId", subject); inventory.Snapshot.subjectId = subject;
            accountPath = PlayerInventoryService.GetSavePath(subject);
            fighter = inventory.GetOwnedDefinitions().FirstOrDefault(c => c.DefinitionId.Contains("cassandra"))
                ?? inventory.GetOwnedDefinitions().First();
            fighterId = inventory.FindOwnedByDefinition(fighter.DefinitionId).instanceId;
            Preview.GetField("Folder", SP).SetValue(null, Folder);
            Preview.GetField("Screens", SP).SetValue(null, new[] { "MainMenu" });
            Build();
            EditorApplication.update += Tick;
        }
        catch (Exception error) { Finish(error); }
    }

    private static ArtifactInstance Piece(string id, ArtifactRarity rarity, ArtifactSlot slot = ArtifactSlot.Flower, int level = 0)
    {
        var piece = ArtifactRules.Generate(id, rarity, 491);
        piece.Slot = slot; piece.Set = ArtifactSet.Striker;
        piece.ItemId = ArtifactRules.ItemId(piece.Set, slot); piece.Level = level;
        ArtifactRules.Normalize(piece);
        return piece;
    }

    private static void Seed()
    {
        var wallet = new ItemInventory { Coins = 10000000 };
        var target = Piece("upgrade-target", ArtifactRarity.SSR, level: 5);
        target.EquippedCharacterId = fighterId; target.Favourite = true;
        target.Experience = ArtifactRules.RequiredXp(target, target.Level) - 100;
        wallet.Artifacts.Add(target);
        for (var rarity = 1; rarity <= 5; rarity++)
            wallet.Artifacts.Add(Piece("rarity-" + rarity, (ArtifactRarity)rarity));
        for (var i = 0; i < 35; i++)
            wallet.Artifacts.Add(Piece("material-" + i.ToString("D2"), ArtifactRarity.Common, (ArtifactSlot)(i % 5)));
        var protectedPiece = Piece("protected-favourite", ArtifactRarity.Common); protectedPiece.Favourite = true;
        wallet.Artifacts.Add(protectedPiece);
        var other = inventory.GetOwnedDefinitions().First(c => c != fighter);
        var wornPiece = Piece("protected-equipped", ArtifactRarity.Common);
        wornPiece.EquippedCharacterId = inventory.FindOwnedByDefinition(other.DefinitionId).instanceId;
        wallet.Artifacts.Add(wornPiece);
        inventory.Snapshot.items = wallet; inventory.SaveCurrent();
        before = inventory.FindArtifact("upgrade-target").Clone();
    }

    private static void Build()
    {
        committed = sawSuccessSparkle = sawAffixPulse = sawLevelPulse = sawVisibleAffix = capturedSparkle = false;
        sparkleStarted = -1;
        Seed(); Preview.GetField("_index", SP).SetValue(null, index);
        Preview.GetMethod("Build", SP).Invoke(null, null);
        root = ((UIDocument)Preview.GetField("_document", SP).GetValue(null)).rootVisualElement;
        Steps.Clear();
        Then("motion helper baseline", CreateMotionProbe, .4);
        Then("motion helper hide", () => ScreenMotion.Hide(motionProbe, () => staleHideCallbacks++), .06);
        Then("motion helper reopen", () =>
        {
            Check(motionProbe.resolvedStyle.opacity > 0 && motionProbe.resolvedStyle.opacity < .8f,
                "Shared Hide has no visible fade before hiding.");
            ScreenMotion.Show(motionProbe);
        }, .6);
        Then("motion helper restored geometry", CheckMotionRestored, .1);
        Then("motion helper rapid reopen", () =>
        {
            for (var i = 0; i < 5; i++)
            { ScreenMotion.Hide(motionProbe, () => staleHideCallbacks++); ScreenMotion.Show(motionProbe); }
        }, .6);
        Then("motion helper cancellation", () => { CheckMotionRestored(); motionProbe.RemoveFromHierarchy(); }, .1);
        Then("open character", () => { CharacterInventoryView.Show(root); CharacterHubView.Show(root, fighter); }, .8);
        Then("open artifact tab", () => Click(root.Q<Button>("hub-tab-artifacts")), .8);
        Then("enlarged slots", CheckSlots, .1);
        Then("open equipment lookup", () => { BeginPickerObservation("EquipmentPickerEnter"); Click(root.Q<Button>("artifact-change")); }, .95);
        Then("equipment lookup entry", () => CapturePickerEntry("EquipmentPickerEnter"), .1);
        Then("equipment lookup sorting", CheckEquipmentPicker, .95);
        Then("equipment lookup settled", () => { CheckSettledGrid(); Capture("EquipmentPicker"); }, .1);
        Then("open workshop", () => { Click(root.Q<Button>("picker-upgrade-target")); Click(root.Q<Button>("artifact-enhance")); }, .9);
        Then("fresh workshop focused Escape", () =>
        {
            keyboardSnapshot = JsonUtility.ToJson(inventory.Snapshot);
            var outgoing = root.Q("artifact-enhance-overlay"); SendFocusedEscape();
            Check(root.Q("artifact-enhance-overlay") == null && outgoing.panel != null,
                "Escape from the actual focused element did not begin closing a fresh workshop.");
            Click(root.Q<Button>("artifact-enhance"));
        }, .7);
        Then("workshop reopened focus", () =>
        {
            Check(ReferenceEquals(root.panel.focusController.focusedElement, root.Q("artifact-enhance-overlay")),
                "Pending workshop exit stole focus from its newly opened replacement.");
            Check(JsonUtility.ToJson(inventory.Snapshot) == keyboardSnapshot, "Closing/reopening a fresh workshop spent resources.");
        }, .1);
        Then("workshop baseline", CheckWorkshop, .1);
        Then("open material lookup", () => { BeginPickerObservation("MaterialPickerEnter"); Click(root.Q<Button>("enhance-empty-material-0")); }, .95);
        Then("material lookup entry", () => CapturePickerEntry("MaterialPickerEnter"), .1);
        Then("material lookup ready", CheckMaterialPicker, .95);
        Then("material lookup settled", () => { CheckSettledGrid(); Capture("MaterialPicker"); }, .1);
        Then("draft material", BeginDraft, .12);
        Then("draft feedback entry", CheckDraftFeedback, .18);
        Then("draft arrow movement", CheckDraftMovement, .9);
        Then("draft settled", CheckDraftSettled, .1);
        Then("close material lookup", () => Click(root.Q<Button>("account-back")), .5);
        Then("open confirmation", OpenConfirmation, .4);
        Then("confirmation focused Escape", () =>
        {
            keyboardSnapshot = JsonUtility.ToJson(inventory.Snapshot); SendFocusedEscape();
            Check(root.Q("artifact-confirm-overlay") == null, "Escape from the focused confirmation control did not cancel.");
            Check(JsonUtility.ToJson(inventory.Snapshot) == keyboardSnapshot, "Cancelling enhancement confirmation spent resources.");
            Click(root.Q<Button>("enhance-commit"));
        }, .4);
        Then("confirmation reopened focus", () =>
        {
            var overlay = root.Q("artifact-confirm-overlay");
            var focused = root.panel.focusController.focusedElement as VisualElement;
            Check(overlay != null && focused != null && overlay.Contains(focused),
                "Pending confirmation exit stole focus from its replacement.");
        }, .1);
        Then("commit reviewed upgrade", BeginCommit, .12);
        Then("experience fill first frame", FirstCommitFrame, .18);
        Then("experience fill progress", CheckCommitProgress, 2.6);
        Then("committed upgrade", CheckCommitted, 1.5);
        Then("success effect cleanup", CheckEffectCleanup, .1);
        Then("horizontal material cap", CheckMaterialCap, .5);
        Then("manual material limit", CheckManualMaterialLimit, .5);
        Then("clear material tray", () => { Click(root.Q<Button>("account-back")); Click(root.Q<Button>("enhance-clear")); }, .5);
        Then("close workshop", () => { workshopExit = root.Q("artifact-enhance-overlay"); Click(root.Q<Button>("account-back")); }, .06);
        Then("workshop exit motion", () =>
        {
            Capture("WorkshopExit");
            Check(workshopExit.panel != null && workshopExit.resolvedStyle.opacity > 0 && workshopExit.resolvedStyle.opacity < 1,
                "Workshop exit does not visibly fade before detaching.");
        }, .5);
        Then("workshop closed", () => { Check(root.Q("artifact-enhance-overlay") == null, "Workshop did not finish exit and detach."); Capture("ArtifactTabAfterUpgrade"); }, .1);
        Then("picker close during tab switch", () =>
        {
            Click(root.Q<Button>("artifact-change"));
            Click(root.Q<Button>("account-back"));
            Click(root.Q<Button>("hub-tab-kit"));
        }, .7);
        Then("tab switch remains active", () =>
        {
            Capture("KitAfterPickerExit");
            Check(root.Q("artifact-equipment-inventory") == null, "Picker closing during tab switch left an inventory blocker.");
            var stage = root.Q("artifact-character-stage");
            Check(stage == null || stage.resolvedStyle.display == DisplayStyle.None,
                "Delayed picker exit re-showed artifact slots on the character kit tab.");
        }, .1);
        next = EditorApplication.timeSinceStartup + .4;
        deadline = EditorApplication.timeSinceStartup + 90;
    }

    private static void Then(string name, Action action, double wait) => Steps.Enqueue(new Step { Name = name, Action = action, Wait = wait });

    private static void Tick()
    {
        try
        {
            // The preview panel has no Game view. Pump its real update loop so scheduled motion runs naturally.
            Preview.GetMethod("Render", SP).Invoke(null, new object[] { Size, "Pump", false });
            ObservePickerMotion();
            ObserveSuccessFeedback();
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Timed out in " + activeStep);
            if (EditorApplication.timeSinceStartup < next) return;
            if (Steps.Count > 0)
            {
                var step = Steps.Dequeue(); activeStep = step.Name;
                step.Action(); next = EditorApplication.timeSinceStartup + step.Wait; return;
            }
            Notes.Add("PASS " + Size + ": enlarged slots, shared sorting, timed picker entry, draft values/arrows, horizontal materials, committed XP/affix feedback, exact consumption and persistence.");
            index++;
            if (index < Sizes.Length) Build(); else Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void CheckSlots()
    {
        Capture("ArtifactTab");
        Check(root.Q("artifact-loadout-message") == null, "Removed artifact log message returned.");
        var hero = root.Q(className: "hub-hero");
        foreach (var i in Enumerable.Range(0, 6))
        {
            var slot = root.Q("artifact-slot-" + i); Bounds(slot);
            var frame = slot.Q(className: "item-reward-frame");
            Check(frame.worldBound.width >= (Size.x == 1280 ? 105 : 135), "Artifact slot frame is not enlarged: " + frame.worldBound);
            Check(slot.worldBound.yMax <= hero.worldBound.yMax + 1, "Enlarged artifact slot is clipped by character hero.");
        }
        Check(root.Q<Button>("artifact-enhance").enabledInHierarchy, "Equipped artifact cannot open enhancement.");
    }

    private static void CreateMotionProbe()
    {
        staleHideCallbacks = 0;
        motionProbe = new VisualElement { name = "motion-interruption-probe" };
        motionProbe.style.position = Position.Absolute; motionProbe.style.left = 500; motionProbe.style.top = 100;
        motionProbe.style.width = 260; motionProbe.style.height = 90;
        motionProbe.style.opacity = .8f; motionProbe.style.translate = new Translate(11, 7);
        motionProbe.style.scale = new Scale(new Vector3(1.06f, .98f, 1));
        root.Add(motionProbe); Pump();
    }

    private static void CheckMotionRestored()
    {
        Check(staleHideCallbacks == 0, "Reopening a panel invoked a stale Hide completion callback.");
        Check(motionProbe.panel != null && motionProbe.resolvedStyle.display == DisplayStyle.Flex && motionProbe.enabledSelf,
            "Rapid close/reopen left a hidden, detached or disabled panel.");
        Check(Math.Abs(motionProbe.style.opacity.value - .8f) < .001f
            && Math.Abs(motionProbe.style.translate.value.x.value - 11) < .001f
            && Math.Abs(motionProbe.style.translate.value.y.value - 7) < .001f
            && Math.Abs(motionProbe.style.scale.value.value.x - 1.06f) < .001f
            && Math.Abs(motionProbe.style.scale.value.value.y - .98f) < .001f,
            "Shared motion did not restore the panel's own inline opacity, translation and scale.");
    }

    private static void CapturePickerEntry(string name)
    {
        var panel = root.Q("artifact-picker-overlay");
        Check(panel != null, "Shared artifact lookup did not open.");
        Check(sawPickerFade, "Picker entry did not produce an intermediate rendered fade frame.");
        Check(sawPickerSlide, "Artifact lookup did not retain its slide from the left.");
        Check(sawGridFade, "Lookup item grid did not fade in over time.");
        pickerEntryName = null;
    }

    private static void BeginPickerObservation(string name)
    {
        pickerEntryName = name; sawPickerFade = sawPickerSlide = sawGridFade = false;
    }

    private static void ObservePickerMotion()
    {
        if (pickerEntryName == null) return;
        var panel = root.Q("artifact-picker-overlay");
        var dialog = panel?.Q(className: "artifact-picker-dialog");
        if (dialog == null) return;
        var wasFading = sawPickerFade;
        sawPickerFade |= dialog.resolvedStyle.opacity > 0 && dialog.resolvedStyle.opacity < .999f;
        sawPickerSlide |= dialog.resolvedStyle.translate.x < -.01f && Math.Abs(dialog.resolvedStyle.translate.y) < .01f;
        var item = panel.Q(className: "artifact-picker-item");
        var wasGridFading = sawGridFade;
        sawGridFade |= item != null && item.resolvedStyle.opacity > 0 && item.resolvedStyle.opacity < .999f;
        if (sawPickerFade && !wasFading) Capture(pickerEntryName);
        if (sawGridFade && !wasGridFading) Capture(pickerEntryName + "Grid");
    }

    private static void CheckPicker()
    {
        var panel = root.Q("artifact-picker-overlay");
        var sort = panel.Q<DropdownField>("artifact-picker-sort");
        Check(sort != null && sort.choices.SequenceEqual(new[] { "Base EXP", "1 Star", "2 Stars", "3 Stars", "4 Stars", "5 Stars" }), "Lookup sort options differ from the six requested choices.");
        Check(panel.Query<DropdownField>().ToList().Count == 1 && panel.Query<TextField>().ToList().Count == 0,
            "Legacy lookup filter/search controls remain.");
        var grid = panel.Q("artifact-picker-grid");
        Check(grid != null && grid.Query<Button>().ToList().Any(b => b.name.StartsWith("picker-")), "Shared artifact lookup grid is empty.");
        Check(panel.Q(className: "artifact-picker-dialog").resolvedStyle.opacity > .99f, "Picker does not settle fully visible.");
        foreach (var i in Enumerable.Range(1, 5))
        {
            sort.index = i; Pump();
            var first = grid.Query<Button>().ToList().First(b => b.name.StartsWith("picker-"));
            Check((int)inventory.FindArtifact(first.name.Substring(7)).Rarity == i, "Selected star rarity is not sorted first: " + sort.value);
        }
        sort.index = 0; Pump();
        var ids = PickerIds(grid);
        var xp = ids.Select(id => ArtifactRules.FodderXp(inventory.FindArtifact(id))).ToList();
        Check(xp.SequenceEqual(xp.OrderBy(v => v)) || xp.SequenceEqual(xp.OrderByDescending(v => v)), "Base EXP ordering is not monotonic.");
        var direction = panel.Q<Button>("artifact-picker-direction");
        Check(direction != null, "Lookup sort direction control is missing.");
        Click(direction); Pump();
        var reverseXp = PickerIds(grid).Select(id => ArtifactRules.FodderXp(inventory.FindArtifact(id))).ToList();
        Check(reverseXp.SequenceEqual(xp.AsEnumerable().Reverse()), "Lookup sort direction does not reverse the EXP order.");
        Click(direction); Pump();
        var viewport = panel.Q<ScrollView>("artifact-picker-scroll").contentViewport.worldBound;
        Check(viewport.height >= ItemRewardView.TileSide + ItemRewardView.FooterHeight, "Lookup cannot display a full item row.");
        foreach (var scroll in panel.Query<ScrollView>().ToList())
            Check(scroll.horizontalScrollerVisibility == ScrollerVisibility.Hidden && scroll.verticalScrollerVisibility == ScrollerVisibility.Hidden,
                "Lookup exposes scrollbar tracks.");
    }

    private static List<string> PickerIds(VisualElement grid) => grid.Query<Button>().ToList()
        .Where(b => b.name != null && b.name.StartsWith("picker-")).Select(b => b.name.Substring(7)).ToList();

    private static void CheckEquipmentPicker()
    {
        CheckPicker();
        Check(root.Q("artifact-equipment-inventory") != null && root.Q("artifact-detail-panel") != null,
            "Equipment lookup lost the existing character inspection panel.");
        Check(root.Q("artifact-equipped-strip").childCount == 6, "Equipment lookup omits fighter/five artifact summary.");
        Bounds(root.Q("artifact-equipped-scroll"));
    }

    private static void CheckWorkshop()
    {
        workshopControlsBeforeLookup = root.Q(className: "artifact-workshop-right").worldBound;
        Capture("WorkshopBaseline");
        Check(root.Q("artifact-equipment-inventory") == null, "Equipment lookup remained above workshop.");
        Check(root.Q<Label>("enhance-level").text == "+5", "Workshop level must show only current level, without /20.");
        Check(root.Q("enhance-add") == null, "Removed [+ Materials] button returned.");
        Check(root.Q("enhance-materials").childCount == 15, "Workshop material tray does not expose 15 slots.");
        CheckTrayRow(); CheckStatRowsInView(); Bounds(root.Q<Button>("enhance-commit"));
    }

    private static void CheckTrayRow()
    {
        Pump();
        var tray = root.Q("enhance-materials");
        Check(tray.resolvedStyle.flexWrap == Wrap.NoWrap && tray.resolvedStyle.flexDirection == FlexDirection.Row,
            "Upgrade materials are not a horizontal non-wrapping group.");
        var children = tray.Children().ToList();
        Check(children.All(c => Math.Abs(c.worldBound.yMin - children[0].worldBound.yMin) < 2), "Upgrade materials wrap into multiple rows.");
        var scroll = root.Q<ScrollView>("enhance-materials-scroll");
        Check(scroll != null && scroll.mode == ScrollViewMode.Horizontal, "Material row cannot scroll horizontally.");
        Check(scroll.contentViewport.worldBound.height >= children[0].worldBound.height - 1, "Horizontal material tiles are clipped vertically.");
    }

    private static void CheckStatRowsInView()
    {
        var stats = root.Q("enhance-stats");
        var viewport = stats.GetFirstAncestorOfType<ScrollView>().contentViewport.worldBound;
        var rows = stats.Children().Where(row => row.ClassListContains("artifact-main-row") || row.ClassListContains("artifact-stat-row")).ToList();
        Check(rows.Count == before.Substats.Count + 1, "Artifact presentation omits a main/substat row.");
        foreach (var row in rows)
            Check(row.worldBound.yMin >= viewport.yMin - 1 && row.worldBound.yMax <= viewport.yMax + 1,
                "Artifact stat row is clipped outside its visible viewport: " + row.name + " " + row.worldBound + " versus " + viewport);
    }

    private static void CheckMaterialPicker()
    {
        var controls = root.Q(className: "artifact-workshop-right").worldBound;
        Check(Vector2.Distance(controls.position, workshopControlsBeforeLookup.position) < .5f &&
            Vector2.Distance(controls.size, workshopControlsBeforeLookup.size) < .5f,
            "Opening artifact lookup moved or resized the workshop controls: " + controls + " versus " + workshopControlsBeforeLookup);
        Check(root.Q("enhance-picker-host").resolvedStyle.position == Position.Absolute &&
            root.Q("artifact-picker-overlay").resolvedStyle.position == Position.Absolute,
            "Workshop artifact lookup must overlay its artwork area outside the flex layout.");
        CheckPicker();
        var ids = PickerIds(root.Q("artifact-picker-grid"));
        Check(!ids.Contains("upgrade-target") && !ids.Contains("protected-favourite") && !ids.Contains("protected-equipped"),
            "Target, favourite or equipped artifacts are eligible upgrade materials.");
        Check(root.Q("enhance-hero").style.display == DisplayStyle.None, "Material picker does not replace the workshop artwork area.");
    }

    private static void CheckSettledGrid()
    {
        var grid = root.Q("artifact-picker-grid");
        Check(grid.Children().All(item => item.resolvedStyle.opacity > .99f),
            "Lookup entry left a grid item faded or hidden after its stagger finished.");
    }

    private static void BeginDraft()
    {
        draftSnapshot = JsonUtility.ToJson(inventory.Snapshot); savedDraft = File.ReadAllText(accountPath);
        Click(root.Q<Button>("picker-material-00"));
    }

    private static void CheckDraftFeedback()
    {
        Capture("DraftFeedbackEnter");
        Check(root.Q<Label>("enhance-level").text == "+5" && root.Q<Label>("enhance-level-delta").text == "+6",
            "Drafting must preserve current +5 and show the projected +6 separately.");
        var delta = root.Q("enhance-level-delta");
        Check(delta.resolvedStyle.scale.value.x > 1, "Projected level does not pop larger before settling.");
        var main = root.Q("enhance-stats").Q(className: "artifact-main-row");
        Check(main.Q<Label>(className: "artifact-stat-before")?.text == ArtifactRules.Format(before.MainStat), "Main stat before value is missing or changed during drafting.");
        var afterValue = main.Q<Label>(className: "artifact-stat-after");
        Check(afterValue != null && !afterValue.text.Contains("→") && afterValue.text != ArtifactRules.Format(before.MainStat),
            "Draft main stat after value is not a separate improved number.");
        var yellow = afterValue.resolvedStyle.color;
        Check(yellow.r > .9f && yellow.g > .65f && yellow.b < .7f, "Projected stat number is not yellow.");
        var arrow = main.Q(className: "artifact-stat-up-arrow");
        arrowStartY = arrow?.resolvedStyle.translate.y ?? 0;
        Check(arrow != null && arrow.resolvedStyle.opacity > 0, "Draft stat has no fading green upward arrow.");
        var green = arrow.resolvedStyle.color;
        Check(green.g > green.r && green.g > green.b, "Draft upward arrow is not green.");
        Check(JsonUtility.ToJson(inventory.Snapshot) == draftSnapshot && File.ReadAllText(accountPath) == savedDraft,
            "Drafting changed the live or saved inventory.");
    }

    private static void CheckDraftMovement()
    {
        Capture("DraftFeedbackRising");
        var arrow = root.Q(className: "artifact-stat-up-arrow");
        Check(arrow.resolvedStyle.translate.y < arrowStartY, "Draft upward arrow did not move upward over time.");
    }

    private static void CheckDraftSettled()
    {
        Capture("DraftFeedbackSettled");
        Check(Math.Abs(root.Q("enhance-level-delta").resolvedStyle.scale.value.x - 1) < .01f,
            "Draft level pulse did not settle back to normal scale.");
        CheckTrayRow(); CheckStatRowsInView();
        Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 1,
            "Material selection does not match the visible tray.");
        Check(inventory.FindArtifact("upgrade-target").Level == 5, "Drafting committed an upgrade.");
    }

    private static void OpenConfirmation()
    {
        Check(root.Q("artifact-picker-overlay") == null && root.Q("enhance-hero").style.display == DisplayStyle.Flex,
            "Closing material lookup failed to restore workshop artwork.");
        Click(root.Q<Button>("enhance-commit"));
    }

    private static void BeginCommit()
    {
        Capture("UpgradeConfirmation");
        Check(root.Q("artifact-confirm-overlay") != null, "Upgrade did not present confirmation.");
        inventoryCount = inventory.GetItemInventorySnapshot().Artifacts.Count; coinsBefore = inventory.Coins;
        Check(inventory.PreviewArtifactEnhancement("upgrade-target", 0, new[] { "material-00" }, out var preview, out var error),
            "Cannot preview the reviewed upgrade: " + error);
        expectedCost = preview.CoinCost;
        Click(root.Q<Button>("artifact-confirm-commit"));
        committed = true;
    }

    private static void FirstCommitFrame()
    {
        Capture("UpgradeExperienceStart");
        Check(root.Q("artifact-enhance-overlay").ClassListContains("enhance-upgrade-running"), "Upgrade lacks a timed experience filling phase.");
        firstXp = root.Q<ProgressBar>("enhance-xp").value;
        Check(!root.Q<Button>("enhance-commit").enabledInHierarchy, "Upgrade can be double-committed during experience animation.");
    }

    private static void CheckCommitProgress()
    {
        Capture("UpgradeExperienceFilling");
        var xp = root.Q<ProgressBar>("enhance-xp").value;
        Check(Math.Abs(xp - firstXp) > .01f, "Committed experience bar stayed fixed across timed frames.");
    }

    private static void CheckCommitted()
    {
        Capture("UpgradeSuccess");
        var target = inventory.FindArtifact("upgrade-target");
        Check(target.Level == 6, "Reviewed material did not reach the intended +6 milestone.");
        Check(inventory.FindArtifact("material-00") == null && inventory.GetItemInventorySnapshot().Artifacts.Count == inventoryCount - 1,
            "Committed upgrade did not consume exactly its reviewed material.");
        Check(inventory.Coins == coinsBefore - expectedCost && target.Favourite && target.EquippedCharacterId == fighterId,
            "Upgrade cost or favourite/equipment ownership changed incorrectly.");
        Check(root.Q<Label>("enhance-level").text == "+6", "Successful upgrade header does not show its current committed level.");
        Check(root.Q("artifact-enhance-overlay").ClassListContains("enhance-upgrade-revealed"),
            "Successful upgrade does not retain the current-level result header.");
        var headerColor = root.Q(className: "artifact-xp-heading").resolvedStyle.backgroundColor;
        Check(headerColor.b > headerColor.r && headerColor.b > .3f, "Successful upgrade header is not visibly highlighted.");
        Check(!root.Q("artifact-enhance-overlay").ClassListContains("enhance-upgrade-running"), "Upgrade did not leave the experience filling state.");
        var improved = Enumerable.Range(0, before.Substats.Count).Where(i => target.Substats[i].Value > before.Substats[i].Value).ToList();
        Check(improved.Count == 1, "+6 did not improve exactly one random affix.");
        Check(sawSuccessSparkle && sawAffixPulse && sawLevelPulse,
            "Success did not visibly sparkle and pulse the committed header/improved stat.");
        Check(sawVisibleAffix, "Improved affix feedback stayed outside the visible stat viewport.");
        Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 0,
            "Consumed materials remain in the draft tray.");
        CheckStatRowsInView();
        inventory.ReloadFromDisk();
        Check(inventory.FindArtifact("upgrade-target").Level == 6 && inventory.FindArtifact("material-00") == null,
            "Committed artifact upgrade was not persisted.");
    }

    private static void CheckEffectCleanup()
    {
        Capture("UpgradeSettled");
        var level = root.Q("enhance-level");
        Check(Math.Abs(level.resolvedStyle.scale.value.x - 1) < .01f, "Success level pulse did not settle.");
        Check(root.Query("screen-motion-sparkle").ToList().Count == 0, "Success particles did not clean themselves up.");
        foreach (var value in root.Q("enhance-stats").Query(className: "artifact-stat-current").ToList())
            Check(Math.Abs(value.resolvedStyle.scale.value.x - 1) < .01f, "Success affix value pulse did not settle.");
    }

    private static void CheckMaterialCap()
    {
        Click(root.Q<Button>("enhance-auto")); Pump();
        var tiles = root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList();
        Check(tiles.Count <= 15 && tiles.Count > 0, "Auto Add ignored the 15-material capacity or produced no materials.");
        CheckTrayRow(); Capture("HorizontalMaterialTray");
        var empty = root.Q("enhance-materials").Query<Button>().ToList().FirstOrDefault(b => b.name != null && b.name.StartsWith("enhance-empty-material-"));
        Check(empty != null, "Auto Add cannot reach next level using the seeded common materials.");
        Click(empty);
    }

    private static void CheckManualMaterialLimit()
    {
        var grid = root.Q("artifact-picker-grid");
        foreach (var id in Enumerable.Range(1, 34).Select(i => "material-" + i.ToString("D2")))
        {
            if (root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 15) break;
            var tile = grid.Q<Button>("picker-" + id);
            if (!tile.ClassListContains("material-selected")) Click(tile);
        }
        Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 15,
            "Cannot fill all 15 material slots.");
        var extra = grid.Query<Button>().ToList().First(b => b.name != null && b.name.StartsWith("picker-material-") && !b.ClassListContains("material-selected"));
        Click(extra);
        Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 15
            && root.Q<Label>("enhance-note").text.Contains("15"), "A sixteenth material bypassed the capacity guard.");
        var selected = grid.Query<Button>().ToList().First(b => b.ClassListContains("material-selected"));
        Click(selected);
        Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 14,
            "Full material tray cannot remove a selected source.");
        Click(extra);
        Check(root.Q("enhance-materials").Query(className: "enhance-material-tile").ToList().Count == 15,
            "Replacing a material in the full tray failed.");
        CheckTrayRow(); Capture("Horizontal15Materials");
    }

    private static void ObserveSuccessFeedback()
    {
        if (!committed || root?.Q<Label>("enhance-level")?.text != "+6") return;
        sawLevelPulse |= root.Q("enhance-level").resolvedStyle.scale.value.x > 1.001f;
        var current = root.Q("enhance-stats").Query(className: "artifact-stat-current").ToList();
        sawAffixPulse |= current.Any(value => value.resolvedStyle.scale.value.x > 1.001f);
        var sparkling = current.FirstOrDefault(value => value.Q("screen-motion-sparkle") != null);
        sawSuccessSparkle |= sparkling != null;
        if (sparkling != null)
        {
            if (sparkleStarted < 0) sparkleStarted = EditorApplication.timeSinceStartup;
            var scroll = root.Q("enhance-stats").GetFirstAncestorOfType<ScrollView>();
            var viewport = scroll.contentViewport.worldBound;
            var b = sparkling.worldBound;
            sawVisibleAffix |= b.yMin >= viewport.yMin - 1 && b.yMax <= viewport.yMax + 1;
            var arrow = sparkling.parent.parent.Q(className: "artifact-commit-arrow");
            if (arrow != null && arrow.resolvedStyle.opacity > .1f)
                Check(arrow.worldBound.xMin >= b.xMax - 1, "Committed improvement arrow overlaps the improved stat number.");
            if (!capturedSparkle && EditorApplication.timeSinceStartup - sparkleStarted >= .12)
            {
                Capture("UpgradeAffixSparkle"); capturedSparkle = true;
                Notes.Add("Observed committed header/stat pulse and target sparkle at " + Size);
            }
        }
    }

    private static void Pump() => Preview.GetMethod("Render", SP).Invoke(null, new object[] { Size, "Pump", false });
    private static void Capture(string name)
    {
        // Capture the frame already painted by Pump; an extra panel update here would advance its animation.
        var target = (RenderTexture)Preview.GetField("_target", SP).GetValue(null);
        var active = RenderTexture.active; Texture2D pixels = null;
        try
        {
            RenderTexture.active = target;
            pixels = new Texture2D(Size.x, Size.y, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, Size.x, Size.y), 0, 0); pixels.Apply();
            File.WriteAllBytes(Folder + "/" + name + "-" + Size.x + "x" + Size.y + ".png", pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = active; if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); }
    }
    private static void Click(Button button)
    {
        Check(button != null && button.enabledInHierarchy, "Clickable control is missing or disabled in " + activeStep);
        typeof(Clickable).GetMethod("Invoke", IP).Invoke(button.clickable, new object[] { null });
    }
    private static void SendFocusedEscape()
    {
        var focused = root.panel.focusController.focusedElement as VisualElement;
        Check(focused != null, "No focused element receives keyboard input.");
        using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            focused.SendEvent(escape);
    }
    private static void Bounds(VisualElement element)
    {
        Check(element != null, "Expected visual element is missing.");
        var b = element.worldBound;
        Check(b.width > 0 && b.height > 0 && b.xMin >= -1 && b.yMin >= 83 && b.xMax <= Size.x + 1 && b.yMax <= Size.y + 1,
            element.name + " exceeds viewport: " + b);
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, IP).SetValue(target, value);
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        Notes.Add(error == null ? "PASS: " + assertions + " assertions, native Unity render frames and isolated persisted transactions."
            : "FAIL in " + activeStep + ": " + error);
        File.WriteAllLines(Folder + "/Verification.txt", Notes);
        if (accountPath != null && File.Exists(accountPath)) File.Delete(accountPath);
        Preview.GetMethod("Cleanup", SP).Invoke(null, null);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
