using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void FreezeBehaviorChecks()
    {
        foreach (var hits in new[] { 1, 3, 10 })
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
        {
            var multi=ElementFixture(side:side);var freezeVictim=multi.OtherTeam(side).Fighters[0];
            freezeVictim.ElementStatuses.Apply(ElementId.Hydro,"setup","freeze-hydro");
            var attack=ElementAttack(ElementId.Cryo,hits);
            attack.Sequence.Add(new CardEffectStep { Timing=CardEffectTiming.AfterAction,Effect=new EffectDefinition {
                Kind=EffectKind.ApplyStatus,StatusRecipe=new StatusRecipeDefinition { Id="freeze.before-settle",Polarity=StatusPolarity.Debuff } } });
            var frozen=PlayKind(multi,CardCategory.AttackDebuff,EffectTargetScope.SelectedEnemy,attack);
            var events=frozen.Events.Skip(multi.Events.Count).ToList();
            var freezeIndex=events.FindIndex(e=>e.Kind==BattleEventKind.StatusApplied && e.StatusRecipeId==StatusSystem.FreezeRecipeId);
            Check(freezeIndex>events.FindLastIndex(e=>e.Kind==BattleEventKind.DamageApplied) &&
                freezeIndex>events.FindIndex(e=>e.Kind==BattleEventKind.ActionCompleted),"Freeze applied before the triggering action finished.");
            Check(StatusSystem.IsFrozen(frozen.OtherTeam(side).Fighters[0]),"Later hits shattered their own newly applied Freeze.");
            Check(!frozen.OtherTeam(side).Fighters[0].Statuses.Instances.Any(item=>item.RecipeId=="freeze.before-settle"),"Freeze settled before authored AfterAction effects.");
            var replay=multi.Clone();foreach(var evt in events)BattlePlaybackState.Apply(replay,evt);
            Check(StatusSystem.IsFrozen(replay.OtherTeam(side).Fighters[0]),"Playback lost deferred Freeze.");
        }
        var state=ElementFixture(ElementalRuleSet.CreateDefault());
        var target=state.Opponent.Fighters[0];
        var source=state.Player.Fighters[0];
        var freeze=GenshinStatusRecipes.CreateOutputs().Single(r=>r.Id==StatusSystem.FreezeRecipeId);
        var buff=new StatusRecipeDefinition { Id="fixture.buff", Polarity=StatusPolarity.Buff };
        var debuff=new StatusRecipeDefinition { Id="fixture.debuff", Polarity=StatusPolarity.Debuff };
        var neutral=new StatusRecipeDefinition { Id="fixture.neutral", Polarity=StatusPolarity.Neutral };
        var grey=neutral.Clone(); grey.Id="fixture.grey"; grey.Color=StatusColor.Grey;
        StatusSystem.Apply(target,source.Id,source.Side,buff,"buff","fixture",1);
        StatusSystem.Apply(target,source.Id,source.Side,debuff,"debuff","fixture",2);
        StatusSystem.Apply(target,source.Id,source.Side,neutral,"neutral","fixture",3);
        StatusSystem.Apply(target,source.Id,source.Side,grey,"grey","fixture",3);
        target.Statuses.Instances.First(s=>s.RecipeId==buff.Id).ShieldRemaining=75; target.Shield=100;
        Check(StatusSystem.Apply(target,source.Id,source.Side,freeze,"freeze","fixture",4).Accepted,"Freeze was rejected.");
        Check(target.Statuses.Instances.Count==2 && target.Statuses.Instances.All(s=>s.RecipeId==freeze.Id || s.RecipeId==grey.Id),
            "Freeze did not purge non-grey statuses, including neutral effects.");
        Check(target.Shield==25,"Freeze left shield belonging to a purged buff.");
        Check(target.Statuses.Instances.All(s=>s.RecipeId!="status.reaction.freeze-recovery"),"Freeze ICD leaked into StatusContainer.");
        Check(StatusSystem.IsFreezeRecoveryActive(target) && StatusSystem.IsFreezeRecoveryActive(target.Clone()),"Hidden ICD did not survive cloning.");
        var restored=SnapshotJson.Deserialize<BattleState>(SnapshotJson.Serialize(state));
        Check(StatusSystem.IsFreezeRecoveryActive(restored.Opponent.Fighters[0]),"Hidden ICD did not survive save serialization.");
        foreach(var recipe in new[] { buff,debuff,neutral,freeze })
            Check(!StatusSystem.Apply(target,source.Id,source.Side,recipe,"blocked","fixture",5).Accepted,"Frozen target accepted "+recipe.Id);
        Check(StatusSystem.IsCardUseBlocked(target,CardCategory.Attack,1,false) && StatusSystem.IsCardUseBlocked(target,CardCategory.Buff,1,false) &&
            StatusSystem.IsCardUseBlocked(target,CardCategory.Attack,3,true),"Freeze did not disable all card categories.");
        var projected=BattleProjectionBuilder.Build(state,TeamSide.Player,"freeze");
        Check(projected.OpponentTeam.Fighters.First(f=>f.FighterId==target.Id).FreezeImmune,"Public forecast omitted hidden Freeze lockout.");
        var wet=StandardEffectDatabase.CreateStatusRecipes().First(r=>r.Id=="status.debuff.wet");
        var preparation=new ElementalForecastAction { TargetId=target.Id,Effect=ElementAttack(ElementId.Hydro) };
        var coldAttack=new ElementalForecastAction { TargetId=target.Id,Effect=ElementAttack(ElementId.Cryo) };
        var blockedSetup=ElementalForecast.Build(projected,state.ElementalRules,new[] { preparation,coldAttack });
        Check(blockedSetup.Lines.Count==2 && blockedSetup.Lines.Any(l=>l.Reaction==ElementalReactionId.Shatter) &&
            blockedSetup.Lines.Single(l=>l.Reaction==ElementalReactionId.Freeze).OutputBlocked,
            "Forecast must show Shatter and neutral aura setup while Freeze output obeys recovery.");
        var shatterSetup=ElementalForecast.Build(projected,state.ElementalRules,new[] {
            new ElementalForecastAction { TargetId=target.Id,Effect=ElementAttack(ElementId.None) },preparation,coldAttack });
        Check(shatterSetup.Lines.Any(l=>l.Reaction==ElementalReactionId.Freeze && l.OutputBlocked),
            "Forecast did not allow setup after a physical shatter, with hidden Freeze ICD still active.");
        var forecastState=ElementFixture(ElementalRuleSet.CreateDefault());
        ElementAura(forecastState.Opponent.Fighters[0],ElementId.Hydro,"freeze-forecast-wet");
        var forecastFreeze=ElementalForecast.Build(BattleProjectionBuilder.Build(forecastState,TeamSide.Player,"freeze-sequence"),
            forecastState.ElementalRules,new[] { coldAttack,preparation,coldAttack });
        Check(forecastFreeze.Lines.Count(l=>l.Reaction==ElementalReactionId.Freeze)==2 && forecastFreeze.Lines.Last().OutputBlocked,
            "Forecast must allow neutral aura setup while retaining Freeze output lockout.");
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
            Color=StatusColor.Grey,
            Behavior=StatusBehavior.DamageOverTime,DefaultDuration=4,PeriodicDamage=new PeriodicDamageDefinition {
                Timing=StatusTickTiming.TargetTurnStart,Scaling=StatusSnapshotScaling.Fixed,FixedAmount=20,CoefficientBp=10000 } };
        StatusSystem.Apply(periodicTarget,source.Id,source.Side,neutralDot,"neutral-dot","fixture",2);
        Check(BattleEngine.TryResolvePlan(periodic,new TurnPlan { RequestId="frozen-dot",ExpectedRevision=periodic.Revision },out var afterTick,out var tickError),tickError);
        Check(afterTick.Events.Any(e=>e.Kind==BattleEventKind.DamageApplied && e.StatusRecipeId==neutralDot.Id) &&
            StatusSystem.IsFrozen(afterTick.Opponent.Fighters[0]) && !afterTick.Events.Skip(periodic.Events.Count).Any(e=>
                e.ElementalReaction==ElementalReactionId.Shatter),"Periodic damage incorrectly shattered Freeze.");

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
        ElementAura(healthTarget,ElementId.Hydro,"health-freeze-wet");
        var healthNext=PlayKind(healthState,CardCategory.Attack,EffectTargetScope.SelectedEnemy,ElementAttack(ElementId.Cryo));
        var healthHit=healthNext.Events.Last(e=>e.Kind==BattleEventKind.DamageApplied && e.TargetId==healthTarget.Id);
        var expectedHealth=(int)((long)healthHit.HealthAfter*healthTarget.Stats.MaxHealth/previousMaximum);
        Check(StatusSystem.IsFrozen(healthNext.Opponent.Fighters[0]) && healthNext.Opponent.Fighters[0].Health==expectedHealth,
            "Freeze dispelling max-HP buffs did not preserve the project's health-ratio rule.");
        FreezeRecoveryAndStanceChecks();
        ShatterReactionChecks();
    }

    private static void FreezeRecoveryAndStanceChecks()
    {
        var freeze=GenshinStatusRecipes.CreateOutputs().Single(r=>r.Id==StatusSystem.FreezeRecipeId);
        var heal=StandardEffectDatabase.CreateStatusRecipes().Single(r=>r.Id=="status.buff.rejuvenation");
        foreach(var control in StandardEffectDatabase.CreateStatusRecipes().Where(r=>
            r.Tags.Contains(CombatTags.Stun) || r.Tags.Contains(CombatTags.Paralyze)).Concat(new[] { freeze }))
        {
            var state=ElementFixture(); var target=state.Opponent.Fighters[0]; var source=state.Player.Fighters[0];
            var stance=CounterStance();
            var parent=StatusSystem.Apply(target,target.Id,target.Side,stance,"stance","fixture",1).Instance;
            StatusSystem.Apply(target,target.Id,target.Side,heal,"heal","fixture",2);
            target.Statuses.Instances.First(s=>s.ParentInstanceId==parent.InstanceId).ShieldRemaining=50;
            target.Shield=75;
            Check(StatusSystem.Apply(target,source.Id,source.Side,control,"control","fixture",3).Accepted,
                "Disability failed to apply: "+control.Id);
            Check(!HitReactionRules.IsInStance(target) && target.Statuses.Instances.All(s=>s.ParentInstanceId!=parent.InstanceId),
                control.Id+" left stance children active.");
            Check(target.Shield==25,"Disability left stance-owned shield active.");
            Check(target.Statuses.Instances.Any(s=>s.RecipeId==heal.Id)==(control.Id!=freeze.Id),
                "Only Freeze should purge the independent neutral healing status.");
            if(control.Id==freeze.Id)
                Check(!StatusSystem.Apply(target,target.Id,target.Side,stance,"blocked-stance","fixture",4).Accepted &&
                    !StatusSystem.Apply(target,target.Id,target.Side,heal,"blocked-heal","fixture",4).Accepted,
                    "Frozen target accepted a neutral stance or healing status.");
        }
        var greyState=ElementFixture(); var greyTarget=greyState.Opponent.Fighters[0];
        var greyStance=CounterStance(); greyStance.Color=StatusColor.Grey;
        StatusSystem.Apply(greyTarget,greyTarget.Id,greyTarget.Side,greyStance,"grey-stance","fixture",1);
        StatusSystem.Apply(greyTarget,greyTarget.Id,greyTarget.Side,freeze,"freeze","fixture",2);
        Check(greyTarget.Statuses.Instances.Count(s=>s.InstanceId=="grey-stance" || s.ParentInstanceId=="grey-stance")==4,
            "Freeze removed a grey stance's dependent effects.");
        foreach(var polarity in new[] { StatusPolarity.Buff,StatusPolarity.Debuff,StatusPolarity.Neutral })
            Check(StatusSystem.Apply(greyTarget,greyTarget.Id,greyTarget.Side,new StatusRecipeDefinition {
                Id="fixture.grey."+polarity,Polarity=polarity,Color=StatusColor.Grey },"grey:"+polarity,"fixture",3).Accepted,
                "Freeze blocked an exempt grey status: "+polarity);

        var healing=ElementFixture(); var patient=healing.Player.Fighters[1]; var healer=healing.Player.Fighters[0];
        patient.Health=50000; patient.Stats.RegenerationBp=1000;
        StatusSystem.Apply(patient,healing.Opponent.Fighters[0].Id,TeamSide.Opponent,freeze,"patient-freeze","fixture",1,duration:3);
        var greyHeal=heal.Clone(); greyHeal.Id="fixture.grey-heal"; greyHeal.Color=StatusColor.Grey;
        Check(StatusSystem.Apply(patient,healer.Id,healer.Side,greyHeal,"grey-heal","fixture",2).Accepted,
            "Frozen target did not accept a grey healing status.");
        var directHeal=new EffectDefinition { Kind=EffectKind.Heal,Target=EffectTargetScope.SelectedAlly,
            HealScalingStat=StatId.MaxHealth,HealCoefficientBp=1000 };
        var blocked=PlayKind(healing,CardCategory.Recovery,EffectTargetScope.SelectedAlly,directHeal,patient.Id);
        Check(blocked.Player.FindFighter(patient.Id).Health==50000 && blocked.Events.Skip(healing.Events.Count).Any(e=>
            e.Kind==BattleEventKind.RecoveryBlocked && e.TargetId==patient.Id),"Freeze did not block a teammate's direct healing.");
        Check(BattleEngine.TryResolvePlan(blocked,new PlanDraft(blocked).BuildPlan("frozen-regeneration"),out var recovered,out var error),error);
        Check(StatusSystem.IsFrozen(recovered.Player.FindFighter(patient.Id)) && recovered.Player.FindFighter(patient.Id).Health==50000 &&
            recovered.Events.Skip(healing.Events.Count).Count(e=>e.Kind==BattleEventKind.RecoveryBlocked && e.TargetId==patient.Id)>=3,
            "Freeze did not block regeneration and grey periodic healing.");
        StatusSystem.BreakFreezeOnAttack(recovered.Player.FindFighter(patient.Id));
        var thawed=PlayKind(recovered,CardCategory.Recovery,EffectTargetScope.SelectedAlly,directHeal,patient.Id);
        Check(thawed.Player.FindFighter(patient.Id).Health>50000,"Healing stayed blocked after thawing.");
    }

    private static void ShatterReactionChecks()
    {
        var freeze=GenshinStatusRecipes.CreateOutputs().Single(r=>r.Id==StatusSystem.FreezeRecipeId);
        foreach(var side in new[] { TeamSide.Player,TeamSide.Opponent })
        foreach(var hits in new[] { 1,3,10 })
        foreach(var level in new[] { 1,10,90 })
        {
            var state=ElementFixture(side:side); var source=state.Team(side).Fighters[0]; var target=state.OtherTeam(side).Fighters[0];
            source.Level=level; source.Stats.Attack=12345; DotBonus(source,5000);
            target.Shield=100; target.Stats.Defense=999999; target.Stats.ResistanceBp=10000;
            StatusSystem.Apply(target,source.Id,source.Side,freeze,"shatter-freeze","fixture",1);
            var zeroHit=ElementAttack(ElementId.None,hits); zeroHit.CoefficientBp=0;
            var forecast=ElementalForecast.Build(BattleProjectionBuilder.Build(state,side,"shatter"),state.ElementalRules,
                new[] { new ElementalForecastAction { TargetId=target.Id,Effect=zeroHit } });
            Check(forecast.Lines.Count==1 && forecast.Lines[0].Reaction==ElementalReactionId.Shatter && StatusSystem.IsFrozen(target),
                "Public preview lost Shatter or mutated the frozen target.");
            var next=PlayKind(state,CardCategory.Attack,EffectTargetScope.SelectedEnemy,zeroHit);
            var events=next.Events.Skip(state.Events.Count).ToList();
            var shatter=events.Single(e=>e.Kind==BattleEventKind.DamageApplied && e.ElementalReaction==ElementalReactionId.Shatter);
            var expected=level*500*3*15/10;
            Check(shatter.Amount==expected && shatter.DamageElement==ElementId.None && shatter.DamageOrigin==ElementalDamageOrigin.Reaction &&
                shatter.ShieldLost==100 && shatter.HealthLost==expected-100 && !shatter.WasCritical && !shatter.WasBlocked,
                "Shatter must use one level/DOT-based non-elemental packet at 3x, independent of ATK and defense.");
            Check(events.Count(e=>e.Kind==BattleEventKind.ElementalReactionTriggered && e.ElementalReaction==ElementalReactionId.Shatter)==1 &&
                shatter.ParentEventId==events.First(e=>e.Kind==BattleEventKind.DamageApplied).Id,
                "Multi-hit Shatter duplicated or lost the breaking hit's lineage.");
            Check(!StatusSystem.IsFrozen(next.OtherTeam(side).Fighters[0]) && StatusSystem.IsFreezeRecoveryActive(next.OtherTeam(side).Fighters[0]) &&
                next.OtherTeam(side).Fighters[1].Health==state.OtherTeam(side).Fighters[1].Health,
                "Shatter bypassed recovery, retained Freeze or damaged adjacent enemies.");
            var replay=state.Clone(); foreach(var evt in events) BattlePlaybackState.Apply(replay,evt);
            Check(replay.OtherTeam(side).Fighters[0].Health==next.OtherTeam(side).Fighters[0].Health &&
                replay.OtherTeam(side).Fighters[0].Shield==next.OtherTeam(side).Fighters[0].Shield &&
                !StatusSystem.IsFrozen(replay.OtherTeam(side).Fighters[0]),"Shatter replay disagreed with authoritative health/shield/thaw.");
        }

        var combo=ElementFixture(); combo.ActionBudget=2; combo.Player.Hand.Clear();
        var victim=combo.Opponent.Fighters[0]; victim.ElementStatuses.Apply(ElementId.Hydro,"setup","combo-hydro");
        var cryo=AiCard(combo,1,ElementAttack(ElementId.Cryo,3));
        var followUp=AiCard(combo,2,ElementAttack(ElementId.None,10));
        var draft=new PlanDraft(combo);
        Check(draft.QueuePlay(cryo.Id,victim.Id,out var comboError) && draft.QueuePlay(followUp.Id,victim.Id,out comboError),comboError);
        Check(BattleEngine.TryResolvePlan(combo,draft.BuildPlan("hydro-cryo-shatter"),out var afterCombo,out comboError),comboError);
        var comboEvents=afterCombo.Events.Skip(combo.Events.Count).ToList();
        Check(comboEvents.Count(e=>e.Kind==BattleEventKind.ElementalReactionTriggered && e.ElementalReaction==ElementalReactionId.Shatter)==1 &&
            comboEvents.Single(e=>e.Kind==BattleEventKind.ElementalReactionTriggered && e.ElementalReaction==ElementalReactionId.Shatter).CardId==followUp.Id,
            "Hydro/Cryo Freeze must shatter on the following attack, never on its own multi-hit action.");
        var frozen=ElementFixture(); var frozenVictim=frozen.Opponent.Fighters[1];
        StatusSystem.Apply(frozenVictim,frozen.Player.Fighters[0].Id,TeamSide.Player,freeze,"adjacent-freeze","fixture",1);
        frozen.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Pyro,"setup","adjacent-overload");
        var overloaded=PlayKind(frozen,CardCategory.Attack,EffectTargetScope.SelectedEnemy,ElementAttack(ElementId.Electro));
        Check(overloaded.Events.Skip(frozen.Events.Count).Any(e=>e.Kind==BattleEventKind.DamageApplied &&
            e.ElementalReaction==ElementalReactionId.Shatter && e.TargetId==frozenVictim.Id),"An adjacent Overload attack broke ice without Shatter damage.");

        var evading=ElementFixture(); var evader=evading.Opponent.Fighters[0];
        StatusSystem.Apply(evader,evading.Player.Fighters[0].Id,TeamSide.Player,freeze,"evade-freeze","fixture",1,duration:3);
        StatusSystem.Apply(evader,evader.Id,evader.Side,new StatusRecipeDefinition { Id="grey.evade",Color=StatusColor.Grey,
            Polarity=StatusPolarity.Neutral,EvadeAttacks=true },"evade","fixture",2);
        var missed=PlayKind(evading,CardCategory.Attack,EffectTargetScope.SelectedEnemy,ElementAttack(ElementId.None,3));
        Check(StatusSystem.IsFrozen(missed.Opponent.Fighters[0]) && !missed.Events.Skip(evading.Events.Count).Any(e=>
            e.ElementalReaction==ElementalReactionId.Shatter),"An evaded attack triggered Shatter.");

        var expiring=ElementFixture(side:TeamSide.Opponent); var expiredTarget=expiring.Opponent.Fighters[0];
        StatusSystem.Apply(expiredTarget,expiring.Player.Fighters[0].Id,TeamSide.Player,freeze,"expire-freeze","fixture",1);
        Check(BattleEngine.TryResolvePlan(expiring,new PlanDraft(expiring).BuildPlan("natural-thaw"),out var expired,out var expireError),expireError);
        Check(!StatusSystem.IsFrozen(expired.Opponent.Fighters[0]) && !expired.Events.Skip(expiring.Events.Count).Any(e=>
            e.ElementalReaction==ElementalReactionId.Shatter),"Natural Freeze expiry triggered Shatter damage.");

        var capped=ElementFixture(); capped.ElementalRules.ReactionDamageCap=77; var cappedTarget=capped.Opponent.Fighters[0];
        cappedTarget.Health=50;
        StatusSystem.Apply(cappedTarget,capped.Player.Fighters[0].Id,TeamSide.Player,freeze,"cap-freeze","fixture",1);
        var zero=ElementAttack(ElementId.None,3); zero.CoefficientBp=0;
        var killed=PlayKind(capped,CardCategory.Attack,EffectTargetScope.SelectedEnemy,zero);
        Check(killed.Events.Skip(capped.Events.Count).Single(e=>e.Kind==BattleEventKind.DamageApplied && e.ElementalReaction==ElementalReactionId.Shatter).Amount==77 &&
            !killed.Opponent.Fighters[0].IsAlive && killed.Events.Skip(capped.Events.Count).Count(e=>e.Kind==BattleEventKind.FighterDefeated &&
                e.SourceId==cappedTarget.Id)==1,"Shatter ignored the reaction cap or duplicated/missed a lethal defeat.");

        var aoe=ElementFixture();
        foreach(var recipient in aoe.Opponent.LivingActive())
            StatusSystem.Apply(recipient,aoe.Player.Fighters[0].Id,TeamSide.Player,freeze,"aoe-freeze:"+recipient.Id,"fixture",1);
        var aoeHit=ElementAttack(ElementId.None,3); aoeHit.CoefficientBp=0; aoeHit.Target=EffectTargetScope.AllEnemies;
        var shattered=PlayKind(aoe,CardCategory.Attack,EffectTargetScope.AllEnemies,aoeHit);
        Check(shattered.Events.Skip(aoe.Events.Count).Count(e=>e.Kind==BattleEventKind.DamageApplied &&
            e.ElementalReaction==ElementalReactionId.Shatter)==3,"AOE must shatter each frozen recipient exactly once.");
    }
}
