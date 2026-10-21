using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Optional observer record captured by the production dispatcher without RNG draws or battle events.</summary>
    [Serializable]
    public sealed class PassiveRuleTrace
    {
        public long FactSequence;
        public string Trigger;
        public string OwnerId;
        public string RuleId;
        public string RootActionId;
        public string ActionNodeId;
        public bool ConditionsPassed;
        public bool Fired;
        public string Reason;
        public int FirstEventIndex;
        public int EventCount;
        public string Outcome;
        public string ActorId;
        public string EventTargetId;
        public string OperationTargetId;
        public string Branch;
        public string ResourceKey;
        public int ResourceBefore;
        public int ResourceAfter;
        public List<string> SelectedTargets = new List<string>();
        public Dictionary<string, int> Values = new Dictionary<string, int>();
        public List<PassiveConditionTrace> Conditions = new List<PassiveConditionTrace>();
        public PassiveRuleTrace Clone()
        {
            var copy = (PassiveRuleTrace)MemberwiseClone();
            copy.Conditions = Conditions?.ConvertAll(c => c.Clone()) ?? new List<PassiveConditionTrace>();
            copy.SelectedTargets = new List<string>(SelectedTargets ?? new List<string>());
            copy.Values = new Dictionary<string, int>(Values ?? new Dictionary<string, int>());
            return copy;
        }
    }

    [Serializable]
    public sealed class PassiveConditionTrace
    {
        public string NodeId;
        public string Logic;
        public string Kind;
        public bool Passed;
        public bool HasObservedValue;
        public int ObservedValue;
        public int Threshold;
        public int EvaluatedChildren;
        public int DeclaredChildren;
        public string Explanation;
        public PassiveConditionTrace Clone() => (PassiveConditionTrace)MemberwiseClone();
    }
}
