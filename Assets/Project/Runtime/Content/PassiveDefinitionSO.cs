using FightingAllstar.Core.Content;
using UnityEngine;

[CreateAssetMenu(fileName = "New Passive Definition", menuName = "Fighting Allstar/Passive Definition")]
public sealed class PassiveDefinitionSO : ScriptableObject
{
    public System.Collections.Generic.List<FightingAllstar.Presentation.Combat.StatusVisualData> statusVisuals = new System.Collections.Generic.List<FightingAllstar.Presentation.Combat.StatusVisualData>();
    private void OnEnable() { foreach (var visual in statusVisuals) FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual); }
    [SerializeField] private PassiveDefinition definition = new PassiveDefinition();
    [SerializeField, HideInInspector] private int studioSchema;
    [SerializeField, HideInInspector] private string studioDefinitionJson;
    public bool StudioOwned => studioSchema != 0;

    public string Id => CreateDefinition()?.Id ?? string.Empty;

    public PassiveDefinition CreateDefinition()
    {
        if (!StudioOwned) return definition?.Clone();
        if (studioSchema != 1) throw new System.InvalidOperationException("Unsupported Studio passive schema: " + studioSchema);
        return SnapshotJson.Deserialize<PassiveDefinition>(studioDefinitionJson) ?? throw new System.InvalidOperationException("Studio passive payload is missing.");
    }
    public void SetStudioDefinition(PassiveDefinition value)
    {
        if (value == null) throw new System.ArgumentNullException(nameof(value));
        studioDefinitionJson = SnapshotJson.Serialize(value); studioSchema = 1;
        definition = new PassiveDefinition { Id = value.Id }; // JSON owns recursive data; Unity must not traverse it.
    }

    public void SetDefinition(PassiveDefinition value)
    {
        if (StudioOwned) throw new System.InvalidOperationException("This passive is Studio-owned. Publish through Character Creator Studio; legacy generators cannot replace it.");
        definition = value == null ? new PassiveDefinition() : value.Clone();
    }
}
