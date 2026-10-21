using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Studio;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class CoreBattleSceneController
    {
        private bool _battleLab;
        private BattlePlaybackDriver _profilePlayback;
        public bool LabProfilePlaybackActive => _profilePlayback?.LiveActive == true;
        public int LabProfileEffectCount => _profilePlayback?.EffectCount ?? 0;
        public float LabProfileTime => _profilePlayback?.CurrentTime ?? 0;
        public int LabProfileAudioCues => _profilePlayback?.AudioCuesPlayed ?? 0;
        private void InitializeProfilePlayback()
        {
            if (!_battleLab) return;
            var profiles = _fighterViews.Keys.ToDictionary(id=>id,id=>BattleLabContext.VisualFor(id)?.Profile);
            if (!profiles.Values.Any(p=>p!=null)) return;
            var sound=gameObject.AddComponent<AudioSource>(); sound.playOnAwake=false; sound.spatialBlend=0;
            var effects=new GameObject("Lab profile effects"); effects.transform.SetParent(transform,false);
            _profilePlayback=new BattlePlaybackDriver(_fighterViews,profiles,Camera.main,sound,effects.transform,false);
            _stage.ProfilePlayback=_profilePlayback;
        }
        private void LateUpdate() { _profilePlayback?.TickLive(); }
        private bool _labPreviousBackground;
        private void EnableLabBackgroundSimulation()
        { _labPreviousBackground = Application.runInBackground; Application.runInBackground = true; }
        private void RestoreLabBackgroundSimulation()
        { if (_battleLab) Application.runInBackground = _labPreviousBackground; }
        private readonly List<BattleEvent> _labPresented = new List<BattleEvent>();
        public bool IsBattleLab => _battleLab;
        public BattleState LabSnapshot => _battleLab ? _session?.GetSnapshot() : null;
        public BattleState LabDisplaySnapshot => _battleLab ? _displayState?.Clone() : null;
        public bool LabCanAct => _battleLab && CanPlan;
        public IReadOnlyList<BattleEvent> LabPresentedFacts => _labPresented.Select(e => e.Clone()).ToList();
        public int LabFighterViewCount => _battleLab ? _fighterViews.Count : 0;
        public int LabActiveFloatingText => _battleLab ? _fctPool.ActiveCount : 0;
        public bool LabInspectorOpen => _battleLab && _inspector != null;
        public bool LabPlayCard(string cardId, string targetId, out string error)
        {
            error = null;
            if (!LabCanAct) { error = "Wait for planning, or close the character inspector."; return false; }
            if (_draft?.Actions.Count > 0) { error = "Commit or reset the existing Game View plan first."; return false; }
            var draft = new PlanDraft(_snapshot);
            if (!draft.QueuePlay(cardId, targetId, out error)) return false;
            _draft = draft; _commitPending = true; CommitDraft(); return true;
        }
        public bool LabPassTurn(out string error)
        {
            error = null;
            if (!LabCanAct || _draft?.Actions.Count > 0) { error = "Wait for planning and commit/reset any queued cards first."; return false; }
            _draft = new PlanDraft(_snapshot); _commitPending = true; CommitDraft(); return true;
        }
        public bool LabInspect(string fighterId)
        {
            if (!LabCanAct || (_snapshot.Player.FindFighter(fighterId) ?? _snapshot.Opponent.FindFighter(fighterId)) == null) return false;
            OpenFighterInspector(fighterId); return true;
        }
        private void RecordLabPresented(BattleEvent item)
        { if (_battleLab) { _labPresented.Add(item.Clone()); if (_labPresented.Count > 512) _labPresented.RemoveAt(0); } }
        private void BindLabControls()
        {
            var root = battleDocument?.rootVisualElement; if (root == null) return;
            var panel = new VisualElement { name = "battle-lab-controls" };
            panel.style.position = Position.Absolute; panel.style.bottom = 12; panel.style.left = 12;
            panel.style.backgroundColor = new Color(.035f, .065f, .11f, .96f); panel.style.paddingLeft = panel.style.paddingRight = 12;
            panel.style.paddingTop = panel.style.paddingBottom = 8; panel.style.color = Color.white;
            panel.Add(new Label(BattleLabContext.Mode == BattleLabMode.RealBattle ? "BATTLE LAB · REAL BATTLE · persistent combat state + enemy AI" : "BATTLE LAB · TRAINING · 1v1 · no defeat · free ranks/ultimate · idle target"));
            panel.Add(new Label("Seed " + BattleLabContext.Seed + " · disposable session · no rewards or scenario files"));
            var status = new Label("Opening battle…"); panel.Add(status);
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row;
            actions.Add(new Button(BattleLabContext.Restart) { text = "RESTART SAME SETUP" });
            actions.Add(new Button(BattleLabContext.Exit) { text = "EXIT LAB" }); panel.Add(actions); root.Add(panel);
            var exit = root.Q<Button>("surrender-button"); if (exit != null) exit.text = "Exit lab";
            var result = root.Q<Button>("result-continue"); if (result != null) result.text = "Exit lab";
            panel.schedule.Execute(() => {
                panel.BringToFront();
                var hit = _labPresented.LastOrDefault(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount > 0);
                var periodic = _labPresented.LastOrDefault(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == FightingAllstar.Core.Content.ElementalDamageOrigin.DamageOverTime);
                status.text = "Turn " + (_snapshot?.TurnNumber ?? 0) + " · " + PresentationPhase + (hit == null ? "" : " · last contact " + hit.HitIndex + "/" + hit.HitCount + " · " + hit.Amount)
                    + (periodic == null ? "" : " · last DOT " + periodic.Amount);
            }).Every(200);
        }
    }
}

