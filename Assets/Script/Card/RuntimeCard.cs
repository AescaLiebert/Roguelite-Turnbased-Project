using System;

[Serializable]
public class RuntimeCard
{
    public Guid instanceId;
    public Unit owner;
    public SkillCardSO cardData;
    public int rank; // 1, 2, 3

    public RuntimeCard(Unit owner, SkillCardSO data, int rank = 1)
    {
        this.instanceId = Guid.NewGuid();
        this.owner = owner;
        this.cardData = data;
        this.rank = rank;
    }
}
