using FightingAllstar.Core.Content;
using Newtonsoft.Json;

namespace FightingAllstar.Core.Combat
{
    public sealed partial class BattleState
    {
        [field: System.NonSerialized]
        [JsonIgnore]
        public DeterministicRandom ActiveRandom { get; set; }
    }
}
