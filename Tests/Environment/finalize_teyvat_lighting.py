"""WP08 artifact QA, dependency inventory and reproduction bundle."""
from pathlib import Path
import hashlib,json,re,shutil,tarfile,zipfile
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Artifacts/TeyvatGrassland/WP08/Revision02'
ART='Assets/Project/Art/Environment/TeyvatGrassland/Lighting';SCENES='Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def copy_back():
    host=ROOT/'.utmp/TeyvatWorldLab';files=[]
    for folder in (ART,SCENES+'/TG_LightingReview_r2'):
        files += [p for p in (host/folder).rglob('*') if p.is_file()]
        side=host/(folder+'.meta')
        if side.exists():files.append(side)
    for f in (SCENES+'/TG_LightingReview_r2.unity',):files += [host/f,host/(f+'.meta')]
    files += list((host/'Assets/Project/Editor').glob('TeyvatLighting*.cs.meta'))
    for src in files:
        dst=ROOT/src.relative_to(host);dst.parent.mkdir(parents=True,exist_ok=True)
        if not dst.exists() or sha(src)!=sha(dst):shutil.copy2(src,dst)
    print(json.dumps({'synchronizedLightingFiles':len(files)}))
def images():
    from PIL import Image
    import numpy as np
    results=[]
    for folder in ('ReferenceDay','Neutral','Beauty','ActualAura','RockTexture'):
        for warm in sorted((OUT/folder).glob('*.png')):
            a=np.asarray(Image.open(warm).convert('RGB'),dtype=np.float32);cold=OUT/'Reload'/folder/warm.name
            record={'folder':folder,'camera':warm.stem,'brightWhiteFraction':float(np.mean(np.min(a,axis=2)>250)),'nearBlackFraction':float(np.mean(np.max(a,axis=2)<3)),'pinkCandidateFraction':float(np.mean((a[:,:,0]>210)&(a[:,:,1]<80)&(a[:,:,2]>210)))}
            if cold.exists():
                b=np.asarray(Image.open(cold).convert('RGB'),dtype=np.float32);record.update(coldCompared=True,identicalPixels=bool(np.array_equal(a,b)),meanAbsoluteRGBDifference=float(np.abs(a-b).mean()))
            results.append(record)
    ablations=[]
    for ablation,baseline in [('NoDistanceHaze','ReferenceDay/TG_EnvironmentOverview'),('NoBloom','ReferenceDay/TG_PlayerAttack'),('NoSunShadows','ReferenceDay/TG_PlayerPlanning'),('AuraNoBloom','ActualAura/TG_PlayerAttack'),('AuraNoPostProcess','ActualAura/TG_PlayerAttack'),('ActorsNoProbes','ReferenceDay/TG_PlayerAttack')]:
        p=OUT/'Ablations'/(ablation+'.png');q=OUT/(baseline+'.png')
        if p.exists() and q.exists():
            a=np.asarray(Image.open(p).convert('RGB'),dtype=np.float32);b=np.asarray(Image.open(q).convert('RGB'),dtype=np.float32);d=np.abs(a-b)
            ablations.append({'ablation':ablation,'baseline':baseline,'meanAbsoluteRGBDifference':float(d.mean()),'changedPixelFraction':float(np.mean(np.max(d,axis=2)>1))})
    rock=[]
    for camera in ('TG_WaterSample','TG_EnvironmentOverview'):
        a=np.asarray(Image.open(OUT/'RockTexture'/(camera+'_PaintedLit.png')).convert('RGB'),dtype=np.float32);b=np.asarray(Image.open(OUT/'RockTexture'/(camera+'_FlatTintLit.png')).convert('RGB'),dtype=np.float32);d=np.abs(a-b)
        rock.append({'camera':camera,'meanAbsoluteRGBDifference':float(d.mean()),'changedPixelFraction':float(np.mean(np.max(d,axis=2)>1)),'renderedTextureDifference':bool(d.mean()>.05)})
    report={'images':results,'ablations':ablations,'rockTexture':rock,'method':'Pixel QA accompanies visual review. RGB white/black/pink fractions are diagnostics, not proof of aesthetic parity or shader correctness. Rock on/off comparison uses .05 meanRGB as a starting presence tolerance, not a quality score. Ablation differences demonstrate output changes, not a causal GPU timing measurement.'}
    (OUT/'ImageReview.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'images':len(results),'ablations':ablations}))
def bundle():
    before=read(OUT/'PreservationBefore.json');checks=[{'path':i['path'],'before':i['sha256'],'after':sha(Path(i['path']))} for i in before]
    preservation={'passed':all(i['before']==i['after'] for i in checks),'files':len(checks),'checks':checks};(OUT/'PreservationCheck.json').write_text(json.dumps(preservation,indent=2)+'\n');assert preservation['passed'],'Protected inputs changed'
    for folder in (OUT,OUT/'Reload'):
        assert(folder/'COMPLETE.txt').exists() and not(folder/'FAILED.txt').exists()
        validation=read(folder/'LightingValidation.json');assert validation['passed'] and validation['legacyProbes'] and validation['reflectionTexture'] and validation['referenceRockTexture']
        assert read(folder/'PostProcess/PixelValidation.json')['passed']
    for name in ('ProbeBake','ReflectionBake'):assert read(OUT/(name+'.json'))['passed']
    assert(OUT/'Playback/COMPLETE.txt').exists() and not(OUT/'Playback/FAILED.txt').exists()
    playback=read(OUT/'Playback/PlaybackValidation.json');assert playback['passed'] and playback['reflectionTexture']
    assert read(OUT/'ReferenceRockBindings.json')['bindingsPresent']
    assert all(x['renderedTextureDifference'] for x in read(OUT/'ImageReview.json')['rockTexture'])
    package=OUT/'TG_WP08_Lighting.unitypackage'
    payload_checks=[]
    with tarfile.open(package,'r:gz') as t:
        paths=[]
        for m in t.getmembers():
            if not m.name.endswith('/pathname'):continue
            path=t.extractfile(m).read().decode();paths.append(path)
            payload=t.extractfile(m.name.rsplit('/',1)[0]+'/asset').read()
            digest=hashlib.sha256(payload).hexdigest();assert digest==sha(ROOT/path),f'Package differs from synchronized source: {path}'
            meta=t.extractfile(m.name.rsplit('/',1)[0]+'/asset.meta').read()
            assert hashlib.sha256(meta).hexdigest()==sha(ROOT/(path+'.meta')),f'Package metadata differs: {path}'
            payload_checks.append({'path':path,'sha256':digest})
    assert not any(p.startswith(('ProjectSettings/','Packages/')) or p.endswith('/Battle.unity') for p in paths)
    for suffix in ('TG_LitBattleStage_r2.prefab','TG_LightingReview_r2.unity','TG_ReferenceDay.asset','TG_LightingURP.asset','TeyvatLightingRig.cs','TeyvatLightingProfile.cs','TG_PaintedSky.shader','TG_LightingTerrain.shader','TG_Common.hlsl','TG_ReferenceRock.hlsl','TG_RockPainted.png','LICENSE.md','TG_ArenaReflection.exr','LightingData.asset'):
        assert any(p.endswith(suffix) for p in paths),suffix
    includes=[]
    for path in paths:
        p=ROOT/path
        if p.suffix not in ('.shader','.hlsl'):continue
        for include in re.findall(r'#include\s+"([^"]+)"',p.read_text(encoding='utf-8-sig')):
            if include.startswith('Packages/'):continue
            target=(p.parent/include).resolve()
            if not target.is_file():continue # Unity built-in includes resolved by editor
            relative=target.relative_to(ROOT).as_posix();includes.append(relative);assert relative in paths,relative
    (OUT/'PackageContents.json').write_text(json.dumps({'assets':len(paths),'bytes':package.stat().st_size,'sha256':sha(package),'localIncludesChecked':len(includes),'allPayloadsMatchSynchronizedSource':True,'payloads':payload_checks,'paths':sorted(paths)},indent=2)+'\n')
    source=[p for p in (ROOT/ART).rglob('*') if p.is_file() and p.suffix in ('.cs','.shader','.hlsl','.md')]
    source += list((ROOT/'Assets/Project/Editor').glob('TeyvatLighting*.cs'))
    source += [ROOT/'Tests/Environment/RunTeyvatLightingLab.ps1',Path(__file__),ROOT/'ArtDirection/Level Design/TeyvatGrasslandConcepts/WP08_Lighting.md']
    source += [ROOT/'ArtDirection/Level Design/TeyvatGrasslandConcepts'/f for f in ('WORK_PACKAGES.md','ASSET_CONTRACT.md')]
    source += [ROOT/'Assets/Project/Runtime/Presentation/Combat'/f for f in ('UltimateReadyAura.cs','ActorVisualEffectGeometry.cs')]
    source += [ROOT/'Assets/Resources'/f for f in ('UltimateReadyAura.shader','ActorVisualEffects.shader')]
    zip_path=OUT/'TG_WP08_EditableSource.zip'
    with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED,6) as z:
        for p in sorted(set(source)):z.write(p,p.relative_to(ROOT).as_posix())
    owned=[p for p in (ROOT/ART).rglob('*') if p.is_file()]+list((ROOT/'Assets/Project/Editor').glob('TeyvatLighting*'))+[ROOT/(ART+'.meta'),ROOT/SCENES/'TG_LightingReview_r2.unity',ROOT/SCENES/'TG_LightingReview_r2.unity.meta',ROOT/SCENES/'TG_LightingReview_r2.meta']+[p for p in (ROOT/SCENES/'TG_LightingReview_r2').rglob('*') if p.is_file()]+source
    (OUT/'HandoffManifest.json').write_text(json.dumps({'package':'WP08 reference daylight lighting r2','date':'2026-10-19','renderer':'Unity6000.3.4f1 / URP17.3 Forward / Linear','protectedFiles':len(checks),'sourceFiles':len(set(source)),'sourceBytes':zip_path.stat().st_size,'sourceSHA256':sha(zip_path),'owned':[{'path':p.relative_to(ROOT).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(set(owned))]},indent=2)+'\n')
    print(json.dumps({'passed':True,'protectedFiles':len(checks),'packageAssets':len(paths),'packageBytes':package.stat().st_size,'sourceFiles':len(set(source)),'sourceBytes':zip_path.stat().st_size}))
def motion():
    import imageio.v2 as imageio
    import numpy as np
    from PIL import Image
    reports=[]
    for name in ('Water','Tour'):
        files=sorted((OUT/'Playback'/name).glob('Frame*.png'));assert files
        frames=[Image.open(p).convert('RGB') for p in files];mp4=OUT/('TG_WP08_'+name+'PlayMode.mp4')
        with imageio.get_writer(mp4,fps=6,codec='libx264',quality=8,macro_block_size=2) as writer:
            for frame in frames:writer.append_data(np.asarray(frame))
        gif=mp4.with_suffix('.gif');frames[0].save(gif,save_all=True,append_images=frames[1:],duration=167,loop=0,optimize=False)
        reports.append({'name':name,'frames':len(files),'encodedFPS':6,'mp4Bytes':mp4.stat().st_size,'gifBytes':gif.stat().st_size,'runtimeFPS':'not measured'})
        for frame in frames:frame.close()
    (OUT/'MotionInventory.json').write_text(json.dumps(reports,indent=2)+'\n');print(json.dumps(reports))
if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser();p.add_argument('operation',choices=('copy-back','images','bundle','motion'));args=p.parse_args();{'copy-back':copy_back,'images':images,'bundle':bundle,'motion':motion}[args.operation]()
