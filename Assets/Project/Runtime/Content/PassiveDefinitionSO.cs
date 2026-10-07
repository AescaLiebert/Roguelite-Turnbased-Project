using FightingAllstar.Core.Content;
using UnityEngine;

[CreateAssetMenu(fileName = "New Passive Definition", menuName = "Fighting Allstar/Passive Definition")]
public sealed class PassiveDefinitionSO : ScriptableObject
{
    public System.Collections.Generic.List<FightingAllstar.Presentation.Combat.StatusVisualData> statusVisuals = new System.Collections.Generic.List<FightingAllstar.Presentation.Combat.StatusVisualData>();
    private void OnEnable() { foreach (var visual in statusVisuals) FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual); }
    [SerializeField] private PassiveDefinition definition = new PassiveDefinition();

    public string Id => definition == null ? string.Empty : definition.Id;

    public PassiveDefinition CreateDefinition() => definition == null ? null : definition.Clone();

    public void SetDefinition(PassiveDefinition value)
    {
        definition = value == null ? new PassiveDefinition() : value.Clone();
    }
}
