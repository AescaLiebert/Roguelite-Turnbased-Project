"""Reproducible, review-only authoring drafts. Never imports content into Unity."""
import csv, hashlib, json, math, re
from pathlib import Path

ROOT = Path(__file__).resolve().parent
INPUT = Path('E:/Drive_E_Download')
OUT = ROOT / 'CharacterData'
OUT.mkdir(exist_ok=True)
FILES = {
    'characters': 'WIP_Character Database - WIP-Character (1).csv',
    'stats': 'Copy of 7DSGC Database - Stats Introduction (1).csv',
    'attackEffects': 'Copy of 7DSGC Database - Attack Effect Database (2).csv',
    'debuffEffects': 'Copy of 7DSGC Database - Debuff Effect Database (1).csv',
}
sources = {}
for kind, name in FILES.items():
    path = INPUT / name
    sources[kind] = {'file': name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                     'rows': list(csv.DictReader(path.open(encoding='utf-8-sig', newline='')))}
guide = INPUT / '7DSGC Copycat game Guide.pdf'
sources['guide'] = {'file': guide.name, 'sha256': hashlib.sha256(guide.read_bytes()).hexdigest(), 'pages': 7}
(OUT / 'source-catalog.json').write_text(json.dumps(sources, ensure_ascii=False, indent=2), encoding='utf-8')

STAT_FIELDS = list(sources['characters']['rows'][0])[22:37]
# Starting values: explicit role profiles, not estimates of official franchise stats.
GENERATED = {
    10: [2850,300,230,4400,15,15,10,25,140,15,20,115,10,20,5],
    17: [2650,310,130,3600,30,5,10,20,135,5,10,110,5,20,5],
    27: [2700,260,250,4300,10,20,15,10,120,15,20,120,20,30,5],
    28: [2600,230,170,3900,10,15,10,10,120,10,15,115,10,20,5],
    29: [3000,390,180,4100,20,10,10,30,160,40,15,110,5,20,5],
}
ISSUES = {
 1:['Ignite ultimate duration missing: propose 3 owner turns. Relic disabled until unlock design exists.'],
 2:['Uncapped team mitigation: propose 30% cap as a starting value. Ignite ultimate duration proposed 3 turns.'],
 3:['Card 2 description disables Recovery; effect tag says Attack. Draft follows description.'],
 4:['Passive scope All conflicts with own-attacks wording: draft self-only, current action, non-stacking.'],
 6:['Second passive sentence has no reliable trigger/target: retain verbatim, do not execute that clause.'],
 8:['Skill 1 says Stun but tag says disable stance: follow Stun description. Skill 2 Self target; propose 2-turn duration. Ultimate female condition applies to Paralyze; damage/Shock hit all enemies (proposal).'],
 9:['PVP-only passive is inactive in dungeons; do not silently activate it. Skill 1 duration proposed 2 turns.'],
 10:['All stats generated. PG gain proposed equal to actual PG removed, once per card; excludes ultimate.'],
 11:['Ultimate is composite: heal all allies, cleanse Women allies, damage all enemies. Separate target queries.'],
 12:['Skill 2 says Single target but description all allies: follow all allies. Ultimate target field Heal is a category; propose SelectedAlly. Ultimate stat buff duration proposed 3 turns.'],
 15:['Passive text specifies PvP despite missing mode tag: normalize mode to PvPOnly.'],
 17:['All stats generated. Explosion delay missing: propose 2 affected-owner turn ends. Passive observes own side turn-end damage ledger.'],
 18:['SUB self Lifesteal is ineffective while benched if reserve-only: propose SUB means active-or-reserve. Ultimate lowest what is incomplete: propose lowest current HP percentage, ties all qualify.'],
 21:['Stance Evade magnitude absent: propose guaranteed evade of next hostile damaging card; still permits later damage. Relic gauge gain means current gauge, not lifetime gains (proposal).'],
 22:['Relic team predicate/heal recipient ambiguous; retain disabled pending explicit definition.'],
 23:['On enemy defeat PG drain can cascade: once per root action starting value. Relic remains disabled.'],
 24:['Explicit +7 percentage points per ultimate level retained across C0..C6. Relic ignore30% uses multiplicative removal of target CritDefense.'],
 27:['All stats generated; Barrier duration source 2 turns.'],
 28:['All stats generated. Skill 2 shared 50% proc gates both removal and stun (proposal).'],
 29:['All stats generated. Passive halves own CritResistance value to form enemy CritChance reduction.'],
}

def fmt(x):
    return f'{x:.2f}'.rstrip('0').rstrip('.')

