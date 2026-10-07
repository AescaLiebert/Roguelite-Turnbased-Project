using UnityEngine;
using UnityEngine.UIElements;
using FightingAllstar.Core.Combat;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class CoreBattleSceneController
    {
        [SerializeField, Tooltip("Starting value: 0.45 seconds. Tune against accidental opens versus intentional holds.")]
        private float characterInspectHoldSeconds = .45f;
        private BattleCharacterInspector _inspector;
        private string _inspectHeldId;
        private Vector2 _inspectStart;
        private float _inspectStartedAt;
        private void HandleInspectorInput()
        {
            if (_inspector != null) return;
            if (!CanPlan || _allyPicker != null) { _inspectHeldId = null; return; }
            bool touch = Input.touchCount > 0;
            var position = touch ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            bool down = touch ? Input.GetTouch(0).phase == TouchPhase.Began : Input.GetMouseButtonDown(0);
            bool held = touch ? Input.GetTouch(0).phase != TouchPhase.Ended && Input.GetTouch(0).phase != TouchPhase.Canceled : Input.GetMouseButton(0);
            if (down)
            {
                _inspectHeldId = null;
                if (IsOverInspectorBlockingUi(position) || Camera.main == null) return;
                _inspectHeldId = BattleInspectorPicking.Pick(Camera.main, position, _fighterViews);
                _inspectStart = position; _inspectStartedAt = Time.unscaledTime;
            }
            if (!held || Input.touchCount > 1 || Vector2.Distance(position, _inspectStart) > 24f) { _inspectHeldId = null; return; }
            if (_inspectHeldId == null || Time.unscaledTime - _inspectStartedAt < characterInspectHoldSeconds) return;
            var id = _inspectHeldId; _inspectHeldId = null;
            HideTooltip();
            _inspector = new GameObject("Battle Character Inspector").AddComponent<BattleCharacterInspector>();
            _inspector.Open(_snapshot.Clone(), id, key => {
                    var fighter = _snapshot.Player.Fighters.Find(f => f.Definition.Id == key) ?? _snapshot.Opponent.Fighters.Find(f => f.Definition.Id == key);
                    return ResolveCharacterIcon(fighter, LoadCharacter(key));
                }, key => _fighterBillboards.TryGetValue(key, out var hud) ? hud : null, () => _inspector = null,
                (fighter, slot) => {
                    var character = LoadCharacter(fighter.Definition.Id);
                    var art = slot == 0 ? character?.Ultimate?.icon : slot == 1 ? character?.Skill1?.cardIcon : character?.Skill2?.cardIcon;
                    return art != null ? art : ResolveCharacterIcon(fighter, character);
                }, (fighter, slot) => {
                    var character = LoadCharacter(fighter.Definition.Id);
                    return slot == 0 ? character?.Ultimate?.ultimateName : slot == 1 ? character?.Skill1?.cardName : character?.Skill2?.cardName;
                }, (fighter, slot, rank) => BuildInspectorCardTooltip(fighter, slot, rank),
                key => LoadCharacter(key));
        }

        private bool IsOverInspectorBlockingUi(Vector2 screen)
        {
            var root = battleDocument?.rootVisualElement;
            if (root?.panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            // Only real controls block inspection; a fixed screen-height cutoff hides allied models.
            var picked = root.panel.Pick(point);
            for (var element = picked; element != null; element = element.parent)
                if (element == _handRow || element == _actionRow || element is Button) return true;
            return false;
        }
    }
}
