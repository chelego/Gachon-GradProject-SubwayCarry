#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabPhonePublication
    {
        public static string LastStatus {get;private set;}="Normal 연결 없음 · 원본/출력만 저장";
        public static AnimationLabPhoneMotion[] Targets(Motion source)=>AssetDatabase.FindAssets("t:AnimationLabPhoneMotion",new[]{AnimationLabMotionAuthoring.Root+"/Data"}).Select(g=>AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AssetDatabase.GUIDToAssetPath(g))).Where(b=>b!=null&&(b.sourceId==source.id||b.FindSource(source.id)!=null||b.FindSqueezeSource(source.id)!=null)).ToArray();
        public static void Validate(Motion source,AnimationClip clip,AnimationLabPhoneMotion[] targets)
        {
            foreach(var target in targets)
            {
                var squeeze=target.FindSqueezeSource(source.id);
                if(squeeze!=null)
                {
                    if(target.rig!=source.sourceRig||clip==null||clip.isHumanMotion||clip.length<=0)throw new InvalidOperationException("Generic squeeze rig/clip mismatch");
                    if(source.loop!=(squeeze.phase==1))throw new InvalidOperationException("옆걸음 유지에만 반복 설정을 사용하세요.");
                    if(source.loop){var a=source.Evaluate(0,false);var b=source.Evaluate(source.lastFrame,false);for(int j=1;j<source.paths.Length;j++)if(Quaternion.Angle(a.rotations[j],b.rotations[j])>1)throw new InvalidOperationException("옆걸음 반복 경계 불일치: "+source.paths[j]);}
                    continue;
                }
                var entry=target.FindSource(source.id);var paths=entry!=null&&AnimationLabPhoneMotion.BothArms(entry.role)?target.bothArmPaths:target.upperPaths;
                if(target.actions.Count(e=>e!=null&&e.sourceId==source.id)>1)throw new InvalidOperationException("같은 제작 원본이 여러 동작 역할에 중복 연결됐습니다.");
                if(entry!=null)
                {
                    bool repeat=entry.role==AnimationLabPhoneAction.CarryLoop||entry.role==AnimationLabPhoneAction.LandscapeHold;
                    if(source.loop!=repeat)throw new InvalidOperationException(repeat?"이 유지 동작은 반복 설정이 필요합니다.":"이 전환 동작은 반복을 해제하세요.");
                    if(entry.contactFrame>=source.lastFrame||entry.carryFrame>source.lastFrame)throw new InvalidOperationException("주머니 접촉/전환 기준 프레임이 클립 범위를 벗어납니다.");
                }
                if((entry==null&&target.authored!=null&&target.authored.id!=source.id)||target.rig!=source.sourceRig)throw new InvalidOperationException("Normal 대상의 원본/리그 연결 불일치");
                if(paths==null||paths.Length==0||paths.Any(p=>!source.paths.Contains(p)))throw new InvalidOperationException("Normal 상체 마스크 경로 불일치");
                string hips=source.humanPaths[(int)HumanBodyBones.Hips];if(paths.Any(p=>p.Length==0||p==hips||p.Contains("thigh")||p.Contains("shin")||p.Contains("foot")))throw new InvalidOperationException("상체 동작에 ROOT/골반/다리 마스크가 포함됨");
                if(clip==null||clip.isHumanMotion||clip.length<=0)throw new InvalidOperationException("Normal 상체 대상은 검증된 Generic 클립이어야 합니다.");
                if(source.loop){var first=source.Evaluate(0,false);var last=source.Evaluate(source.lastFrame,false);foreach(string path in paths){int joint=Array.IndexOf(source.paths,path);if(Quaternion.Angle(first.rotations[joint],last.rotations[joint])>1)throw new InvalidOperationException("상체 반복 경계가 맞지 않습니다. 처음/마지막 키를 확인하세요: "+path);}}
            }
        }
        public static void Publish(Motion source,AnimationClip clip,AnimationLabPhoneMotion[] targets)
        {
            LastStatus=targets.Length==0?"Normal 연결 없음 · 원본/출력만 저장":"Normal 적용 완료 · 다음 Play에서 수정본 재생 (상체 마스크)";
            foreach(var target in targets)
            {
                var squeeze=target.FindSqueezeSource(source.id);
                if(squeeze!=null){squeeze.authored=source;squeeze.clip=clip;using(var squeezeHash=SHA256.Create())squeeze.revision=BitConverter.ToString(squeezeHash.ComputeHash(File.ReadAllBytes(AssetDatabase.GetAssetPath(clip)))).Replace("-","").Substring(0,12);LastStatus="사람 사이 통과 · 다음 Play에 수정본 적용";EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);continue;}
                var entry=target.FindSource(source.id);if(entry==null){target.authored=source;target.upperBody=clip;target.upperLoop=source.loop;target.displayName=source.clipName.Replace("_"," · ");}else{entry.authored=source;entry.clip=clip;entry.loop=source.loop;entry.displayName=source.clipName.Replace("_"," · ");}
                using var hash=SHA256.Create();var revision=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(AssetDatabase.GetAssetPath(clip)))).Replace("-","").Substring(0,12);if(entry==null)target.publishedRevision=revision;else entry.revision=revision;
                EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);
            }
        }
        public static Motion OwnedSource(UnityEngine.Object value,out bool owned)
        {
            owned=false;if(value is Motion motion){owned=true;return motion;}string path=AssetDatabase.GetAssetPath(value);var importer=AssetImporter.GetAtPath(path);const string prefix="SubwayCarry.MotionAuthoring:";
            if(importer==null||!importer.userData.StartsWith(prefix,StringComparison.Ordinal))return null;owned=true;string id=importer.userData.Substring(prefix.Length);
            var matches=AssetDatabase.FindAssets("t:AnimationLabAuthoredMotion",new[]{AnimationLabMotionAuthoring.Authored}).Select(g=>AssetDatabase.LoadAssetAtPath<Motion>(AssetDatabase.GUIDToAssetPath(g))).Where(m=>m!=null&&m.id==id).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException(matches.Length==0?"대응하는 편집 원본이 없습니다. Authored 원본과 .meta를 확인하세요.":"같은 제작 ID의 편집 원본이 여러 개입니다. 임의로 열지 않습니다.");
            var source=matches[0];if(AssetDatabase.GetAssetPath(source.generatedClip)!=path&&AssetDatabase.GetAssetPath(source.generatedFbx)!=path)throw new InvalidOperationException("출력 소유 ID와 명시 원본 참조가 다릅니다. 원본 출력은 변경하지 않았습니다.");return source;
        }
    }
}
#endif
