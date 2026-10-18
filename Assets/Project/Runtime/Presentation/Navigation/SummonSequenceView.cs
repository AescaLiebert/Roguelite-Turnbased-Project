using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Run;
using FightingAllstar.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Presentation of an already committed receipt. Never rolls or grants rewards.</summary>
    public sealed class SummonSequenceView : IDisposable
    {
        // Starting values measured against Gacha_pull.mp4: fast reward arrivals, ~1 s red X,
        // then artwork/name slam. See ArtReference/GachaSequence.md for comparison/playtest criteria.
        public const float OpeningDuration = .45f, ArrivalDuration = .2f;
        public const float PremiumDuration = 4.1f, FinalHoldDuration = .65f;
        public const float PremiumHoldTime = 2.6f, PremiumExitTime = 3.8f;
        public const float ObtainTileSize = 154f;
        private readonly VisualElement _overlay, _board, _grid, _cinematic, _heroStage, _nameBar, _flash;
        private readonly Image _hero, _echo;
        private readonly Label _name, _rarity, _attribute, _counter, _hint, _crossWord, _header, _boardWord;
        private readonly Button _skip, _advance, _close;
        private readonly SummonSequenceGraphic _boardGraphic, _boardLight, _effect, _foreground;
        private readonly SummonSequenceBand _band;
        private readonly ScrollView _itemScroll;
        private readonly List<ObtainRewardCell> _cards = new List<ObtainRewardCell>();
        private readonly List<float> _starts = new List<float>();
        private readonly List<CharacterObject> _draws = new List<CharacterObject>();
        private readonly SummonSequenceAudio _audio = new SummonSequenceAudio();
        private IVisualElementScheduledItem _tick;
        private Action _completed;
        private double _lastTick;
        private float _elapsed, _loopElapsed;
        private int _acknowledgedPremium = -1;
        private int _activePremium = -1, _lastArrival = -1, _lastCharge = -1, _lastImpact = -1;
        private bool _disposed;
        private bool _itemMode;
        public bool IsPlaying { get; private set; }
        public bool IsAwaitingClose { get; private set; }
        public bool IsAwaitingRevealInput { get; private set; }
        // Authored animation duration. Player inspection time is intentionally unbounded.
        public float Duration { get; private set; }

        public SummonSequenceView(VisualElement root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var sheet = Resources.Load<StyleSheet>("UI/SummonSequence");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            _overlay = Element("summon-sequence", "summon-sequence");
            _overlay.pickingMode = PickingMode.Position;
            _overlay.focusable = true;
            _overlay.RegisterCallback<KeyDownEvent>(OnKey);
            _overlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (!IsAwaitingClose) return;
                for (var target = evt.target as VisualElement; target != null; target = target.parent)
                    if (_itemMode && target is ObtainRewardCell) return;
                Close();
            });
            _overlay.RegisterCallback<DetachFromPanelEvent>(_ => Cancel());
            _overlay.RegisterCallback<GeometryChangedEvent>(_ => { if (!_disposed) RenderSafely(_elapsed, false); });
            _board = Element("summon-reward-board", "summon-reward-board");
            _boardGraphic = new SummonSequenceGraphic(false); _board.Add(_boardGraphic);
            _boardWord = new Label("SUMMON RESULTS") { pickingMode = PickingMode.Ignore };
            _boardWord.AddToClassList("summon-board-watermark"); _boardWord.AddToClassList("heavy"); _board.Add(_boardWord);
            _header = new Label("REWARDS ACQUIRED"); _header.AddToClassList("summon-board-title"); _header.AddToClassList("heavy"); _board.Add(_header);
            _counter = new Label(); _counter.AddToClassList("summon-board-count"); _board.Add(_counter);
            _grid = Element("summon-reward-grid", "summon-reward-grid"); _board.Add(_grid);
            _itemScroll = new ScrollView(ScrollViewMode.Horizontal) { name = "obtain-item-scroll",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            _itemScroll.AddToClassList("obtain-item-scroll"); _board.Add(_itemScroll); _itemScroll.style.display = DisplayStyle.None;
            _itemScroll.contentContainer.style.alignItems = Align.Center;
            _boardLight = new SummonSequenceGraphic(false, true) { name = "summon-board-light" }; _board.Add(_boardLight);
            _overlay.Add(_board);
            _cinematic = Element("summon-cinematic", "summon-cinematic");
            _effect = new SummonSequenceGraphic(true) { name = "summon-reveal-backdrop" }; _cinematic.Add(_effect);
            _crossWord = new Label(); _crossWord.AddToClassList("summon-cross-word"); _crossWord.AddToClassList("heavy"); _cinematic.Add(_crossWord);
            _heroStage = Element("summon-hero-stage", "summon-hero-stage");
            _echo = new Image { name = "summon-echo", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _echo.AddToClassList("summon-hero-echo"); _heroStage.Add(_echo);
            _hero = new Image { name = "summon-hero", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _hero.AddToClassList("summon-hero"); _heroStage.Add(_hero); _cinematic.Add(_heroStage);
            _nameBar = Element("summon-name-bar", "summon-name-bar");
            _band = new SummonSequenceBand(); _nameBar.Add(_band);
            _rarity = new Label { name = "summon-rarity" }; _rarity.AddToClassList("summon-rarity"); _nameBar.Add(_rarity);
            _rarity.AddToClassList("heavy");
            _attribute = new Label { name = "summon-attribute" }; _attribute.AddToClassList("summon-attribute"); _nameBar.Add(_attribute);
            _name = new Label { name = "summon-name" }; _name.AddToClassList("summon-fighter-name"); _nameBar.Add(_name);
            _name.AddToClassList("heavy");
            _cinematic.Add(_nameBar);
            _foreground = new SummonSequenceGraphic(true, true) { name = "summon-reveal-light" };
            _cinematic.Add(_foreground);
            _hint = new Label("TAP TO CONTINUE"); _hint.AddToClassList("summon-continue-hint"); _cinematic.Add(_hint);
            _advance = new Button(AdvancePremium) { name = "summon-advance", text = "" }; _advance.AddToClassList("summon-advance");
            _advance.tooltip = "Continue the fighter reveal"; _cinematic.Add(_advance);
            _overlay.Add(_cinematic);
            _flash = Element("summon-flash", "summon-flash"); _overlay.Add(_flash);
            _close = new Button(Close) { name = "summon-result-close", text = "", tooltip = "Close summon results" };
            _close.AddToClassList("summon-result-close");
            // A footer-sized close target leaves the horizontal reward row free for swipe/inspection.
            _close.style.left = _close.style.right = Length.Percent(10);
            _close.style.top = Length.Percent(86); _close.style.bottom = Length.Percent(4);
            var closeLabel = new Label("TAP TO CLOSE") { name = "summon-result-close-label", pickingMode = PickingMode.Ignore };
            closeLabel.AddToClassList("summon-result-close-label"); closeLabel.AddToClassList("condensed");
            closeLabel.style.top = 0;
            _close.Add(closeLabel); _overlay.Add(_close); _close.style.display = DisplayStyle.None;
            _skip = new Button(Skip) { name = "summon-skip", text = "SKIP  »" }; _skip.AddToClassList("summon-skip"); _overlay.Add(_skip);
            root.Add(_overlay); _overlay.style.display = DisplayStyle.None;
        }

        public static bool HasPremiumReveal(FighterRarity rarity) => rarity >= FighterRarity.SSR;

        public void Play(IReadOnlyList<CharacterObject> draws, Action completed)
            => Play(SummonDrawPresentation.Classify(draws, Array.Empty<string>()), completed);

        public void Play(IReadOnlyList<SummonDrawPresentation> draws, Action completed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SummonSequenceView));
            Cancel(); Build(draws); StartPlayback(completed);
        }

        /// <summary>The common white obtain panel for committed item/artifact grants. Never grants inventory.</summary>
        public void PlayItems(DungeonRewardReceipt receipt, Action completed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SummonSequenceView));
            Cancel(); BuildItems(receipt); StartPlayback(completed);
        }

        public void PlayItems(IEnumerable<ItemAmount> items, IEnumerable<ArtifactInstance> artifacts, Action completed)
            => PlayItems(new DungeonRewardReceipt
            {
                Items = (items ?? Enumerable.Empty<ItemAmount>()).Where(i => i != null && i.Amount > 0).Select(i => i.Clone()).ToList(),
                Artifacts = (artifacts ?? Enumerable.Empty<ArtifactInstance>()).Where(a => a != null).Select(a => a.Clone()).ToList()
            }, completed);

        public void SampleItemsForPreview(DungeonRewardReceipt receipt, float elapsed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SummonSequenceView));
            Cancel(); BuildItems(receipt);
            _overlay.style.display = DisplayStyle.Flex; _overlay.BringToFront();
            _loopElapsed = Mathf.Clamp(elapsed, 0, Duration); Render(_loopElapsed);
            IsAwaitingClose = elapsed >= Duration && _cards.Count > 0; SetCloseControl();
        }

        private void StartPlayback(Action completed)
        {
            _completed = completed; IsPlaying = true;
            _lastArrival = _lastCharge = _lastImpact = -1;
            if (_draws.Count == 0) { Finish(); return; }
            _overlay.style.display = DisplayStyle.Flex; _overlay.BringToFront();
            // This full-screen sequence owns its wipe/light entrance rather than the shared panel pop.
            Render(0); _skip.Focus();
            // Start after initial layout/font preparation so a cold UI cannot eat the opening beat.
            _lastTick = Clock;
            _tick = _overlay.schedule.Execute(() =>
            {
                if (!IsPlaying) return;
                var now = Clock;
                var delta = (float)(now - _lastTick); _lastTick = now;
                AdvancePlayback(delta);
            }).Every(16);
        }

        private void AdvancePlayback(float delta)
        {
            if (!IsPlaying) return;
            _loopElapsed += Mathf.Max(0, delta);
            var enteredHold = false;
            var next = IsAwaitingRevealInput ? _elapsed : _elapsed + Mathf.Max(0, delta);
            // Clamp before rendering/cues, even if a slow frame crosses the entire cinematic.
            // Each premium in a multi-pull gets its own input gate.
            if (!IsAwaitingRevealInput)
                for (var i = _acknowledgedPremium + 1; i < _draws.Count; i++)
                {
                    if (_draws[i] == null || !HasPremiumReveal(_draws[i].FighterRarity)) continue;
                    var hold = _starts[i] + PremiumHoldTime;
                    if (next < hold) break;
                    next = hold; IsAwaitingRevealInput = true; enteredHold = true; break;
                }
            if (!RenderSafely(next, true)) return;
            if (enteredHold) _advance.Focus();
            if (next >= Duration) ShowResults();
        }

        /// <summary>Deterministic, silent editor sampling. No account or reward state is accessed.</summary>
        public void SampleForPreview(IReadOnlyList<CharacterObject> draws, float elapsed)
            => SampleForPreview(SummonDrawPresentation.Classify(draws, Array.Empty<string>()), elapsed);

        public void SampleForPreview(IReadOnlyList<SummonDrawPresentation> draws, float elapsed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SummonSequenceView));
            Cancel(); Build(draws); _overlay.style.display = DisplayStyle.Flex; _overlay.BringToFront();
            _loopElapsed = Mathf.Clamp(elapsed, 0, Duration);
            Render(Mathf.Clamp(elapsed, 0, Duration));
            IsAwaitingClose = elapsed >= Duration && _draws.Count > 0;
            SetCloseControl();
        }

        private void Build(IReadOnlyList<SummonDrawPresentation> draws)
        {
            _itemMode = false;
            _overlay.RemoveFromClassList("summon-items");
            _itemScroll.style.display = DisplayStyle.None;
            if (_grid.parent != _board)
            {
                _grid.RemoveFromHierarchy(); _board.Insert(_board.IndexOf(_boardLight), _grid);
            }
            _grid.style.flexWrap = StyleKeyword.Null;
            _grid.style.maxWidth = StyleKeyword.Null;
            _grid.style.height = StyleKeyword.Null;
            _grid.style.minHeight = StyleKeyword.Null;
            _boardWord.text = "SUMMON RESULTS";
            _close.pickingMode = PickingMode.Position;
            _draws.Clear(); _starts.Clear(); _cards.Clear(); _grid.Clear(); _activePremium = -1;
            _elapsed = _loopElapsed = 0; _acknowledgedPremium = -1; IsAwaitingRevealInput = false;
            var time = OpeningDuration;
            if (draws != null)
                foreach (var receipt in draws)
                {
                    var fighter = receipt?.Character;
                    if (fighter == null) continue;
                    _starts.Add(time); _draws.Add(fighter);
                    time += ArrivalDuration + (HasPremiumReveal(fighter.FighterRarity) ? PremiumDuration : 0);
                    _cards.Add(SummonRewardItemView.Create(receipt, _cards.Count));
                }
            Duration = time + FinalHoldDuration;
            _grid.EnableInClassList("summon-single", _draws.Count == 1);
            foreach (var card in _cards) _grid.Add(card);
        }

        private void BuildItems(DungeonRewardReceipt receipt)
        {
            Build(Array.Empty<SummonDrawPresentation>());
            _itemMode = true; _boardWord.text = "ITEMS OBTAINED";
            _overlay.AddToClassList("summon-items");
            _close.pickingMode = PickingMode.Position;
            _itemScroll.style.display = DisplayStyle.Flex; _itemScroll.Add(_grid);
            _itemScroll.scrollOffset = Vector2.zero;
            _grid.style.flexWrap = Wrap.NoWrap;
            // Freeze the receipt so inspection always shows the exact granted amounts and artifact rolls.
            var snapshot = receipt?.Clone();
            var staging = new VisualElement();
            if (snapshot != null)
                ItemRewardView.Populate(staging, snapshot.Items.Select(i => ItemCatalog.Find(i.ItemId))
                    .Where(i => i != null).GroupBy(i => i.Id).Select(g => g.First()), _overlay.parent, snapshot);
            var time = OpeningDuration;
            foreach (var tile in staging.Children().ToList())
            {
                _starts.Add(time); _draws.Add(null); time += ArrivalDuration;
                var cell = new ObtainItemRewardCell(tile, _cards.Count); _cards.Add(cell); _grid.Add(cell);
            }
            Duration = time + FinalHoldDuration;
            _grid.EnableInClassList("summon-single", _cards.Count == 1);
        }

        private void Render(float elapsed)
        {
            _elapsed = elapsed;
            var width = _overlay.resolvedStyle.width; var height = _overlay.resolvedStyle.height;
            if (float.IsNaN(width) || width <= 0) width = 1920;
            if (float.IsNaN(height) || height <= 0) height = 1080;
            var portrait = height > width;
            _overlay.EnableInClassList("summon-portrait", portrait);
            // Font and card dimensions follow the actual panel, including portrait previews.
            var unit = Mathf.Min(width / 1920f, height / 1080f);
            var cardWidth = Mathf.Min(width * .14f, ObtainTileSize * Mathf.Max(.65f, unit));
            if (portrait) cardWidth = width * .15f;
            if (_itemMode)
            {
                cardWidth = ObtainTileSize;
                var boardHeight = _board.resolvedStyle.height;
                if (float.IsNaN(boardHeight) || boardHeight <= 0) boardHeight = Mathf.Max(240, height * (portrait ? .32f : .36f));
                var rowWidth = Mathf.Max(width, (cardWidth + 24) * _cards.Count + 1);
                _grid.style.maxWidth = rowWidth; _grid.style.width = rowWidth;
                _grid.style.height = boardHeight * .96f;
            }
            else _grid.style.width = Mathf.Min(width * .96f, (cardWidth + (portrait ? 10 : 24)) * Mathf.Min(5, _draws.Count) + 1);
            foreach (var card in _cards)
            {
                card.style.width = cardWidth;
                card.style.height = _itemMode ? cardWidth + ItemRewardView.FooterHeight * cardWidth / ItemRewardView.TileSide + 12 : cardWidth + 28;
                var cardName = card.Q(className: "summon-card-name");
                if (cardName != null) cardName.style.fontSize = Mathf.Clamp(cardWidth * .13f, 10, 18);
            }
            _header.style.fontSize = Mathf.Clamp(width * .034f, 22, 56);
            _boardWord.style.fontSize = Mathf.Clamp(width * .09f, 64, 180);
            var maxNameSize = width * .5f / Mathf.Max(8, _name.text.Length * .46f);
            _name.style.fontSize = Mathf.Clamp(Mathf.Min(width * (portrait ? .06f : .038f), maxNameSize), 16, 68);
            _rarity.style.fontSize = Mathf.Clamp(width * .06f, 34, 104);
            _attribute.style.fontSize = Mathf.Clamp(width * .015f, 12, 24);
            _hint.style.fontSize = Mathf.Clamp(width * .014f, 12, 22);
            _skip.style.fontSize = Mathf.Clamp(width * .014f, 14, 24);
            _crossWord.style.fontSize = Mathf.Clamp(width * .018f, 18, 32);
            var open = Ease(elapsed / OpeningDuration);
            var visualTime = IsPlaying || IsAwaitingClose ? _loopElapsed : elapsed;
            _overlay.style.opacity = Mathf.Clamp01(elapsed / .16f);
            _board.style.scale = new Scale(Vector3.one);
            _board.style.translate = new Translate(0, (1 - open) * 32);
            _board.style.opacity = open;
            var titleIn = Ease((elapsed - .08f) / .32f);
            _header.style.opacity = titleIn;
            _header.style.translate = new Translate((1 - titleIn) * width * .08f, (1 - titleIn) * 8);
            _header.style.letterSpacing = new Length(Mathf.Lerp(portrait ? 4 : 10, portrait ? 0 : 1, titleIn));
            _counter.style.opacity = Ease((elapsed - .2f) / .25f);
            _boardWord.style.translate = new Translate(-(1 - open) * width * .12f, 0);
            _boardGraphic.TimeValue = elapsed; _boardGraphic.LoopTime = visualTime; _boardGraphic.MarkDirtyRepaint();
            _boardLight.TimeValue = elapsed; _boardLight.MarkDirtyRepaint();
            var premiumIndex = -1; var premiumTime = 0f; var shown = 0;
            for (var i = 0; i < _draws.Count; i++)
            {
                var isPremium = _draws[i] != null && HasPremiumReveal(_draws[i].FighterRarity);
                var local = elapsed - _starts[i];
                if (isPremium && local >= 0 && local < PremiumDuration)
                { premiumIndex = i; premiumTime = local; }
                var arrival = local - (isPremium ? PremiumDuration : 0);
                if (arrival >= 0) shown++;
                _cards[i].SetArrival(arrival);
                _cards[i].SetTime(visualTime);
            }
            _counter.text = shown + " / " + _draws.Count;
            _cinematic.style.display = premiumIndex < 0 ? DisplayStyle.None : DisplayStyle.Flex;
            _flash.style.opacity = 0;
            _cinematic.style.translate = new Translate(0, 0);
            _cinematic.style.scale = new Scale(Vector3.one);
            _activePremium = premiumIndex;
            if (premiumIndex < 0) return;
            SetPremium(_draws[premiumIndex]);
            var reveal = premiumTime - 1.25f;
            var slam = premiumTime - 1.35f;
            var exit = Mathf.Clamp01((premiumTime - PremiumExitTime) / (PremiumDuration - PremiumExitTime));
            _cinematic.style.opacity = Mathf.Clamp01(premiumTime / .12f) * (1 - exit);
            // Starting values: a visible screen-entry strike, then a sharper camera-push kick.
            // Unscaled timeline sampling makes the shake identical in the game and editor preview.
            var offset = ScreenKick(premiumTime - .035f, .38f, 36) + ScreenKick(premiumTime - 1.03f, .28f, 46);
            offset *= Mathf.Max(unit, .5f);
            _cinematic.style.translate = new Translate(offset.x, offset.y);
            var overscan = Mathf.Max(Mathf.Abs(offset.x) * 2 / width, Mathf.Abs(offset.y) * 2 / height);
            _cinematic.style.scale = new Scale(Vector3.one * (1 + overscan * 1.12f));
            _effect.TimeValue = premiumTime; _effect.LoopTime = visualTime; _effect.MarkDirtyRepaint();
            _foreground.TimeValue = premiumTime; _foreground.LoopTime = visualTime; _foreground.MarkDirtyRepaint();
            _crossWord.style.opacity = Mathf.Clamp01(premiumTime / .2f) * (1 - Mathf.Clamp01((premiumTime - .95f) / .18f));
            _crossWord.style.letterSpacing = new Length(Mathf.Lerp(9, 2, Ease(premiumTime / .55f)));
            _crossWord.style.translate = new Translate(0, (1 - Ease(premiumTime / .3f)) * 12);
            _heroStage.style.opacity = slam < 0 ? Ease(reveal / .12f) : Mathf.Lerp(.35f, 1, Ease(slam / .26f));
            _hero.tintColor = slam < 0 ? new Color(.02f, .015f, .025f) : Color.white;
            var heroIn = Ease(Mathf.Max(0, slam) / .65f);
            var heroScale = slam < 0 ? Mathf.Lerp(1.12f, 1.25f, Ease(reveal / .1f)) : Mathf.Lerp(1.25f, 1, heroIn);
            _heroStage.style.scale = new Scale(Vector3.one * heroScale);
            var shock = slam >= 0 && slam < .24f ? Mathf.Sin(slam * 85) * Mathf.Pow(1 - slam / .24f, 2) * 7 * Mathf.Max(unit, .5f) : 0;
            _heroStage.style.translate = new Translate(shock, (1 - heroIn) * 22);
            var backgroundScale = slam < 0 ? Mathf.Lerp(1, 1.11f, Ease(premiumTime / 1.2f)) : Mathf.Lerp(1, 1.18f, Ease(slam / .85f));
            _effect.style.scale = new Scale(Vector3.one * backgroundScale);
            _effect.style.translate = new Translate(-shock * .45f, 0);
            _echo.style.opacity = .1f * heroIn;
            var barIn = Ease((premiumTime - 1.45f) / .32f);
            _nameBar.style.opacity = barIn;
            _nameBar.style.translate = new Translate(-(1 - barIn) * width, 0);
            var rarityIn = Ease((premiumTime - 1.53f) / .34f);
            _rarity.style.opacity = rarityIn;
            _rarity.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.32f, 1, rarityIn));
            _band.TimeValue = visualTime; _band.MarkDirtyRepaint();
            _hint.style.opacity = Mathf.Clamp01((premiumTime - 2.25f) / .35f) * (.8f + .2f * Mathf.Sin(visualTime * 2.4f));
            _advance.SetEnabled(IsAwaitingRevealInput);
            // A fast opening impulse and a separate artwork strike, with no repeated flashes on hold.
            var impulse = premiumTime < .07f ? .16f * (1 - premiumTime / .07f) : 0;
            var strike = slam >= 0 && slam < .09f ? Mathf.Pow(1 - slam / .09f, 2) * .22f : 0;
            _flash.style.opacity = Mathf.Max(impulse, strike, exit * .68f);
        }

        private void SetPremium(CharacterObject fighter)
        {
            var art = fighter.FighterFull != null ? fighter.FighterFull : fighter.FighterPic != null ? fighter.FighterPic : fighter.FighterIcon;
            if (_hero.sprite != art) { _hero.sprite = art; _echo.sprite = art; }
            _name.text = fighter.FighterName;
            _rarity.text = fighter.FighterRarity.ToString();
            _crossWord.text = fighter.FighterRarity + "  /  FIGHTER SUMMON";
            _attribute.text = fighter.FighterAttribute.ToString().ToUpperInvariant();
            _effect.Rarity = fighter.FighterRarity;
            _foreground.Rarity = fighter.FighterRarity;
            var accent = SummonAttributeTheme.ColorFor(fighter.FighterAttribute);
            _effect.Accent = accent;
            _foreground.Accent = accent;
            _band.Accent = accent; _band.MarkDirtyRepaint();
            _echo.tintColor = new Color(accent.r, accent.g, accent.b, 1);
            _attribute.style.backgroundColor = Color.Lerp(accent, Color.black, .65f);
            foreach (var theme in new[] { "red", "yellow", "green", "blue", "purple" })
                _overlay.EnableInClassList("theme-" + theme, theme == SummonAttributeTheme.NameFor(fighter.FighterAttribute));
        }

        private void PlayCues(float elapsed)
        {
            for (var i = 0; i < _draws.Count; i++)
            {
                var rarity = _draws[i] != null ? _draws[i].FighterRarity : FighterRarity.R;
                var premium = _draws[i] != null && HasPremiumReveal(rarity);
                var local = elapsed - _starts[i];
                if (premium && local >= 0 && i > _lastCharge) { _audio.PlayCharge(rarity); _lastCharge = i; }
                if (premium && local >= 1.35f && i > _lastImpact) { _audio.PlayImpact(rarity); _lastImpact = i; }
                if (local >= (premium ? PremiumDuration : 0) && i > _lastArrival)
                { _audio.PlayArrival(rarity, i); _lastArrival = i; }
            }
        }

        private bool RenderSafely(float elapsed, bool playCues)
        {
            try
            {
                Render(elapsed);
                if (playCues) PlayCues(elapsed);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                // The receipt is already saved. A presentation failure must still wait for dismissal.
                if (IsPlaying || IsAwaitingClose)
                {
                    try { ShowResults(); }
                    catch (Exception fallbackError)
                    {
                        Debug.LogException(fallbackError);
                        _tick?.Pause(); _tick = null;
                        IsPlaying = false; IsAwaitingRevealInput = false; IsAwaitingClose = true;
                        _cinematic.style.display = DisplayStyle.None; _flash.style.opacity = 0;
                        _overlay.style.opacity = 1; _board.style.opacity = 1;
                        _board.style.translate = new Translate(0, 0);
                        SetCloseControl(); _close.Focus();
                    }
                }
                else _overlay.style.display = DisplayStyle.None;
                return false;
            }
        }

        private void AdvancePremium()
        {
            if (!IsPlaying || !IsAwaitingRevealInput || _activePremium < 0) return;
            _acknowledgedPremium = _activePremium;
            IsAwaitingRevealInput = false;
            _lastTick = Clock;
            // Preserve the light-out/return beat after input instead of cutting straight to the grid.
            RenderSafely(_starts[_activePremium] + PremiumExitTime, false);
        }

        private void OnKey(KeyDownEvent evt)
        {
            if (_itemMode && evt.target is Button itemButton && itemButton != _close && itemButton != _skip && evt.keyCode != KeyCode.Escape) return;
            if (IsAwaitingClose && (evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return))
            { Close(); evt.StopPropagation(); return; }
            if (!IsPlaying) return;
            if (evt.target == _skip && evt.keyCode != KeyCode.Escape) return;
            if (evt.keyCode == KeyCode.Escape) { Skip(); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return)
            { AdvancePremium(); evt.StopPropagation(); }
        }

        public void Skip() { if (IsPlaying) ShowResults(); }
        private void ShowResults()
        {
            _tick?.Pause(); _tick = null; _audio.Stop();
            Render(Duration);
            IsPlaying = false; IsAwaitingClose = true; IsAwaitingRevealInput = false;
            SetCloseControl(); _close.Focus();
            var start = Clock;
            _tick = _overlay.schedule.Execute(() =>
            {
                var time = Duration + (float)(Clock - start);
                _loopElapsed = time;
                foreach (var card in _cards) card.SetTime(time);
                _boardGraphic.LoopTime = time; _boardGraphic.MarkDirtyRepaint();
            }).Every(33);
        }
        private void SetCloseControl()
        {
            _close.style.display = IsAwaitingClose ? DisplayStyle.Flex : DisplayStyle.None;
            _skip.style.display = IsAwaitingClose ? DisplayStyle.None : DisplayStyle.Flex;
        }
        public void Close() { if (IsAwaitingClose) Finish(); }
        private void Finish()
        {
            if (!IsPlaying && !IsAwaitingClose) return;
            _tick?.Pause(); _tick = null; IsPlaying = false;
            IsAwaitingClose = false;
            IsAwaitingRevealInput = false;
            _audio.Stop();
            var completed = _completed; _completed = null;
            ScreenMotion.Hide(_overlay, completed);
        }
        private void Cancel()
        {
            if (_itemMode && _overlay.parent != null) ItemRewardView.CloseTooltip(_overlay.parent);
            ScreenMotion.Cancel(_overlay);
            _tick?.Pause(); _tick = null; IsPlaying = false; _completed = null;
            IsAwaitingClose = false; IsAwaitingRevealInput = false; SetCloseControl();
            _audio.Stop();
            _overlay.style.display = DisplayStyle.None;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; Cancel(); _audio.Dispose(); _overlay.RemoveFromHierarchy();
        }
        private static float Ease(float value) => 1 - Mathf.Pow(1 - Mathf.Clamp01(value), 3);
        private static Vector2 ScreenKick(float age, float duration, float strength)
        {
            if (age < 0 || age >= duration) return Vector2.zero;
            var envelope = Mathf.Clamp01(age / .018f) * Mathf.Pow(1 - age / duration, 1.3f);
            return new Vector2((Mathf.Cos(age * 97f) * .75f + Mathf.Cos(age * 173f) * .25f) * strength,
                -Mathf.Sin(age * 123f + .8f) * strength * .55f) * envelope;
        }
        private static double Clock => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
        private static VisualElement Element(string name, string className)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.AddToClassList(className); return element;
        }
    }

}
