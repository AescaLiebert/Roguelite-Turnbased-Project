using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FightingAllstar.Presentation;
using FightingAllstar.Presentation.Navigation;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Read-only battle dossier. All displayed values come from one committed snapshot.</summary>
    public sealed class BattleCharacterInspector : MonoBehaviour
    {
        private static readonly Color Surface = new Color(.055f,.09f,.14f,.98f);
        private static readonly Color Raised = new Color(.085f,.135f,.195f);
        private static readonly Color Accent = new Color(.34f,.78f,.88f);
        private static readonly Color Muted = new Color(.57f,.66f,.75f);
        private RectTransform panel, body, popup;
        [NonSerialized] private BattleState state;
        private FighterState selected;
        private Func<string, Sprite> portrait;
        private Func<string, CharacterObject> characterLookup;
        private Func<string, CoreFighterHud> hud;
        private Func<FighterState,int,Sprite> cardArt;
        private Func<FighterState,int,string> cardName;
        private Func<FighterState,int,int,CoreBattleSceneController.InspectorCardTooltipData> cardTooltip;
        private Action closed;
        private int page;
        private readonly List<TextMeshProUGUI> fittedRows = new List<TextMeshProUGUI>();

        public void Open(BattleState snapshot, string id, Func<string, Sprite> portraits,
            Func<string, CoreFighterHud> huds, Action onClose,
            Func<FighterState,int,Sprite> cardArtwork = null, Func<FighterState,int,string> cardNames = null,
            Func<FighterState,int,int,CoreBattleSceneController.InspectorCardTooltipData> tooltipBuilder = null,
            Func<string, CharacterObject> characters = null)
        {
            state=snapshot; portrait=portraits; characterLookup=characters; hud=huds; closed=onClose; cardArt=cardArtwork; cardName=cardNames; cardTooltip=tooltipBuilder;
            var canvas=gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=30000;
            var scaler=gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if(UnityEngine.EventSystems.EventSystem.current==null)
            {
                var events=new GameObject("Inspector input",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.EventSystems.StandaloneInputModule));
                events.transform.SetParent(transform,false);
            }
            var shade=Box(transform,"Backdrop",new Color(.005f,.015f,.025f,.8f)); Stretch(shade);
            panel=Box(shade,"Character inspector",Surface);
            panel.anchorMin=new Vector2(.07f,.055f);panel.anchorMax=new Vector2(.93f,.945f);panel.offsetMin=panel.offsetMax=Vector2.zero;
            Show(id);
        }
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape)) { if(popup!=null) DismissCard(); else Close(); }
        }
        private void LateUpdate()
        {
            foreach(var text in fittedRows)
            {
                if(text==null || text.rectTransform.rect.width<=0) continue;
                var element=text.transform.parent.GetComponent<LayoutElement>();
                if(element==null) continue;
                var padding=Mathf.Max(32,text.rectTransform.offsetMin.y-text.rectTransform.offsetMax.y);
                var height=Mathf.Max(112,text.GetPreferredValues(text.text,text.rectTransform.rect.width,0).y+padding);
                element.minHeight=Mathf.Max(element.minHeight,112);
                if(Mathf.Abs(element.preferredHeight-height)>1) element.preferredHeight=height;
            }
        }
        private void Close() { closed?.Invoke(); Destroy(gameObject); }
        private FighterState Find(string id)=>state.Player.FindFighter(id)??state.Opponent.FindFighter(id);
        private static string Name(FighterState f)=>f==null?"Unknown source":string.IsNullOrWhiteSpace(f.Definition.DisplayName)?f.Definition.Id:f.Definition.DisplayName;
        private void Show(string id)
        {
            DismissCard(); selected=Find(id); if(selected==null)return;
            fittedRows.Clear(); Clear(panel);
            var heroCharacter = characterLookup?.Invoke(selected.Definition.Id);
            var hero = heroCharacter != null
                ? CharacterIconView.CreateUGUI(panel, "Selected character icon", heroCharacter)
                : Picture(panel,"Selected portrait",portrait(selected.Definition.Id));
            Place(hero,24,22,64,64);
            var name=Text(panel,Name(selected),30); Place(name.rectTransform,102,20,-195,40);
            var stateText=(selected.Side==TeamSide.Player?"YOUR TEAM":"ENEMY TEAM")+"  /  "+(selected.IsReserve?"SUB":selected.IsAlive?"ACTIVE":"DEFEATED");
            var identity=Text(panel,stateText,15,Muted);Place(identity.rectTransform,104,62,-205,25);
            var exit=Button(panel,"Close","×",Close);Place(exit, -70,22,46,44,true);
            var roster=Box(panel,"Roster",Color.clear);Place(roster,24,105,-48,60);Horizontal(roster,8);
            foreach(var member in state.Team(selected.Side).Fighters)
            {
                var memberId=member.Id;
                var entry=Button(roster,"Select "+Name(member),"",()=>Show(memberId),memberId==id);
                entry.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
                var memberCharacter = characterLookup?.Invoke(member.Definition.Id);
                var icon = memberCharacter != null
                    ? CharacterIconView.CreateUGUI(entry, "Character icon", memberCharacter)
                    : Picture(entry,"Portrait",portrait(member.Definition.Id));
                Place(icon,8,7,44,44);
                var label=Text(entry,Name(member)+(member.IsReserve?"  <color=#79DBEE>SUB</color>":!member.IsAlive?"  KO":""),16);
                Stretch(label.rectTransform,62,7,6,7);label.alignment=TextAlignmentOptions.MidlineLeft;label.enableAutoSizing=true;label.fontSizeMin=11;label.fontSizeMax=16;
            }
            var tabs=Box(panel,"Sections",Color.clear);Place(tabs,24,185,-48,44);Horizontal(tabs,8);
            var labels=new[]{"Overview","Effects","Cards"};
            for(int i=0;i<labels.Length;i++)
            {
                int tab=i;var b=Button(tabs,"Tab "+labels[i],labels[i],()=>{page=tab;Show(selected.Id);},page==tab);
                b.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
            }
            body=Box(panel,"Body",Color.clear);Stretch(body,24,246,24,22);
            var content=Scroll(body,"Details");
            if(page==0) BuildOverview(content);
            else if(page==1) BuildEffects(content);
            else BuildCards(content);
        }
        private void BuildOverview(RectTransform content)
        {
            var effective=StatusSystem.GetEffectiveStats(selected);
            var primary=Box(content,"Primary stats",Color.clear);Height(primary,112);Horizontal(primary,12);
            foreach(var stat in new[]{StatId.Attack,StatId.Defense,StatId.MaxHealth})
            {
                var tile=Box(primary,stat.ToString(),Raised);tile.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
                var label=Text(tile,stat==StatId.MaxHealth?"MAX HP":Label(stat.ToString()).ToUpperInvariant(),14,Muted);Place(label.rectTransform,16,12,-32,22);
                var value=Text(tile,StatValue(selected,stat,effective),28);Place(value.rectTransform,16,44,-32,48);
            }
            var health=Box(content,"Health",Raised);Height(health,54);
            var hp=Text(health,"<color=#94AABD>HP</color>  "+selected.Health.ToString("N0")+" / "+effective.MaxHealth.ToString("N0")+
                (selected.Shield>0?"    <color=#94AABD>SHIELD</color>  "+selected.Shield.ToString("N0"):""),20);Stretch(hp.rectTransform,16,12,16,8);
            Heading(content,"DETAILED STATS");
            var stats=((StatId[])Enum.GetValues(typeof(StatId))).Where(s=>s>=StatId.Pierce&&
                s!=StatId.ReflectDamage&&s!=StatId.Evade&&s!=StatId.Perception&&s!=StatId.Control&&s!=StatId.Avoidance).ToArray();
            for(int i=0;i<stats.Length;i+=2)
            {
                var row=Box(content,"Stat row",Color.clear);Height(row,40);Horizontal(row,16);
                for(int j=i;j<Math.Min(i+2,stats.Length);j++)
                {
                    var cell=Box(row,stats[j].ToString(),Raised);cell.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
                    var label=Text(cell,Label(stats[j].ToString()),17,Muted);Stretch(label.rectTransform,12,8,8,4);label.rectTransform.anchorMax=new Vector2(.5f,1);
                    var value=Text(cell,StatValue(selected,stats[j],effective),18);Stretch(value.rectTransform,4,8,12,4);value.rectTransform.anchorMin=new Vector2(.5f,0);value.alignment=TextAlignmentOptions.TopRight;
                }
            }
            var cc=Text(content,"Combat class  "+StatValue(selected,StatId.CombatClass,effective),17,Muted);Height(cc.rectTransform,32);
            BuildPassiveSection(content, selected);
        }
                private void BuildPassiveSection(RectTransform content, FighterState fighter)
        {
            var charObj = characterLookup?.Invoke(fighter.Definition.Id);
            var passive = CombatDetailFormatter.ExtractPassiveDetails(fighter, charObj, (id, pol) => StatusVisualData.GetSprite(id, pol));
            if (string.IsNullOrWhiteSpace(passive.RawDescription)) return;

            Heading(content, "PASSIVE");

            var preview = Box(content, "Passive artwork", new Color(.97f, .90f, .74f)); Height(preview, 130);
            var passiveIcon = Picture(preview, "Passive icon", passive.Icon != null ? passive.Icon : portrait?.Invoke(fighter.Definition.Id));
            passiveIcon.anchorMin = passiveIcon.anchorMax = new Vector2(.5f, .5f); passiveIcon.pivot = new Vector2(.5f, .5f);
            passiveIcon.sizeDelta = new Vector2(110, 110); passiveIcon.anchoredPosition = Vector2.zero;
            var row = Box(content, "Own passive", new Color(.325f, .282f, .224f));
            Height(row, 112);
            var conditions = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt")?.PassiveConditions(passive.SourceType).Where(c => c.icon != null).ToList();
            var badgeCount = (passive.Statuses?.Count ?? 0) + (conditions?.Count ?? 0);
            var badgeIndex = 0;
            if (conditions != null)
                foreach (var condition in conditions)
                {
                    var conditionIcon = Picture(row, "Passive condition " + condition.label, condition.icon);
                    Place(conditionIcon, -10 - (badgeCount - badgeIndex++) * 32, 14, 28, 28, true);
                }
            foreach (var badge in passive.Statuses ?? new List<CombatStatusBadgeData>())
            {
                var statusIcon = Picture(row, "Passive status icon " + badge.Name, badge.Icon);
                Place(statusIcon, -10 - (badgeCount - badgeIndex++) * 32, 14, 28, 28, true);
            }
            var title = Text(row, passive.Title, 20); Place(title.rectTransform, 16, 12, -(badgeCount * 32 + 36), 30);
            title.enableAutoSizing = true; title.fontSizeMin = 14; title.fontSizeMax = 20;
            var textContent = passive.FormattedDescription;
            if (!string.IsNullOrWhiteSpace(passive.Keywords))
            {
                textContent += "\n<size=15>" + passive.Keywords + "</size>";
            }

            var text = Text(row, textContent, 19);
            text.richText = true;
            Stretch(text.rectTransform, 16, 54, 16, 14);
            fittedRows.Add(text);
        }
        public static string StatValue(FighterState fighter,StatId stat,StatBlock effective)
        {
            int total=effective.Get(stat),initial=fighter.Stats.Get(stat);long delta=(long)total-initial;
            bool ratio=stat>=StatId.Pierce;
            var value=ratio?(total/100d).ToString("0.##")+"%":total.ToString("N0");
            if(delta==0)return value;
            string change=ratio?(delta/100d).ToString("+0.##;-0.##;0")+"%":(delta>0?"+":"")+delta.ToString("N0");
            return "<color="+(delta>0?"#79E5A2":"#FF8894")+">"+value+"</color> <size=70%>("+change+")</size>";
        }
        private void BuildEffects(RectTransform content)
        {
            Heading(content,"BUFFS & DEBUFFS  ·  "+selected.Statuses.Instances.Count);
            if(selected.Statuses.Instances.Count==0) Empty(content,"No active buffs or debuffs");
            foreach(var status in selected.Statuses.Instances)
            {
                var polarity=status.Recipe?.Polarity??StatusPolarity.Neutral;
                var visual=StatusVisualData.Get(status.RecipeId,polarity);
                string duration=status.Recipe?.DurationClock==StatusDurationClock.Permanent?"Permanent":status.RemainingDuration+" turn"+(status.RemainingDuration==1?"":"s");
                var metadata=duration+(status.StackCount>1?"  ·  "+status.StackCount+" stacks":"");
                var desc=Describe(status);
                if(string.IsNullOrWhiteSpace(desc))desc=visual?.Description;
                if(string.IsNullOrWhiteSpace(desc))desc="Active combat effect.";
                EffectRow(content,"Status "+status.RecipeId,StatusVisualData.GetDisplayName(status.RecipeId,polarity),metadata,desc,Find(status.SourceFighterId),status,polarity==StatusPolarity.Debuff);
            }
            Heading(content,"PASSIVE EFFECTS");
            var groups=selected.PassiveContributions.GroupBy(c=>c.OwnerId+"|"+c.RuleId).ToList();
            if(groups.Count==0)Empty(content,"No passive stat changes");
            foreach(var group in groups)
            {
                var owner=Find(group.First().OwnerId);
                var changes=string.Join("  ·  ",group.Select(c=>CombatDetailFormatter.DescribeModifier(c.Modifier,c.Modifier.Amount)));
                var description=CombatDetailFormatter.ExtractPassiveDetails(owner).FormattedDescription;
                EffectRow(content,"Passive "+group.Key,Name(owner),"Passive"+(owner?.IsReserve==true?"  ·  SUB":""),
                    (string.IsNullOrWhiteSpace(description)?"":description+"\n")+"<color=#79DBEE>"+changes+"</color>",owner,null,false);
            }
            var counters="Own turns completed  "+selected.OwnerTurnsCompleted;
            foreach(var counter in selected.PassiveCounters)counters+="    ·    "+Label(counter.Key)+"  "+counter.Value;
            var clock=Text(content,counters,16,Muted);Height(clock.rectTransform,34);
        }
        private void EffectRow(RectTransform content,string key,string title,string meta,string description,FighterState owner,StatusInstance status,bool debuff)
        {
            var row=Box(content,key,debuff?new Color(.24f,.10f,.15f):new Color(.065f,.205f,.29f));Height(row,112);
            var stripe=Box(row,"Accent",debuff?new Color(.9f,.36f,.44f):Accent);Stretch(stripe);stripe.anchorMax=new Vector2(0,1);stripe.sizeDelta=new Vector2(3,0);
            RectTransform icon=status!=null?hud(selected.Id)?.CreateInspectorStatusIcon(status,row):null;
            if(icon==null)icon=Picture(row,"Effect icon",status==null?(owner==null?null:portrait(owner.Definition.Id)):StatusVisualData.GetSprite(status.RecipeId,status.Recipe?.Polarity??StatusPolarity.Neutral));
            Place(icon,16,18,54,54);
            var text=Text(row,"<b>"+title+"</b> <size=70%><color=#94BACB>"+meta+"</color></size>\n<size=19>"+description+"</size>",22);
            Stretch(text.rectTransform,86,14,84,14);fittedRows.Add(text);
            var source=Picture(row,"Source portrait",owner==null?null:portrait(owner.Definition.Id));Place(source,-66,18,48,48,true);
        }
        private void BuildCards(RectTransform content)
        {
            Heading(content,"CHARACTER CARDS");
            var hint=Text(content,"Select a card to compare its ranks or ultimate levels.",18,Muted);Height(hint.rectTransform,32);
            foreach(var skill in selected.Definition.Skills.OrderBy(s=>s.Slot)) CardEntry(content,skill.Slot,"SKILL "+skill.Slot,"Ranks 1–3");
            CardEntry(content,0,"ULTIMATE","Levels 0–5");
        }
        private string CardName(int slot)
        { var name=cardName?.Invoke(selected,slot);return string.IsNullOrWhiteSpace(name)?slot==0?"Ultimate":"Skill "+slot:name; }
        private void CardEntry(RectTransform content,int slot,string label,string levels)
        {
            var row=Button(content,"Card "+slot,"",()=>ShowCard(slot,slot==0?Mathf.Clamp(selected.ConstellationTier,0,5):1));Height(row,125);
            var art=Picture(row,"Card artwork",cardArt?.Invoke(selected,slot));Place(art,16,10,76,104);
            var type=Text(row,label,14,Accent);Place(type.rectTransform,112,16,-150,23);
            var name=Text(row,CardName(slot),25);Place(name.rectTransform,112,43,-150,34);
            var caption=Text(row,levels+"  ·  Tap to inspect",17,Muted);Place(caption.rectTransform,112,83,-150,25);
        }
        private void DismissCard(){if(popup==null)return;popup.gameObject.SetActive(false);Destroy(popup.gameObject);popup=null;}
        private void ShowCard(int slot,int rank)
        {
            DismissCard();popup=Box(transform,"Card detail overlay",new Color(0,.01f,.025f,.85f));Stretch(popup);
            var card=Box(popup,"Card details",new Color(.97f,.90f,.74f));card.anchorMin=new Vector2(.23f,.08f);card.anchorMax=new Vector2(.77f,.92f);card.offsetMin=card.offsetMax=Vector2.zero;
            var title=Text(card,slot==0?"ULTIMATE DETAILS":"SKILL DETAILS",26,new Color(.33f,.24f,.15f));Place(title.rectTransform,24,18,-98,40);title.alignment=TextAlignmentOptions.Center;
            var close=Button(card,"Close card","×",DismissCard);Place(close,-66,20,42,40,true);
            close.GetComponent<Image>().color=new Color(.62f,.10f,.12f);
            var kitArt=Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
            var charObj=characterLookup?.Invoke(selected.Definition.Id);
            float copyTop;
            if(slot==0)
            {
                var hero=Box(card,"Ultimate card preview",Color.clear);Place(hero,24,68,-48,192);CardPreview(hero,slot,3,kitArt,charObj);
                var current=Text(card,"CURRENT LEVEL "+selected.ConstellationTier,18,new Color(.41f,.29f,.13f));Place(current.rectTransform,24,263,-48,28);current.alignment=TextAlignmentOptions.Center;
                copyTop=360;
            }
            else copyTop=320;
            var choices=Box(card,"Card levels",Color.clear);Place(choices,24,slot==0?298:76,-48,slot==0?48:228);Horizontal(choices,6);
            for(int i=slot==0?0:1;i<=(slot==0?5:3);i++)
            {
                if(slot!=0&&i>1){var arrow=Text(choices,"→",38,new Color(.80f,.56f,.13f));arrow.alignment=TextAlignmentOptions.Center;var arrowLayout=arrow.gameObject.AddComponent<LayoutElement>();arrowLayout.preferredWidth=38;arrowLayout.flexibleWidth=0;}
                int level=i;var choice=Button(choices,(slot==0?"Level ":"Rank ")+i,slot==0?"Lv "+i+(i==selected.ConstellationTier?"\n<size=11>CURRENT</size>":""):"",()=>ShowCard(slot,level),i==rank);
                choice.GetComponent<Image>().color=i==rank?new Color(.12f,.75f,.84f,.35f):Color.clear;
                if(slot==0){choice.GetComponent<Image>().color=i==rank?new Color(.76f,.90f,.87f):new Color(.94f,.84f,.65f);choice.GetComponentInChildren<TextMeshProUGUI>().color=new Color(.33f,.24f,.15f);}
                choice.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
                if(slot!=0){CardPreview(choice,slot,i,kitArt,charObj);var caption=Text(choice,"R"+i,17,new Color(.33f,.24f,.15f));Place(caption.rectTransform,0,200, -1,24);caption.alignment=TextAlignmentOptions.Center;}
            }
            var root=Box(card,"Card description",new Color(.325f,.282f,.224f));Stretch(root,0,copyTop,0,0);
            var inset=Box(root,"Description inset",Color.clear);Stretch(inset,20,16,20,16);var content=Scroll(inset,"Card scroll");
            var skill=selected.Definition.Skills.FirstOrDefault(s=>s.Slot==slot);
            var skillRank=skill?.Ranks.FirstOrDefault(r=>r.Rank==rank);
            var ultimate=selected.Definition.UltimateTiers.FirstOrDefault(t=>t.Tier==rank);
            bool exists=slot==0?ultimate!=null:skillRank!=null;
            var tooltip=cardTooltip?.Invoke(selected,slot,rank);
            var cardHeader=Box(content,"Tooltip card header",Color.clear);Height(cardHeader,56);
            var typeIcon=Picture(cardHeader,"Skill icon",tooltip?.CardTypeIcon);Place(typeIcon,8,12,32,32);
            if(tooltip?.CardTypeIcon==null)typeIcon.gameObject.SetActive(false);
            var statusBadges=tooltip?.Statuses??new List<CoreBattleSceneController.StatusBadgeData>();
            var headerTitle=Text(cardHeader,tooltip?.Title??CardName(slot),20);headerTitle.enableWordWrapping=true;headerTitle.enableAutoSizing=true;headerTitle.fontSizeMin=14;headerTitle.fontSizeMax=20;
            Stretch(headerTitle.rectTransform,52,12,Mathf.Max(14,statusBadges.Count*34+8),12);headerTitle.alignment=TextAlignmentOptions.MidlineLeft;
            for(int i=0;i<statusBadges.Count;i++)
            {
                var icon=Picture(cardHeader,"Tooltip status icon "+statusBadges[i].Name,statusBadges[i].Icon);
                Place(icon,-10-(statusBadges.Count-i)*32,20,28,28,true);
            }
            var rankLabel=Text(content,(slot==0?"ULTIMATE LEVEL ":"RANK ")+rank,14,Accent);Height(rankLabel.rectTransform,24);
            var divider=Box(content,"Tooltip divider",new Color(.88f,.70f,.38f,.6f));Height(divider,1);
            var descriptionText=tooltip?.Description;
            if(string.IsNullOrWhiteSpace(descriptionText))descriptionText=exists?(slot==0?ultimate?.Description:skillRank?.Description):"No card data is authored for this level.";
            var description=Text(content,descriptionText,17,new Color(.94f,.92f,.88f));description.richText=true;description.enableWordWrapping=true;description.margin=new Vector4(3,2,3,4);var descElement=description.gameObject.AddComponent<LayoutElement>();descElement.minHeight=76;
            if(!string.IsNullOrWhiteSpace(tooltip?.Keywords))
            {
                var keywords=Text(content,tooltip.Keywords,15,new Color(.34f,.74f,.93f));keywords.fontStyle=FontStyles.Bold;keywords.enableWordWrapping=true;var kwElement=keywords.gameObject.AddComponent<LayoutElement>();kwElement.minHeight=40;
            }
        }
        private void CardPreview(RectTransform parent,int slot,int rank,CharacterKitArtSO art,CharacterObject character)
        {
            var face=Box(parent,"Rank card artwork",Color.clear);face.anchorMin=face.anchorMax=new Vector2(.5f,1);face.pivot=new Vector2(.5f,1);face.anchoredPosition=new Vector2(0,-4);face.sizeDelta=new Vector2(104,188);
            var sprite=cardArt?.Invoke(selected,slot);if(sprite==null)sprite=portrait?.Invoke(selected.Definition.Id);
            var crop=Box(face,"Artwork crop",Color.clear);Stretch(crop,3,4,3,18);crop.gameObject.AddComponent<RectMask2D>();
            var artwork=Picture(crop,"Card image",sprite);artwork.anchorMin=artwork.anchorMax=new Vector2(.5f,.5f);artwork.pivot=new Vector2(.5f,.5f);artwork.anchoredPosition=Vector2.zero;
            var factor=sprite!=null?Mathf.Max(98/sprite.rect.width,166/sprite.rect.height):1;
            artwork.sizeDelta=sprite!=null?sprite.rect.size*factor:new Vector2(98,166);artwork.GetComponent<Image>().preserveAspect=true;
            var frame=new GameObject("Rank frame",typeof(RectTransform),typeof(RawImage));frame.transform.SetParent(face,false);Stretch((RectTransform)frame.transform);frame.GetComponent<RawImage>().texture=art?.Rank(rank);frame.GetComponent<RawImage>().raycastTarget=false;
            var skill=slot==1?character?.Skill1:character?.Skill2;
            var type=slot==0?null:art?.Type(skill?.GetRankData(rank)?.skillType??SkillType.Attack);
            if(type!=null){var icon=Picture(face,"Card badge",type);Place(icon,-30,6,24,24,true);}
        }
        private static string Describe(StatusInstance status)
        {
            var r = status.Recipe;
            if (r == null) return "Active combat effect.";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var m in r.Modifiers)
            {
                var amount = m.ScaleByStatusPotency ? (long)status.PotencyBp * m.PotencyCoefficientBp / 10000 : m.Amount;
                if (r.Stacking == StatusStackingPolicy.AddStacks && m.Operation != ModifierOperation.Multiplier) amount *= Math.Max(1, status.StackCount);
                parts.Add(CombatDetailFormatter.DescribeModifier(m,amount));
            }
            if (r.PeriodicDamage != null && (r.Behavior & StatusBehavior.DamageOverTime) != 0)
                parts.Add("Deals " + (r.PeriodicDamage.Scaling == StatusSnapshotScaling.Fixed ? r.PeriodicDamage.FixedAmount.ToString() :
                    (r.PeriodicDamage.CoefficientBp / 100d).ToString("0.##") + "% of " + Label(r.PeriodicDamage.Scaling.ToString())) +
                    " damage · " + Label(r.PeriodicDamage.Timing.ToString()) + ".");
            if (r.DebuffImmunity) parts.Add("Prevents debuffs.");
            if (r.AdditionalDamageImmunity) parts.Add("Prevents additional damage.");
            if (r.HasTaunt) parts.Add("Forces enemy single-target cards to target this fighter.");
            if (r.EvadeAttacks) parts.Add("Evades attacks.");
            if (r.DisableMask != CardCategoryMask.None) parts.Add("Disables: " + Label(r.DisableMask.ToString()) + ".");
            if (r.RecoverDamageTakenBp != 0) parts.Add("Recovers " + (r.RecoverDamageTakenBp / 100d).ToString("0.##") + "% of damage received at own turn start.");
            if (r.CounterEnabled) parts.Add("Counters after the attack finishes: " + Label(r.CounterCategory.ToString()) + ".");
            if (r.SurviveLethalCharges > 0) parts.Add("Survives lethal damage " + r.SurviveLethalCharges + " time(s).");
            if (r.ImmuneStatusTags.Count > 0) parts.Add("Immune to: " + string.Join(", ", r.ImmuneStatusTags) + ".");
            if (!string.IsNullOrEmpty(status.ParentInstanceId)) parts.Add("Ends with its parent stance.");
            return parts.Count > 0 ? string.Join(" ", parts) : string.Empty;
        }

        private static string Label(string s)=>System.Text.RegularExpressions.Regex.Replace(s??"","([a-z])([A-Z])","$1 $2").Replace('-',' ');
        private static void Clear(Transform root){foreach(Transform child in root){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
        private static RectTransform Box(Transform parent,string name,Color color)
        {var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);g.GetComponent<Image>().color=color;return (RectTransform)g.transform;}
        private static RectTransform Picture(Transform parent,string name,Sprite sprite)
        {var r=Box(parent,name,sprite==null?new Color(.12f,.2f,.27f):Color.white);var image=r.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return r;}
        private static TMP_FontAsset inspectorFont;
        private static TextMeshProUGUI Text(Transform parent,string value,float size,Color? color=null)
        {var g=new GameObject("Text",typeof(RectTransform));g.transform.SetParent(parent,false);var t=g.AddComponent<TextMeshProUGUI>();if(inspectorFont==null)inspectorFont=Resources.Load<TMP_FontAsset>("UI/BarlowCondensed-SemiBoldItalic SDF");if(inspectorFont!=null)t.font=inspectorFont;t.text=value;t.fontSize=size;t.color=color??new Color(.91f,.95f,1);t.raycastTarget=false;return t;}
        private static RectTransform Button(Transform parent,string name,string text,Action action,bool selected=false)
        {var r=Box(parent,name,selected?new Color(.09f,.30f,.38f):Raised);var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.onClick.AddListener(()=>action());var label=Text(r,text,19,selected?Accent:Color.white);Stretch(label.rectTransform,6,3,6,3);label.alignment=TextAlignmentOptions.Center;return r;}
        private static void Heading(Transform parent,string text){var t=Text(parent,text,15,Accent);Height(t.rectTransform,30);}
        private static void Empty(Transform parent,string text){var row=Box(parent,"Empty",Raised);Height(row,54);var label=Text(row,text,18,Muted);Stretch(label.rectTransform,16,14,16,10);}
        private static void Horizontal(RectTransform r,float gap){var l=r.gameObject.AddComponent<HorizontalLayoutGroup>();l.spacing=gap;l.childControlHeight=true;l.childForceExpandHeight=true;l.childControlWidth=true;l.childForceExpandWidth=true;}
        private static void Height(RectTransform r,float height){var e=r.GetComponent<LayoutElement>()??r.gameObject.AddComponent<LayoutElement>();e.preferredHeight=height;e.minHeight=height;}
        private static RectTransform Scroll(RectTransform root,string name)
        {
            var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var viewport=Box(root,"Viewport",Color.clear);Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Box(viewport,name,Color.clear);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;
            return content;
        }
        private static void Stretch(RectTransform r,float l=0,float t=0,float right=0,float b=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-right,-t);}
        private static void Place(RectTransform r,float x,float y,float w,float h,bool right=false)
        {r.anchorMin=new Vector2(right?1:0,1);r.anchorMax=new Vector2(w<0?1:(right?1:0),1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    }
}
