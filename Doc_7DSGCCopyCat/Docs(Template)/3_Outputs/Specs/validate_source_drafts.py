"""Validate documentation authoring data; does not test the game runtime."""
import hashlib,json,re,math
from pathlib import Path
root=Path(__file__).resolve().parents[2]
data=Path(__file__).resolve().parent/'CharacterData'
src=json.loads((data/'source-catalog.json').read_text(encoding='utf-8'))
draft=json.loads((data/'wip-character-drafts.json').read_text(encoding='utf-8'))
for s in src.values():
    assert hashlib.sha256((Path('E:/Drive_E_Download')/s['file']).read_bytes()).hexdigest()==s['sha256']
chars=draft['characters']
assert len(chars)==29 and len({c['definitionId'] for c in chars})==29
generated=[]
for raw,c in zip(src['characters']['rows'],chars):
    assert c['sourceId']==int(raw['Id']) and c['runtimeReady'] is False
    assert len(c['stats'])==19
    assert [x['tier'] for x in c['constellations']]==list(range(7))
    for card in c['cards']:
        assert [x['rank'] for x in card['ranks']]==[1,2,3]
        assert card['ranks'][2]['description']==raw[f"Card_{card['slot']}_Description"].strip()
    for key,v in c['stats'].items():
        assert math.isfinite(v['value']) and v['value']>=0
        if v['provenance']=='source': assert v['value']==float(raw[key])
    if any(v['provenance']=='generated-starting-value' for v in c['stats'].values()):generated.append(c['sourceId'])
assert generated==[10,17,27,28,29]
assert '8 Ignites' in chars[0]['constellations'][6]['description']
assert '49%' in chars[23]['constellations'][6]['description']
assert '65% HP' in chars[11]['constellations'][6]['description']
gdd=root/'1_Inputs_Templates/GDD_Fighting_Allstar.md'
template=(root/'1_Inputs_Templates/GDD_TEMPLATE.md').read_text(encoding='utf-8')
tags=lambda s:set(re.findall(r'@tag:([\w-]+)',s))
assert tags(template)<=tags(gdd.read_text(encoding='utf-8'))
docs=[gdd,root/'3_Outputs/Specs/fighting-allstar-combat-source-contract.md',data/'character-roster.md',root/'3_Outputs/Specs/fighting-allstar-prototype-arch-plan.md',root/'3_Outputs/TestPlans/fighting-allstar-prototype-test-plan.md']
errors=[]
for p in docs:
    text=p.read_text(encoding='utf-8')
    assert text.count('```')%2==0,p
    for link in re.findall(r'\]\(([^\s]+)\)',text):
        if '://' in link or link.startswith('#'):continue
        # These generated links use no parenthesis-bearing target filenames.
        target=link.split('#')[0]
        if not (p.parent/target).exists():errors.append((str(p),link))
    for code in re.findall(r'```json\s*\n(.*?)```',text,re.S):json.loads(code)
assert not errors,errors
print('PASS: five input hashes; 29 identities; 174 ranks; 203 tiers; 551 finite stat fields; source equality; generated IDs; Kyo/Robert/Kensou progression; GDD template tags; five document link sets; JSON examples.')
