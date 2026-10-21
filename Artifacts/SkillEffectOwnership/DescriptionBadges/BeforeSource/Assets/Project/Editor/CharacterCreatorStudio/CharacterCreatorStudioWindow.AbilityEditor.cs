#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        private Action _kitAbilityPreviewRefresh;
        private readonly HashSet<string> _kitAbilityOpenFolds = new HashSet<string>();
        private readonly HashSet<string> _kitAbilityCollapsedNodes = new HashSet<string>();
        private EffectTargetScope _kitAbilityRootTarget;

        private void BuildKitAbilityEditor(int slot)
        {
            var character = EditableKit();
            var source = _draft?.SourceCharacter;
            var ultimate = slot == 0;
            var skill = ultimate ? null : character.Skills.FirstOrDefault(s => s.Slot == slot);
            var rank = skill?.Ranks.FirstOrDefault(r => r.Rank == _kitRank);
            var tier = ultimate ? character.UltimateTiers.FirstOrDefault(t => t.Tier == _kitTier) : null;
            var effect = ultimate ? tier?.Effect : rank?.Effect;
            var scope = ultimate ? "C" + _kitTier : "Rank " + _kitRank;
            var path = ultimate ? "ultimate.tier" + _kitTier : "skill" + slot + ".rank" + _kitRank;
            KitHeader(ultimate ? "Ultimate" : "Skill " + slot, null);
            // The inspector scrolls independently, so the card remains visible while editing a long flow.
            var ownership = _inspector.contentContainer.Children().ToArray();
            _inspector.Clear(); _inspector.AddToClassList("studio-ability-inspector");
            var preview = new VisualElement { name = "studio-ability-preview" }; preview.AddToClassList("studio-ability-preview"); _inspector.Add(preview);
            BuildAbilityGamePreview(preview, character, source, slot);
            var assetActions = new Foldout { text = "Asset & save", value = false }; _inspector.Add(assetActions);
            foreach (var element in ownership) assetActions.Add(element);
            var editor = new VisualElement(); editor.AddToClassList("studio-ability-editor"); _host.Add(editor);

            if (!ultimate && skill == null) { Body(editor, "Missing skill slot.", "studio-error"); return; }
            if ((ultimate && tier == null) || (!ultimate && rank == null))
            {
                Button(editor, "Create " + scope, () => Edit("Create " + scope, () => {
                    if (ultimate) character.UltimateTiers.Add(new UltimateTierDefinition { Tier = _kitTier, Effect = new EffectDefinition() });
                    else skill.Ranks.Add(new SkillRankDefinition { Rank = _kitRank, Effect = new EffectDefinition() });
                }, true));
                return;
            }
            var settings = Node(editor, "BASE CARD SETTINGS"); settings.name = "studio-ability-base-settings";
            settings.AddToClassList("studio-ability-settings");
            if (ultimate)
            {
                EnumControl(settings, "Category · all C0–C5", RootUltimateCategory(character, tier), v => {
                    character.HasUltimateCardSettings = true; character.UltimateCategory = (CardCategory)v;
                }, true);
                EnumControl(settings, "Target · all C0–C5", RootUltimateTarget(character, tier), v => {
                    character.HasUltimateCardSettings = true; character.UltimateTargetScope = (EffectTargetScope)v;
                }, true);
                TextControl(settings, "Tier label", tier.SourceLabel, v => tier.SourceLabel = v);
            }
            else
            {
                EnumControl(settings, "Category · all ranks", skill.Category, v => skill.Category = (CardCategory)v, true);
                EnumControl(settings, "Target · all ranks", skill.TargetScope, v => {
                    skill.TargetScope = (EffectTargetScope)v;
                    CardRules.ApplySharedSkillSettings(skill);
                }, true);
                ToggleControl(settings, "Override category for " + scope, rank.HasCardKind, v => rank.HasCardKind = v, true);
                if (rank.HasCardKind)
                    EnumControl(settings, "Category · " + scope, rank.Category, v => rank.Category = (CardCategory)v, true);
                Body(settings, "Target and damage choreography are shared by every rank. A rank may override its card category.", "studio-subtle");
            }
            var sharedAnimation = ultimate ? RootUltimateDamageAnimation(character, tier) : RootSkillDamageAnimation(skill);
            var sharedTarget = ultimate ? RootUltimateTarget(character, tier) : CardRules.ResolveTargetScope(skill);
            _kitAbilityRootTarget = sharedTarget;
            if (ultimate ? character.UltimateTiers.Any(t => AbilityOperations(t?.Effect).Any(e => e.Kind == EffectKind.Damage)) :
                skill.Ranks.Any(r => AbilityOperations(r?.Effect).Any(e => e.Kind == EffectKind.Damage)))
                BuildAbilityDamageSettings(settings, sharedAnimation, sharedTarget, ultimate ? "all C0–C5" : "all ranks", attack => {
                    if (ultimate)
                    {
                        character.HasUltimateCardSettings = true;
                        character.UltimateDamageAnimation = attack;
                    }
                    else
                    {
                        skill.DamageAnimation = attack;
                        CardRules.ApplySharedSkillSettings(skill);
                    }
                });
            if (effect == null)
            {
                Button(editor, "Create main action", () => Edit("Create main action", () => {
                    if (ultimate) tier.Effect = new EffectDefinition(); else rank.Effect = new EffectDefinition();
                }, true));
                return;
            }

            Section(editor, scope.ToUpperInvariant() + " · ACTION FLOW");
            DrawAbilityFlow(editor, effect, path);
            BuildNativeWordingEditor(_inspector, NativeWordingTarget(ultimate ? "Ultimate" : "Skill " + slot));
            Button(preview, "Open Battle Lab", () => { EnsureLabSlots(); _labActors[0].Tier = _kitTier; ShowWorkspace("lab"); });
            if (!ultimate) Button(_inspector, "Variants & upgrades", () => ShowWorkspace("abilities"));
        }

        private void BuildAbilityGamePreview(VisualElement parent, CharacterDefinition character, CharacterObject source, int slot)
        {
            var ultimate = slot == 0;
            var skill = ultimate ? null : character.Skills.FirstOrDefault(s => s.Slot == slot);
            var selected = ultimate ? _kitTier : _kitRank;
            var art = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
            var showcase = new VisualElement(); showcase.AddToClassList("studio-ability-showcase"); parent.Add(showcase);
            showcase.RegisterCallback<GeometryChangedEvent>(e => showcase.EnableInClassList("studio-ability-showcase-compact", e.newRect.width < 320));
            Body(showcase, ultimate ? "ULTIMATE DETAILS" : "SKILL DETAILS", "studio-ability-showcase-title");
            var choices = new VisualElement(); choices.AddToClassList("studio-ability-rank-choices"); showcase.Add(choices);
            var icons = new List<Action>();
            Action<int> choose = value => { if (ultimate) _kitTier = value; else _kitRank = value; Rebuild(); ResetWorkspaceScroll(); };
            if (ultimate)
            {
                var face = AbilityCardFace(source, slot, 3, art, out _); choices.Add(face);
                var tiers = new VisualElement(); tiers.AddToClassList("studio-ability-tier-choices"); showcase.Add(tiers);
                for (var i = 0; i <= 5; i++)
                {
                    var chosen = i;
                    var button = Button(tiers, "C" + i, () => choose(chosen));
                    button.name = "studio-kit-tier-" + i; button.EnableInClassList("studio-active", selected == i);
                }
            }
            else
            {
                for (var i = 1; i <= 3; i++)
                {
                    var chosen = i;
                    if (i > 1) Body(choices, "→", "studio-ability-rank-arrow");
                    var button = new Button(() => choose(chosen)) { name = "studio-kit-rank-" + i, tooltip = "Edit rank " + i };
                    button.AddToClassList("studio-ability-rank-choice"); button.EnableInClassList("studio-ability-rank-selected", i == selected);
                    button.Add(AbilityCardFace(source, slot, i, art, out var badge));
                    Body(button, "R" + i, "studio-ability-rank-caption"); choices.Add(button);
                    icons.Add(() => {
                        var rank = skill?.Ranks.FirstOrDefault(r => r.Rank == chosen);
                        var category = rank?.HasCardKind == true ? rank.Category : skill?.Category ?? CardCategory.Attack;
                        badge.sprite = art?.Type(StudioAssetCategories.Native(category));
                        badge.style.display = badge.sprite == null ? DisplayStyle.None : DisplayStyle.Flex;
                    });
                }
            }
            var copy = new VisualElement(); copy.AddToClassList("studio-ability-game-copy"); parent.Add(copy);
            var header = new VisualElement(); header.AddToClassList("studio-ability-game-header"); copy.Add(header);
            var type = new VisualElement(); type.AddToClassList("studio-live-card-badges"); header.Add(type);
            var title = new Label(); title.AddToClassList("studio-ability-game-title"); header.Add(title);
            var badges = new VisualElement(); badges.AddToClassList("studio-live-card-badges"); header.Add(badges);
            var description = new Label { name = "studio-ability-game-description", enableRichText = true };
            description.AddToClassList("studio-ability-game-description"); copy.Add(description);
            var keywords = new Label { name = "studio-ability-game-keywords", enableRichText = true };
            keywords.AddToClassList("studio-ability-game-keywords"); copy.Add(keywords);
            _kitAbilityPreviewRefresh = () => {
                var owner = new FighterState { Definition = character, ConstellationTier = ultimate ? selected : _kitTier };
                var state = new CardState { Kind = ultimate ? CardKind.Ultimate : CardKind.Skill, Rank = selected, UltimateTier = selected, SkillId = skill?.Id };
                var details = CombatDetailFormatter.ExtractCardDetails(state, owner, source);
                title.text = character.DisplayName + " · " + (ultimate ? "Ultimate · C" + selected : "Skill " + slot + " · R" + selected);
                description.text = details.desc;
                keywords.text = details.keywords;
                keywords.style.display = string.IsNullOrWhiteSpace(details.keywords) ? DisplayStyle.None : DisplayStyle.Flex;
                type.Clear(); badges.Clear();
                AddPreviewBadge(type, art?.SkillIcon(state, owner), "Card type / target");
                foreach (var status in details.statuses) AddPreviewBadge(badges, status.Icon, status.Name);
                foreach (var refresh in icons) refresh();
            };
            _kitAbilityPreviewRefresh();
        }

        private static VisualElement AbilityCardFace(CharacterObject source, int slot, int rank, CharacterKitArtSO art, out Image badge)
        {
            var skill = slot == 1 ? source?.Skill1 : source?.Skill2;
            var sprite = slot == 0 ? source?.Ultimate?.icon : skill?.cardIcon;
            if (sprite == null) sprite = source?.FighterIcon;
            var face = new VisualElement(); face.AddToClassList("studio-ability-card-face");
            var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            image.AddToClassList("studio-ability-card-art"); face.Add(image);
            var frame = new Image { image = art?.Rank(rank), scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            frame.AddToClassList("studio-ability-card-frame"); face.Add(frame);
            badge = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            badge.AddToClassList("studio-ability-card-type"); face.Add(badge);
            face.pickingMode = PickingMode.Ignore;
            return face;
        }

        private static IEnumerable<EffectOperationDefinition> AbilityOperations(EffectDefinition root)
        {
            if (root == null) yield break;
            yield return root;
            if (root.Sequence != null) foreach (var step in root.Sequence) if (step?.Effect != null) yield return step.Effect;
        }

        private static CardCategory RootUltimateCategory(CharacterDefinition character, UltimateTierDefinition selected)
        {
            if (character?.HasUltimateCardSettings == true) return character.UltimateCategory;
            var root = character?.UltimateTiers?.FirstOrDefault(t => t?.Tier == 0) ?? selected;
            return root?.Category ?? CardCategory.Attack;
        }

        private static EffectTargetScope RootUltimateTarget(CharacterDefinition character, UltimateTierDefinition selected)
        {
            if (character?.HasUltimateCardSettings == true) return character.UltimateTargetScope;
            var root = character?.UltimateTiers?.FirstOrDefault(t => t?.Tier == 0) ?? selected;
            return root?.Effect?.Target ?? EffectTargetScope.SelectedEnemy;
        }

        private static DamageAttackDefinition RootUltimateDamageAnimation(CharacterDefinition character, UltimateTierDefinition selected)
        {
            if (character?.HasUltimateCardSettings == true && character.UltimateDamageAnimation != null)
                return character.UltimateDamageAnimation.Clone();
            var root = character?.UltimateTiers?.FirstOrDefault(t => t?.Tier == 0) ?? selected;
            return FirstDamageAnimation(root?.Effect) ?? new DamageAttackDefinition();
        }

        private static DamageAttackDefinition RootSkillDamageAnimation(SkillDefinition skill)
            => skill?.DamageAnimation?.Clone() ?? FirstDamageAnimation(skill?.Ranks?.Select(r => r?.Effect)) ?? new DamageAttackDefinition();

        private static DamageAttackDefinition FirstDamageAnimation(IEnumerable<EffectDefinition> roots)
        {
            if (roots == null) return null;
            foreach (var root in roots)
            {
                var result = FirstDamageAnimation(root);
                if (result != null) return result;
            }
            return null;
        }

        private static DamageAttackDefinition FirstDamageAnimation(EffectDefinition root)
        {
            var damage = AbilityOperations(root).FirstOrDefault(effect => effect.Kind == EffectKind.Damage);
            return damage?.Attack?.Clone();
        }

        private string AbilityEffectSummary(EffectOperationDefinition effect)
            => effect?.DamageRecipe != null ? Humanize(effect.DamageRecipe.Family.ToString()) + " damage · " + CardDescriptionText.TargetName(effect.Target) :
                effect?.Kind == EffectKind.Damage ? "Damage · " + CardDescriptionText.TargetName(_kitAbilityRootTarget) : EffectSummary(effect);

        private void BuildAbilityDamageSettings(VisualElement parent, DamageAttackDefinition attack, EffectTargetScope target, string scope,
            Action<DamageAttackDefinition> setRootAnimation)
        {
            attack = attack ?? new DamageAttackDefinition();
            var animation = Node(parent, "DAMAGE ANIMATION · " + scope.ToUpperInvariant()); animation.name = "studio-ability-damage-animation";
            Body(animation, "One hit setup is shared by every damage action in this card.", "studio-subtle");
            var area = target == EffectTargetScope.AllEnemies || target == EffectTargetScope.AllAllies;
            var templates = DamageAnimationTemplates.All.Where(t => t.Area == area).ToList();
            var names = new List<string> { "Custom" }; names.AddRange(templates.Select(t => t.Name));
            var matching = templates.FirstOrDefault(t => t.Range == attack.Range && t.Hits == attack.ResolvedHitCount);
            ChoiceControl(animation, "Preset", names, matching?.Name ?? "Custom", value => {
                var template = templates.FirstOrDefault(t => t.Name == value); if (template == null) return;
                setRootAnimation(attack); attack.HitCount = template.Hits; attack.Range = template.Range;
            }, true);
            var preset = animation[animation.childCount - 1] as PopupField<string>;
            _kitAbilityPreviewRefresh += () => preset?.SetValueWithoutNotify(templates.FirstOrDefault(t => t.Range == attack.Range && t.Hits == attack.ResolvedHitCount)?.Name ?? "Custom");
            var hits = new SliderInt("Hits", 1, 10) { value = attack.ResolvedHitCount, showInputField = true, name = "studio-damage-hits-root" };
            hits.RegisterValueChangedCallback(e => Edit("Edit shared damage animation", () => {
                setRootAnimation(attack); attack.HitCount = Mathf.Clamp(e.newValue, 1, 10);
            })); animation.Add(hits);
            EnumControl(animation, "Range", attack.Range, v => { setRootAnimation(attack); attack.Range = (AttackRange)v; });
            EnumControl(animation, "Hit reaction", attack.Reaction, v => { setRootAnimation(attack); attack.Reaction = (HitReaction)v; });
            EnumControl(animation, "Reaction on", attack.ReactionTiming, v => { setRootAnimation(attack); attack.ReactionTiming = (HitReactionTiming)v; });
            KitLiveLabel(animation, () => attack.ResolvedHitCount + " hits  ·  " + Humanize(attack.Range.ToString()) + "  ·  " + Humanize(attack.Reaction.ToString()) + " / " + Humanize(attack.ReactionTiming.ToString()), "studio-ability-animation-summary");
        }

        private void DrawAbilityFlow(VisualElement parent, EffectDefinition root, string path)
        {
            var tools = Row(parent);
            Button(tools, "Collapse all actions", () => rootVisualElement.Query<Foldout>(className: "studio-ability-action-fold").ForEach(f => f.value = false));
            Button(tools, "Expand all actions", () => rootVisualElement.Query<Foldout>(className: "studio-ability-action-fold").ForEach(f => f.value = true));
            var flow = new VisualElement { name = "studio-kit-action-flow" }; flow.AddToClassList("studio-ability-flow"); parent.Add(flow);
            var preTimings = new[] { CardEffectTiming.BeforeAction, CardEffectTiming.Damaging };
            foreach (var timing in preTimings)
                if (root.Sequence != null) foreach (var step in root.Sequence.Where(s => s != null && s.Timing == timing)) DrawAbilityFlowStep(flow, root, step, path);
            var primary = AbilityFlowNode(flow, "MAIN ACTION", "studio-ability-flow-main", path + ".main", root); primary.name = "studio-kit-primary";
            DrawAbilityOperation(primary, root, path, true);
            foreach (var timing in new[] { CardEffectTiming.AfterDamage, CardEffectTiming.AfterAction })
                if (root.Sequence != null) foreach (var step in root.Sequence.Where(s => s != null && s.Timing == timing)) DrawAbilityFlowStep(flow, root, step, path);
            var add = Node(parent, "ADD ACTION"); add.AddToClassList("studio-ability-add-action");
            var kind = new EnumField("Action", EffectKind.ApplyStatus); add.Add(kind);
            var newTiming = new EnumField("Run", CardEffectTiming.AfterAction); add.Add(newTiming);
            Button(add, "+ Add action", () => Edit("Add action", () => {
                if (root.Sequence == null) root.Sequence = new List<CardEffectStep>();
                var operation = CreateVariantOperation((EffectKind)kind.value);
                if (operation.Kind == EffectKind.Damage) operation.DamageRecipe = new DamageEffectRecipe { Family = DamageFamily.Additional, CannotCrit = true, CannotBlock = true };
                root.Sequence.Add(new CardEffectStep { Timing = (CardEffectTiming)newTiming.value, Effect = operation });
            }, true)).name = "studio-kit-add-step";
            KitFold(parent, "Advanced card rules", path + ".advanced", p => KitMembers(p, root, path, 0,
                "Sequence", "Attack", "Kind", "Target", "Scaling", "ScalingStat", "CoefficientBp", "Magnitude", "Family", "DamageElement", "KeywordId", "StatusRecipe", "StatusStackCount", "StatusApplyChanceBp", "StatusDurationOverride", "HealValue", "HealCoefficientBp", "HealScalingStat", "PowerGaugeAmount", "Conditions"));
        }

        private VisualElement AbilityFlowNode(VisualElement parent, string title, string tone, string key, EffectOperationDefinition effect)
        {
            if (parent.childCount > 0) Body(parent, "↓", "studio-ability-flow-arrow");
            var node = new Foldout { text = title + " · " + AbilityEffectSummary(effect), value = !_kitAbilityCollapsedNodes.Contains(key) };
            node.AddToClassList("studio-node"); node.AddToClassList("studio-ability-action-fold"); node.AddToClassList("studio-ability-flow-node"); node.AddToClassList(tone); parent.Add(node);
            node.RegisterValueChangedCallback(e => { if (e.target != node) return; if (e.newValue) _kitAbilityCollapsedNodes.Remove(key); else _kitAbilityCollapsedNodes.Add(key); });
            _kitAbilityPreviewRefresh += () => node.text = title + " · " + AbilityEffectSummary(effect);
            return node;
        }

        private void DrawAbilityFlowStep(VisualElement parent, EffectDefinition root, CardEffectStep step, string path)
        {
            var index = root.Sequence.IndexOf(step);
            var node = AbilityFlowNode(parent, Humanize(step.Timing.ToString()).ToUpperInvariant(), "studio-ability-flow-followup", path + ".step" + (step.Effect?.Id ?? index.ToString()), step.Effect);
            node.name = "studio-kit-step-" + index;
            EnumControl(node, "Run", step.Timing, v => step.Timing = (CardEffectTiming)v, true);
            DrawAbilityOperation(node, step.Effect, path + ".step" + index, false);
            var controls = Row(node);
            var previous = Button(controls, "↑ Earlier", () => MoveAbilityStep(root, step, -1));
            previous.SetEnabled(root.Sequence.Take(index).Any(s => s?.Timing == step.Timing));
            var next = Button(controls, "↓ Later", () => MoveAbilityStep(root, step, 1));
            next.SetEnabled(root.Sequence.Skip(index + 1).Any(s => s?.Timing == step.Timing));
            Button(controls, "Remove", () => Edit("Remove action", () => root.Sequence.Remove(step), true));
        }

        private void MoveAbilityStep(EffectDefinition root, CardEffectStep step, int offset)
        {
            var index = root.Sequence.IndexOf(step); var target = index + offset;
            if (index < 0) return;
            while (target >= 0 && target < root.Sequence.Count && root.Sequence[target]?.Timing != step.Timing) target += offset;
            if (target < 0 || target >= root.Sequence.Count) return;
            Edit("Reorder action", () => { root.Sequence.RemoveAt(index); root.Sequence.Insert(target, step); }, true);
        }

        private void DrawAbilityOperation(VisualElement parent, EffectOperationDefinition effect, string path, bool primary)
        {
            if (effect == null) { Body(parent, "Missing action.", "studio-error"); return; }
            KitLiveLabel(parent, () => AbilityEffectSummary(effect), "studio-ability-action-summary");
            EnumControl(parent, "Action", effect.Kind, v => {
                effect.Kind = (EffectKind)v;
                if (effect.Kind != EffectKind.Damage) effect.DamageRecipe = null;
                if (effect.Kind == EffectKind.ApplyStatus && effect.StatusRecipe == null) effect.StatusRecipe = NewVariantStatus();
            }, true);
            if (!primary && effect.Kind == EffectKind.Damage)
            {
                if (effect.DamageRecipe == null)
                {
                    Button(parent, "Configure follow-up packet", () => Edit("Configure follow-up packet", () => effect.DamageRecipe = new DamageEffectRecipe {
                        Family = effect.Family, Scaling = effect.Scaling, ScalingStat = effect.ScalingStat,
                        CoefficientBp = effect.CoefficientBp, FixedAmount = effect.Magnitude, Profile = effect.DamageProfile?.Clone() }, true));
                    DrawAbilityConditions(parent, effect.Conditions, path + ".conditions");
                    return;
                }
                EnumControl(parent, "Affects", effect.Target, v => effect.Target = (EffectTargetScope)v);
                Body(parent, "One follow-up packet. It does not replay the main attack animation.", "studio-subtle");
                DrawDamagePacket(parent, effect.DamageRecipe, path);
                DrawAbilityConditions(parent, effect.Conditions, path + ".conditions");
                return;
            }
            if (effect.Kind == EffectKind.Damage)
                Body(parent, "Damage target follows the root card target setting.", "studio-subtle");
            else EnumControl(parent, "Affects", effect.Target, v => effect.Target = (EffectTargetScope)v, true);
            if (effect.Kind == EffectKind.Damage)
            {
                EnumControl(parent, "Scale from", effect.Scaling, v => effect.Scaling = (StatScaling)v, true);
                if (effect.Scaling == StatScaling.Fixed) IntControl(parent, "Damage", effect.Magnitude, v => effect.Magnitude = v);
                else
                {
                    if (effect.Scaling == StatScaling.SpecificStat) EnumControl(parent, "Stat", effect.ScalingStat, v => effect.ScalingStat = (StatId)v);
                    BasisPointPercentControl(parent, "Damage (%)", effect.CoefficientBp, v => effect.CoefficientBp = v);
                }
                EnumControl(parent, "Element", effect.DamageElement, v => effect.DamageElement = (ElementId)v);
                var keywordRecipes = StandardEffectDatabase.CreateAttackEffects();
                var keywords = new List<string> { "None" }; keywords.AddRange(keywordRecipes.Select(r => CombatTextCatalog.CanonicalKeyword(r.Id)).Distinct());
                ChoiceControl(parent, "Keyword", keywords, string.IsNullOrEmpty(effect.KeywordId) ? "None" : CombatTextCatalog.CanonicalKeyword(effect.KeywordId), v =>
                    effect.KeywordId = v == "None" ? null : keywordRecipes.FirstOrDefault(r => CombatTextCatalog.CanonicalKeyword(r.Id) == v)?.Id ?? v);
                KitFold(parent, "Damage rules & conditions", path + ".damage", p => {
                    EnumControl(p, "Damage family", effect.Family, v => effect.Family = (DamageFamily)v);
                    ToggleControl(p, "Custom damage profile", effect.DamageProfile != null, v => effect.DamageProfile = v ? new DamageProfileDefinition { Id = effect.Id + ".profile" } : null, true);
                    if (effect.DamageProfile != null) DrawDamageProfile(p, effect.DamageProfile);
                    DrawAbilityConditions(p, effect.Conditions, path + ".conditions");
                });
            }
            else if (effect.Kind == EffectKind.ApplyStatus)
            {
                var recipes = _document.Statuses.Select(s => s.Definition).Concat(StandardEffectDatabase.CreateStatusRecipes()).Where(s => s != null).GroupBy(s => s.Id).Select(g => g.First()).ToList();
                var choices = new List<string> { "Custom status" }; choices.AddRange(recipes.Select(r => r.Id));
                var existing = effect.StatusRecipe;
                ChoiceControl(parent, "Status", choices, recipes.Any(r => r.Id == existing?.Id) ? existing.Id : "Custom status", v => {
                    effect.StatusRecipe = v == "Custom status" ? existing?.Clone() ?? NewVariantStatus() : recipes.First(r => r.Id == v).Clone();
                }, true);
                if (effect.StatusRecipe != null)
                {
                    KitLiveLabel(parent, () => effect.StatusRecipe.NameKey ?? effect.StatusRecipe.Id, "studio-ability-status-name");
                    IntControl(parent, "Turns (0 = status default)", effect.StatusDurationOverride, v => effect.StatusDurationOverride = v);
                    IntControl(parent, "Stacks", effect.StatusStackCount, v => effect.StatusStackCount = v);
                    BasisPointPercentControl(parent, "Apply chance (%)", effect.StatusApplyChanceBp, v => effect.StatusApplyChanceBp = v);
                    KitFold(parent, "Status definition & modifiers", path + ".status", p => DrawAbilityStatusDefinition(p, effect.StatusRecipe, path + ".status"));
                    DrawGrantedReactions(parent, effect.StatusRecipe, path + ".status");
                    if ((effect.StatusRecipe.Behavior & StatusBehavior.Stance) != 0 || effect.StatusRecipe.StanceChildren.Count > 0)
                        DrawStanceChildren(parent, effect.StatusRecipe, path + ".status");
                }
            }
            else if (effect.Kind == EffectKind.Heal)
            {
                if (effect.HealValue != null)
                {
                    EnumControl(parent, "Recover from", effect.HealValue.Source, v => effect.HealValue.Source = (EffectValueSource)v, true);
                    BasisPointPercentControl(parent, "Recovery (%)", effect.HealValue.CoefficientBp, v => effect.HealValue.CoefficientBp = v);
                    IntControl(parent, "Fixed recovery", effect.HealValue.FixedAmount, v => effect.HealValue.FixedAmount = v);
                }
                else
                {
                    EnumControl(parent, "Recover from", effect.HealScalingStat, v => effect.HealScalingStat = (StatId)v);
                    BasisPointPercentControl(parent, "Recovery (%)", effect.HealCoefficientBp, v => effect.HealCoefficientBp = v);
                }
            }
            else if (effect.Kind == EffectKind.ChangePowerGauge) IntControl(parent, "Power Gauge change", effect.PowerGaugeAmount, v => effect.PowerGaugeAmount = v);
            else if (effect.Kind == EffectKind.ModifyCardRank) IntControl(parent, "Rank change", effect.Magnitude, v => effect.Magnitude = v);

            if (effect.Kind != EffectKind.Damage) DrawAbilityConditions(parent, effect.Conditions, path + ".conditions");
            if (!primary) KitFold(parent, "Advanced action fields", path + ".advanced", p => KitMembers(p, effect, path, 0,
                "DamageRecipe", "Attack", "Kind", "Target", "Conditions", "Scaling", "ScalingStat", "CoefficientBp", "Magnitude", "DamageElement", "KeywordId", "StatusRecipe", "StatusDurationOverride", "StatusStackCount", "StatusApplyChanceBp", "HealValue", "HealScalingStat", "HealCoefficientBp", "PowerGaugeAmount"));
        }

        private void DrawAbilityStatusDefinition(VisualElement parent, StatusRecipeDefinition status, string path)
        {
            TextControl(parent, "Name", status.NameKey, v => status.NameKey = v);
            EnumControl(parent, "Polarity", status.Polarity, v => status.Polarity = (StatusPolarity)v);
            IntControl(parent, "Default turns", status.DefaultDuration, v => status.DefaultDuration = v);
            IntControl(parent, "Maximum stacks", status.MaxStacks, v => status.MaxStacks = v);
            foreach (var modifier in status.Modifiers.ToArray())
            {
                var box = Node(parent, "MODIFY STAT / DAMAGE");
                EnumControl(box, "Modify", modifier.ResolvedTarget, v => { modifier.Target = (ModifierTarget)v; modifier.SerializedTarget = -1; }, true);
                if (modifier.ResolvedTarget == ModifierTarget.Stat)
                    EnumControl(box, "Stat", modifier.ResolvedStat, v => { modifier.Stat = (StatId)v; modifier.SerializedStat = -1; }, true);
                if (modifier.ResolvedTarget == ModifierTarget.StatBundle)
                    EnumControl(box, "Stat group", modifier.ResolvedBundle, v => { modifier.Bundle = (StatBundleKind)v; modifier.SerializedBundle = -1; });
                EnumControl(box, "Change by", modifier.ResolvedOperation, v => { modifier.Operation = (ModifierOperation)v; modifier.SerializedOperation = -1; }, true);
                var percentage = modifier.ResolvedOperation != ModifierOperation.Flat ||
                    (modifier.ResolvedTarget == ModifierTarget.Stat && modifier.ResolvedStat >= StatId.Pierce);
                if (percentage) BasisPointPercentControl(box, "Amount (%)", modifier.Amount, v => modifier.Amount = v);
                else IntControl(box, "Amount", modifier.Amount, v => modifier.Amount = v);
                KitFold(box, "Advanced modifier", path + ".modifier" + status.Modifiers.IndexOf(modifier), p => KitMembers(p, modifier, path, 0));
                Button(box, "Remove modifier", () => Edit("Remove status modifier", () => status.Modifiers.Remove(modifier), true));
            }
            Button(parent, "+ Stat / damage modifier", () => Edit("Add status modifier", () => status.Modifiers.Add(new StatModifierDefinition {
                Stat = StatId.Attack, Operation = ModifierOperation.PercentOfBase, Amount = 1000 }), true));
            KitFold(parent, "Advanced status rules", path + ".advanced", p => KitMembers(p, status, path, 0,
                "NameKey", "Polarity", "DefaultDuration", "MaxStacks", "Modifiers", "Reactions", "StanceChildren"));

        }
    }
}
#endif
