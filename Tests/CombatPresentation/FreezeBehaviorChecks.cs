using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void FreezeBehaviorChecks()
    {
        var state=ElementFixture(ElementalRuleSet.CreateDefault());
        var target=state.Opponent.Fighters[0];
        var source=state.Player.Fighters[0];
        var freeze=GenshinStatusRecipes.CreateOutputs().Single(r=>r.Id==StatusSystem.FreezeRecipeId);
        var buff=new StatusRecipeDefinition { Id="fixture.buff", Polarity=StatusPolarity.Buff };
        var debuff=new StatusRecipeDefinition { Id="fixture.debuff", Polarity=StatusPolarity.Debuff };
        var neutral=new StatusRecipeDefinition { Id="fixture.neutral", Polarity=StatusPolarity.Neutral };
        StatusSystem.Apply(target,source.Id,source.Side,buff,"buff","fixture",1);
        StatusSystem.Apply(target,source.Id,source.Side,debuff,"debuff","fixture",2);
        StatusSystem.Apply(target,source.Id,source.Side,neutral,"neutral","fixture",3);
        target.Statuses.Instances.First(s=>s.RecipeId==buff.Id).ShieldRemaining=75; target.Shield=100;
        Check(StatusSystem.Apply(target,source.Id,source.Side,freeze,"freeze","fixture",4).Accepted,"Freeze was rejected.");
        Check(target.Statuses.Instances.Count==2 && target.Statuses.Instances.All(s=>s.RecipeId==freeze.Id || s.RecipeId==neutral.Id),
            "Freeze did not purge all buffs/debuffs while preserving neutral effects.");
        Check(target.Shield==25,"Freeze left shield belonging to a purged buff.");
        Check(target.Statuses.Instances.All(s=>s.RecipeId!="status.reaction.freeze-recovery"),"Freeze ICD leaked into StatusContainer.");
        Check(StatusSystem.IsFreezeRecoveryActive(target) && StatusSystem.IsFreezeRecoveryActive(target.Clone()),"Hidden ICD did not survive cloning.");
        var restored=SnapshotJson.Deserialize<BattleState>(SnapshotJson.Serialize(state));
        Check(StatusSystem.IsFreezeRecoveryActive(restored.Opponent.Fighters[0]),"Hidden ICD did not survive save serialization.");
        foreach(var recipe in new[] { buff,debuff,freeze })
            Check(!StatusSystem.Apply(target,source.Id,source.Side,recipe,"blocked","fixture",5).Accepted,"Frozen target accepted "+recipe.Id);
        Check(StatusSystem.IsCardUseBlocked(target,CardCategory.Attack,1,false) && StatusSystem.IsCardUseBlocked(target,CardCategory.Buff,1,false) &&
            StatusSystem.IsCardUseBlocked(target,CardCategory.Attack,3,true),"Freeze did not disable all card categories.");
        var projected=BattleProjectionBuilder.Build(state,TeamSide.Player,"freeze");
        Check(projected.OpponentTeam.Fighters.First(f=>f.FighterId==target.Id).FreezeImmune,"Public forecast omitted hidden Freeze lockout.");
        var wet=StandardEffectDatabase.CreateStatusRecipes().First(r=>r.Id=="status.debuff.wet");
        var preparation=new ElementalForecastAction { TargetId=target.Id,Effect=new EffectDefinition { Kind=EffectKind.ApplyStatus,
            Target=EffectTargetScope.SelectedEnemy,StatusRecipe=wet,StatusApplyChanceBp=10000 } };
        var coldAttack=new ElementalForecastAction { TargetId=target.Id,Effect=ElementAttack(ElementId.Cryo) };
        var blockedSetup=ElementalForecast.Build(projected,state.ElementalRules,new[] { preparation,coldAttack });
        Check(blockedSetup.Lines.Count==0,"Forecast allowed elemental setup on a frozen target.");
        var shatterSetup=ElementalForecast.Build(projected,state.ElementalRules,new[] {
            new ElementalForecastAction { TargetId=target.Id,Effect=ElementAttack(ElementId.None) },preparation,coldAttack });
        Check(shatterSetup.Lines.Any(l=>l.Reaction==ElementalReactionId.Freeze && l.OutputBlocked),
            "Forecast did not allow setup after a physical shatter, with hidden Freeze ICD still active.");
        var forecastState=ElementFixture(ElementalRuleSet.CreateDefault());
        ElementFuel(forecastState.Opponent.Fighters[0],wet.Id,"freeze-forecast-wet");
        var forecastFreeze=ElementalForecast.Build(BattleProjectionBuilder.Build(forecastState,TeamSide.Player,"freeze-sequence"),
            forecastState.ElementalRules,new[] { coldAttack,preparation,coldAttack });
        Check(forecastFreeze.Lines.Count(l=>l.Reaction==ElementalReactionId.Freeze)==1,
            "Forecast failed to purge/block setup during a newly predicted Freeze.");
        Check(StatusSystem.BreakFreezeOnAttack(target) && !StatusSystem.IsFrozen(target),"Attack did not break Freeze.");
        Check(!StatusSystem.BreakFreezeOnAttack(target),"Repeated attack removed Freeze twice.");
        Check(StatusSystem.Apply(target,source.Id,source.Side,buff,"thawed-buff","fixture",6).Accepted,"Thawed target still blocks buffs.");
        Check(!StatusSystem.Apply(target,source.Id,source.Side,freeze,"early","fixture",7).Accepted,"Shatter bypassed Freeze ICD.");
        StatusSystem.AdvanceOwnerTurn(target);
        Check(StatusSystem.IsFreezeRecoveryActive(target),"Freeze ICD expired a turn early.");
        StatusSystem.AdvanceOwnerTurn(target);
        Check(StatusSystem.Apply(target,source.Id,source.Side,freeze,"again","fixture",8).Accepted,"Freeze ICD never expired.");
        StatusSystem.AdvanceOwnerTurn(target);
        Check(!StatusSystem.IsFrozen(target),"Freeze did not expire naturally.");

        var contactState=ElementFixture(ElementalRuleSet.CreateDefault());
        var victim=contactState.Opponent.Fighters[0];
        StatusSystem.Apply(victim,source.Id,source.Side,freeze,"contact-freeze","fixture",1);
        var effect=ElementAttack(ElementId.None);
        effect.Sequence.Add(new CardEffectStep { Effect=new EffectDefinition { Kind=EffectKind.ApplyStatus,StatusRecipe=debuff,StatusDurationOverride=2 } });
        var next=PlayKind(contactState,CardCategory.AttackDebuff,EffectTargetScope.SelectedEnemy,effect);
        Check(!StatusSystem.IsFrozen(next.Opponent.Fighters[0]),"Real card damage did not shatter Freeze.");
        Check(next.Opponent.Fighters[0].Statuses.Instances.Any(s=>s.RecipeId==debuff.Id),"Follow-up debuff remained blocked after shatter.");
        var hit=next.Events.First(e=>e.Kind==BattleEventKind.DamageApplied && e.TargetId==victim.Id);
        Check(hit.StatusesAfter!=null && hit.StatusesAfter.All(s=>s.RecipeId!=freeze.Id),"Shatter is missing from hit playback snapshot.");
        var display=contactState.Clone(); BattlePlaybackState.Apply(display,hit);
        Check(!StatusSystem.IsFrozen(display.Opponent.Fighters[0]),"Playback retained the frozen shell after impact.");

        var immune=ElementFixture(); var immuneTarget=immune.Opponent.Fighters[0];
        var immunity=new StatusRecipeDefinition { Id="fixture.immunity",Polarity=StatusPolarity.Buff,DebuffImmunity=true };
        StatusSystem.Apply(immuneTarget,source.Id,source.Side,immunity,"immune","fixture",1);
        Check(!StatusSystem.Apply(immuneTarget,source.Id,source.Side,freeze,"rejected","fixture",2).Accepted &&
            immuneTarget.Statuses.Instances.Any(s=>s.RecipeId==immunity.Id) && !StatusSystem.IsFreezeRecoveryActive(immuneTarget),
            "Rejected Freeze purged immunity or started its ICD.");

        var periodic=ElementFixture(); var periodicTarget=periodic.Opponent.Fighters[0];
        StatusSystem.Apply(periodicTarget,source.Id,source.Side,freeze,"dot-freeze","fixture",1,duration:3);
        var neutralDot=new StatusRecipeDefinition { Id="fixture.neutral-dot",Polarity=StatusPolarity.Neutral,
            Behavior=StatusBehavior.DamageOverTime,DefaultDuration=4,PeriodicDamage=new PeriodicDamageDefinition {
                Timing=StatusTickTiming.TargetTurnStart,Scaling=StatusSnapshotScaling.Fixed,FixedAmount=20,CoefficientBp=10000 } };
        StatusSystem.Apply(periodicTarget,source.Id,source.Side,neutralDot,"neutral-dot","fixture",2);
        Check(BattleEngine.TryResolvePlan(periodic,new TurnPlan { RequestId="frozen-dot",ExpectedRevision=periodic.Revision },out var afterTick,out var tickError),tickError);
        Check(afterTick.Events.Any(e=>e.Kind==BattleEventKind.DamageApplied && e.StatusRecipeId==neutralDot.Id) &&
            StatusSystem.IsFrozen(afterTick.Opponent.Fighters[0]),"Periodic damage incorrectly shattered Freeze.");

        var selfFreeze=ElementFixture().Player.Fighters[0];
        StatusSystem.Apply(selfFreeze,source.Id,source.Side,freeze,"self-freeze","fixture",1,skipNextDurationClock:true);
        StatusSystem.AdvanceOwnerTurn(selfFreeze);
        Check(StatusSystem.IsFrozen(selfFreeze),"Freeze consumed its skipped applying turn.");
        StatusSystem.AdvanceOwnerTurn(selfFreeze);
        Check(!StatusSystem.IsFrozen(selfFreeze) && StatusSystem.IsFreezeRecoveryActive(selfFreeze),"Skipped-turn Freeze lost its recovery turn.");
        StatusSystem.AdvanceOwnerTurn(selfFreeze);
        Check(!StatusSystem.IsFreezeRecoveryActive(selfFreeze),"Skipped-turn Freeze ICD never expired.");

        var healthState=ElementFixture(ElementalRuleSet.CreateDefault()); var healthTarget=healthState.Opponent.Fighters[0];
        var healthBuff=new StatusRecipeDefinition { Id="fixture.freeze-health-buff",Polarity=StatusPolarity.Buff,
            Modifiers=new System.Collections.Generic.List<StatModifierDefinition> { new StatModifierDefinition {
                Target=ModifierTarget.Stat,Stat=StatId.MaxHealth,Operation=ModifierOperation.Flat,Amount=50000 } } };
        StatusSystem.Apply(healthTarget,source.Id,source.Side,healthBuff,"health-buff","fixture",1);
        healthTarget.Health=StatusSystem.GetEffectiveStats(healthTarget).MaxHealth;
        var previousMaximum=healthTarget.Health;
        ElementFuel(healthTarget,wet.Id,"health-freeze-wet");
        var healthNext=PlayKind(healthState,CardCategory.Attack,EffectTargetScope.SelectedEnemy,ElementAttack(ElementId.Cryo));
        var healthHit=healthNext.Events.Last(e=>e.Kind==BattleEventKind.DamageApplied && e.TargetId==healthTarget.Id);
        var expectedHealth=(int)((long)healthHit.HealthAfter*healthTarget.Stats.MaxHealth/previousMaximum);
        Check(StatusSystem.IsFrozen(healthNext.Opponent.Fighters[0]) && healthNext.Opponent.Fighters[0].Health==expectedHealth,
            "Freeze dispelling max-HP buffs did not preserve the project's health-ratio rule.");
    }
}