def rank_text(text, rank):
    if rank == 3:
        return text.strip()
    # Draft interpolation: percent potency/magnitudes, integer durations and counts.
    text = re.sub(r'(\d+(?:\.\d+)?)%', lambda m: fmt(float(m[1])*rank/3)+'%', text)
    text = re.sub(r'\b(\d+) (turn(?:\(s\)|s)?|gauges?|Ignites?|ignite)\b',
                  lambda m: str(max(1, math.ceil(int(m[1])*rank/3)))+' '+m[2], text, flags=re.I)
    return text.strip()

def ult_text(text, tier, source_id):
    text = text.strip()
    # Only ATK-based primary damage/healing scales by default; no automatic duration/CC growth.
    text = re.sub(r'(\d+(?:\.\d+)?)%( of)?( the)?( own)? ATK',
                  lambda m: fmt(float(m[1])*(1+.05*tier))+'%'+(m[2] or '')+(m[3] or '')+(m[4] or '')+' ATK', text, flags=re.I)
    if source_id in (1,2):
        text = text.replace('2 Ignites effects (+1 level)', f'{2+tier} Ignites effects')
    if source_id == 24:
        text = text.replace('7% for 3 turns (+7% per level)', f'{7+7*tier}% for 3 turns')
    if source_id == 12:
        text = text.replace('Restores 50% HP', f'Restores {fmt(50*(1+.05*tier))}% HP')
    return text

characters = []
for row_index, raw in enumerate(sources['characters']['rows'], start=2):
    sid = int(raw['Id'])
    year = re.search(r'\d{2}$', raw['Name'])[0]
    fid = 'fighter.' + re.sub('[^a-z0-9]', '', raw['CodeName'].lower()) + year
    stats = {}
    for i, key in enumerate(STAT_FIELDS):
        val = raw[key].strip()
        missing = val.lower() in ('', 'nan', 'na')
        stats[key] = {'value': GENERATED[sid][i] if missing else float(val),
                      'unit': 'points' if i < 4 else 'percent',
                      'provenance': 'generated-starting-value' if missing else 'source',
                      'sourceField': key}
    for key, value in [('Avoidance_Rate',0),('Evade_Rate',0),('Control_Rate',100),('Perception_Rate',100)]:
        stats[key] = {'value':value,'unit':'percent','provenance':'guide-page-6-default'}
    cards = []
    for slot in (1,2):
        prefix = f'Card_{slot}_'
        desc = raw[prefix+'Description']
        cards.append({'slot':slot, 'sourceTarget':raw[prefix+'Target'], 'sourceType':raw[prefix+'Type'],
                      'sourceEffectTags':raw[prefix+'Effect'],
                      'ranks':[{'rank':rank,'description':rank_text(desc,rank),
                                'provenance':'source-rank3' if rank==3 else 'generated-starting-value'} for rank in (1,2,3)]})
    characters.append({'definitionId':fid,'sourceId':sid,'sourceRecord':row_index,
      'name':raw['Name'],'familyId':raw['CodeName'], 'rarity':raw['Rarity'],
      'attribute':raw['Attribute'].split()[0],
      'traits':[x.strip().replace('Sarced Treasure','Sacred Treasure') for x in raw['Trait'].split(',')],
      'role':raw['Role'],'stats':stats,'passive':{'sourceType':raw['Passive-Type'], 'description':raw['Passive-Effect'], 'restriction':raw['Passive-Restriction']},
      'cards':cards,'constellations':[{'tier':c,'label':f'C{c}/6','description':ult_text(raw['Card_Ultimate_Description'],c,sid),
          'provenance':'source-base-normalized-to-C0' if c==0 else 'generated-starting-value-with-source-riders'} for c in range(7)],
      'holyRelic':{'description':None if raw['Holy_Relic'].strip().lower() in ('nan','na','') else raw['Holy_Relic'].strip(),'enabledInPrototype':False},
      'reviewNotes':ISSUES.get(sid,[]),'runtimeReady':False})

catalog = {'schemaVersion':'authoring-draft-1','runtimeReady':False,
 'policy':'Review-only descriptions, not executable AST. Source rank3 preserved. All generated values are starting values.',
 'characters':characters}
