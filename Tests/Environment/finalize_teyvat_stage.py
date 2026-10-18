"""Inventory and package the verified WP07 stage; no Unity settings mutation."""
from pathlib import Path
import hashlib,json,shutil,tarfile,zipfile
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Artifacts/TeyvatGrassland/WP07/Revision01'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def copy_back():
    host=ROOT/'.utmp/TeyvatWorldLab'
    folders=['Assets/Project/Art/Environment/TeyvatGrassland/Integration']
    singles=['Assets/Project/Art/Environment/TeyvatGrassland/Prefabs/TG_BattleStage_r1.prefab','Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_StageReview_r1.unity']
    files=[]
    for folder in folders:files += [p for p in (host/folder).rglob('*') if p.is_file()]
    for f in singles:files += [host/f,host/(f+'.meta')]
    files += list((host/'Assets/Project/Editor').glob('TeyvatStage*.cs.meta'))
    for folder in folders+['Assets/Project/Art/Environment/TeyvatGrassland/Prefabs','Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland']:
        p=host/(folder+'.meta')
        if p.exists():files.append(p)
    for src in files:
        dst=ROOT/src.relative_to(host);dst.parent.mkdir(parents=True,exist_ok=True)
        if not dst.exists() or sha(src)!=sha(dst):shutil.copy2(src,dst)
    print(json.dumps({'synchronizedStageFiles':len(files)}))
