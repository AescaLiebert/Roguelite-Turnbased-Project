using FightingAllstar.Core.Content;
using UnityEngine;

[CreateAssetMenu(fileName = "New Passive Definition", menuName = "Fighting Allstar/Passive Definition")]
public sealed class PassiveDefinitionSO : ScriptableObject
{
    public System.Collections.Generic.List<FightingAllstar.Presentation.Combat.StatusVisualData> statusVisuals = new System.Collections.Generic.List<FightingAllstar.Presentation.Combat.StatusVisualData>();
    private void OnEnable() { foreach (var visual in statusVisuals) FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual); }
    [Header("Source Draft")]
    [SerializeField, TextArea] private string sourceDescription;
    [SerializeField] private string sourceType;
    [SerializeField] private string sourceRestriction;
    [SerializeField] private PassiveDefinition definition = new PassiveDefinition();

    public string Id => definition == null ? string.Empty : definition.Id;
    public string SourceDescription => sourceDescription;
    public string SourceType => sourceType;
    public string SourceRestriction => sourceRestriction;

    public PassiveDefinition CreateDefinition() => definition == null ? null : definition.Clone();

    public void SetDefinition(PassiveDefinition value)
    {
        definition = value == null ? new PassiveDefinition() : value.Clone();
    }

    public void SetSourceDraft(string description, string type, string restriction)
    {
        sourceDescription = description ?? string.Empty;
        sourceType = type ?? string.Empty;
        sourceRestriction = restriction ?? string.Empty;
    }
}