(OUT/'wip-character-drafts.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2,allow_nan=False),encoding='utf-8')

lines = ['---','slug: fighting-allstar-character-roster','status: needs-human','gdd_tags: [roster, tuning]','owner: codex','human_checkpoint: required','next_agent: human','blocked_by: []','---',
 '# Fighting Allstar — actual WIP character drafts','',
 '29 source characters. Source IDs are CSV IDs, not legacy Unity asset IDs. Every skill listing in the guide is Rank 3 (PDF p.7). Original spellings/text remain in [source-catalog.json](source-catalog.json). [Structured drafts](wip-character-drafts.json) preserve provenance per stat and rank. These are authoring drafts, not runtime definitions.','',
 '## Generation rules and test gates','',
 '- **Starting values:** R1/R2 percentage magnitudes are one-third/two-thirds of R3, rounded to two decimals. Integer turn/gauge/Ignite counts use ceiling with minimum one. Non-numeric effects remain available; keyword multipliers (Weakpoint ×3, etc.) do not scale with card rank. R3 stays verbatim. This implements the guide’s approximate one-third suggestion; it is not a claim of exact 7DSGC scaling.',
 '- **Starting values:** C0 is the source ultimate baseline; C1–C6 add 5% of baseline ATK-scaled ultimate damage/healing per tier, non-compounding; Kensou’s primary percent-HP ultimate heal follows the same scaling. Other effects remain unchanged except explicit Kyo +1 Ignite/tier and Robert +7 percentage points/tier. There are seven states and six upgrades. No invented C6 passive unlocks.',
 '- **Starting values:** missing stats use explicit role profiles for Shingo97, Andy94, Brian94, Lucky94 and Heavy D94. Existing numbers, including surprising CC values and identical Joe variants, are preserved. Hidden stats use guide p.6 defaults.',
 '- Microtests: compare all ranks at equal card/action investment; R3 must improve payload without making R1 useless. If move/merge always wins, reduce R2/R3 damage or effect duration. If generated low-rank crowd control dominates, gate it to R2 rather than silently altering the source R3.',
 '- Run 100 paired seeds per intended matchup, swapping first side: initial soft-counter target 55–65%, all-round team target at most 60%. These are diagnostic starting targets, not measured results. If rarity/stats dominate, narrow generated stats first and separately evaluate source stat normalization.',
 '- Compare C0 and C6 mixed teams; reject progression that removes access to a counter at C0. If Kyo’s eight Ignites overwhelms defense, tune the proposed stack cap/mitigation cap before adding new power.',
 '- All Holy Relics are retained but disabled for the first prototype. Missing relic means unspecified, not permission to invent a permanent bonus. Character-specific ambiguities below must become explicit typed effects before that character enters a runtime allowlist.','',
 '## Roster index','', '| CSV ID | Character | Rarity | Attribute | ATK / DEF / HP | Stats |','| --- | --- | --- | --- | --- | --- |']
for c in characters:
    s=c['stats']; generated=any(x['provenance']=='generated-starting-value' for x in s.values())
    lines.append(f"| {c['sourceId']} | {c['name']} | {c['rarity']} | {c['attribute']} | {fmt(s['Attack']['value'])} / {fmt(s['Defense']['value'])} / {fmt(s['Health']['value'])} | {'Generated starting values' if generated else 'Source'} |")
for c in characters:
    lines += ['',f"## {c['sourceId']}. {c['name']}",'',f"`{c['definitionId']}` · {c['rarity']} · {c['attribute']} · {', '.join(c['traits'])}",'',
              f"**Passive ({c['passive']['sourceType']}; {c['passive']['restriction']}):** {c['passive']['description']}",'',
              '**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): '+ '; '.join(f"{k}={fmt(v['value'])}" for k,v in c['stats'].items()),'']
    for card in c['cards']:
        lines += [f"### Skill {card['slot']} — {card['sourceTarget']} / {card['sourceType']}",'']
        for r in card['ranks']:
            lines += [f"- **R{r['rank']} ({'source' if r['rank']==3 else 'starting value'}):** {r['description'].replace(chr(10),' ')}"]
    lines += ['','### Ultimate constellation sequence','']
    for u in c['constellations']:
        lines += [f"- **{u['label']}:** {u['description'].replace(chr(10),' ')}"]
    lines += ['', '**Holy Relic (disabled):** '+(c['holyRelic']['description'] or 'Not supplied.'),'']
    lines += ['**Review:** '+n for n in c['reviewNotes']]
(OUT/'character-roster.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
assert len(characters)==29 and len({c['definitionId'] for c in characters})==29
assert all(len(c['cards'])==2 and all(len(x['ranks'])==3 for x in c['cards']) and len(c['constellations'])==7 for c in characters)
assert all(c['cards'][i]['ranks'][2]['description']==sources['characters']['rows'][j][f'Card_{i+1}_Description'].strip() for j,c in enumerate(characters) for i in range(2))
print(json.dumps({'characters':len(characters),'regularRankEntries':174,'constellationEntries':203,'generatedStatBlocks':len(GENERATED),'attackEffects':len(sources['attackEffects']['rows']),'debuffEffects':len(sources['debuffEffects']['rows']),'statDefinitions':len(sources['stats']['rows'])}))
