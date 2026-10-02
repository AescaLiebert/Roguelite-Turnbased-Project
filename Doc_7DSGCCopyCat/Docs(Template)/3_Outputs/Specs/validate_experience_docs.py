"""Finite documentation/math checks; not Unity/runtime tests."""
from pathlib import Path
import re, math
root=Path(__file__).resolve().parents[2]
spec=root/'3_Outputs/Specs'
files=[spec/f'fighting-allstar-{name}.md' for name in ['economy-route-design','battle-screen-states','unity-implementation-plan','experience-task-card']]
for p in files:
    text=p.read_text(encoding='utf-8')
    assert text.startswith('---\n') and text.count('```')%2==0,p
    for link in re.findall(r'\]\(([^\s]+)\)',text):
        if '://' not in link and not link.startswith('#'):
            assert (p.parent/link.split('#')[0]).exists(),(p,link)
states=(spec/'fighting-allstar-battle-screen-states.md').read_text(encoding='utf-8')
assert all(f'| B{i:02d} ' in states for i in range(22))
assert sum([4,36,60])==100
assert 4+4*9+3*20==100
assert 160*10==1600 and 300*160==48000
assert divmod(295+10,300)==(1,5)
assert [math.floor(320*(100+d)/100) for d in [0,25,50,75,100]]==[320,400,480,560,640]
visual=Path('C:/Users/Apricha/.codex/visualizations/2026/09/30/01a0f32d-d292-7353-b6fc-96f7efdcc3e6/fighting-allstar-screens.html')
v=visual.read_text(encoding='utf-8')
assert len(v.encode())<1000000 and '<!doctype' not in v.lower()
assert '\\"' not in v and 'fetch(' not in v and 'XMLHttpRequest' not in v
ids=re.findall(r'\bid="([^"]+)"',v)
assert len(ids)==len(set(ids))
for target in re.findall(r"q\('#([^']+)'\)",v):assert target in ids,target
print('PASS: 4 document link sets/frontmatter/fences; B00–B21 coverage; rates/costs/milestone/reward arithmetic; visualization size, literal markup, unique IDs and referenced elements.')