def bundle():
    from PIL import Image
    import numpy as np
    before=read(OUT/'PreservationBefore.json');checks=[{'path':i['path'],'before':i['sha256'],'after':sha(Path(i['path']))} for i in before]
    preservation={'passed':all(i['before']==i['after'] for i in checks),'files':len(checks),'checks':checks}
    (OUT/'PreservationCheck.json').write_text(json.dumps(preservation,indent=2)+'\n');assert preservation['passed'],'Protected prior work changed'
    for folder in (OUT,OUT/'Reload'):
        assert(folder/'COMPLETE.txt').exists() and not(folder/'FAILED.txt').exists(),str(folder)
        for name in ('StageValidation.json','PrefabValidation.json'):assert read(folder/name)['passed'],name
        for name in ('Perspective','Orthographic'):assert read(folder/name/'WaterDepthValidation.json')['passed']
    assert read(OUT/'CameraSweep.json')['passed']
    assert read(OUT/'Playback/PlaybackValidation.json')['passed']
    comparisons=[]
    for warm in sorted((OUT/'Afternoon').glob('*.png')):
        cold=OUT/'Reload/Afternoon'/warm.name
        assert cold.exists(),warm.name
        a=np.asarray(Image.open(warm)).astype(np.float32);b=np.asarray(Image.open(cold)).astype(np.float32)
        comparisons.append({'camera':warm.stem,'identicalPixels':bool(np.array_equal(a,b)),'meanAbsoluteRGBDifference':float(np.abs(a-b).mean())})
    (OUT/'VisualReloadComparison.json').write_text(json.dumps({'compared':len(comparisons),'exactlyIdentical':sum(i['identicalPixels'] for i in comparisons),'comparisons':comparisons},indent=2)+'\n')
    package=OUT/'TG_WP07_IntegratedStage.unitypackage'
    with tarfile.open(package,'r:gz') as t:paths=[t.extractfile(m).read().decode() for m in t.getmembers() if m.name.endswith('/pathname')]
    assert not any(p.startswith(('ProjectSettings/','Packages/')) or p.endswith('/Battle.unity') for p in paths)
    for suffix in ('TG_BattleStage_r1.prefab','TG_StageReview_r1.unity','TG_StageMeadowPlacement.asset','TG_StageLandscape.asset','TeyvatStageBinding.cs','TeyvatStageTour.cs','TeyvatMaterialGlobals.cs','TeyvatMeadowRenderer.cs','TG_IntegratedWater.shader','TG_ImpactSpray.shader','TG_ImpactFoam.shader','TG_Common.hlsl','LICENSE.md'):
        assert any(p.endswith(suffix) for p in paths),suffix
    (OUT/'PackageContents.json').write_text(json.dumps({'assets':len(paths),'bytes':package.stat().st_size,'sha256':sha(package),'paths':sorted(paths)},indent=2)+'\n')
    source=ROOT/'ArtSource/TeyvatGrassland/Integration'
    files=[p for p in source.rglob('*') if p.is_file() and p.suffix not in ('.blend1','.pyc') and '__pycache__' not in p.parts]
    files+=list((ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/Integration/Shaders').glob('*.*'))
    files+=list((ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/Integration/Runtime').glob('*.cs'))
    files+=list((ROOT/'Assets/Project/Editor').glob('TeyvatStage*.cs'))
    files+=[ROOT/'Tests/Environment/RunTeyvatStageLab.ps1',Path(__file__),ROOT/'ArtDirection/TeyvatGrasslandConcepts/WP07_Integration.md',ROOT/'ArtSource/TeyvatGrassland/LayoutR3/TG_LayoutGeometry.blend',ROOT/'ArtSource/TeyvatGrassland/LayoutR3/TG_LayoutPlan.json',ROOT/'ArtSource/TeyvatGrassland/Shared/create_material_samples.py']
    files=sorted(set(p for p in files if p.suffix!='.meta'))
    bundle=OUT/'TG_WP07_EditableSource.zip'
    with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED,6) as z:
        for p in files:z.write(p,p.relative_to(ROOT).as_posix())
    owned=files+[p for p in (ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/Integration').rglob('*') if p.is_file()]
    owned+=[ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/Prefabs/TG_BattleStage_r1.prefab',ROOT/'Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_StageReview_r1.unity']
    (OUT/'HandoffManifest.json').write_text(json.dumps({'package':'WP07 integrated stage r1','target':'Windows PC; final lighting/actual combat/player performance pending','protectedFiles':len(checks),'sourceFiles':len(files),'sourceBytes':bundle.stat().st_size,'sourceSHA256':sha(bundle),'owned':[{'path':p.relative_to(ROOT).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(set(owned))]},indent=2)+'\n')
    print(json.dumps({'passed':True,'protectedFiles':len(checks),'packageAssets':len(paths),'packageBytes':package.stat().st_size,'sourceFiles':len(files),'sourceBytes':bundle.stat().st_size}))
def motion():
    import imageio.v2 as imageio
    from PIL import Image
    import numpy as np
    out=[]
    for directory,name,fps in ((OUT/'Flow','TG_WaterfallMotion',15),(OUT/'Playback/Water','TG_WaterfallPlayMode',6),(OUT/'Playback/Tour','TG_StageTourPlayMode',6)):
        files=sorted(directory.glob('Frame*.png'))
        if not files:continue
        frames=[Image.open(p).convert('RGB') for p in files]
        gif=OUT/(name+'.gif');frames[0].save(gif,save_all=True,append_images=frames[1:],duration=round(1000/fps),loop=0,optimize=False)
        mp4=OUT/(name+'.mp4')
        with imageio.get_writer(mp4,fps=fps,codec='libx264',quality=8,macro_block_size=2) as writer:
            for f in frames:writer.append_data(np.asarray(f))
        pixels=np.array([np.asarray(f,dtype=np.float32) for f in frames])
        out.append({'name':name,'frames':len(files),'encodedFramesPerSecond':fps,'meanAdjacentRGBDifference':float(np.abs(pixels[1:]-pixels[:-1]).mean()),'gifBytes':gif.stat().st_size,'mp4Bytes':mp4.stat().st_size})
        for f in frames:f.close()
    (OUT/'MotionInventory.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out))
if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser();parser.add_argument('operation',choices=('copy-back','bundle','motion'));args=parser.parse_args()
    {'copy-back':copy_back,'bundle':bundle,'motion':motion}[args.operation]()
