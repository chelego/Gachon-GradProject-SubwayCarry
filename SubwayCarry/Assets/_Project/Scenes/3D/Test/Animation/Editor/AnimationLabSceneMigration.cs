#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // One explicitly requested migration. Never opens/saves/closes a user's scene or changes Play.
    [InitializeOnLoad]
    public static class AnimationLabSceneMigration
    {
        const string Root=AnimationLabBuilder.Root;
        const string Request=Root+"/Source~/NormalSituations.request";
        const string Archive=Root+"/Source~/BeforeNormalSituations.zip";
        const string WalkGuid="f5f49d8bf1636ed4ab0b94582e88c700";
        static readonly string[] Removed={"Run","Sit","Lean","Stairs"};
        static double next;
        static string lastReason;
        static AnimationLabSceneMigration(){EditorApplication.update+=TryApply;}
        static void Wait(string reason){if(reason==lastReason)return;lastReason=reason;Debug.Log("NORMAL_SITUATIONS_PENDING: "+reason);}
        static string Hash(Stream stream){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(stream));}
        static bool Matches(ZipArchive zip,string filename)
        {
            var entry=zip.GetEntry(filename);if(entry==null)return false;
            using var archived=entry.Open();using var current=File.OpenRead(Root+"/"+filename);return Hash(archived)==Hash(current);
        }
        static void TryApply()
        {
            if(Application.isBatchMode||!File.Exists(Request)||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+3;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){Wait("컴파일/임포트/Play 중에는 실행하지 않음");return;}
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);if(scene.isDirty){Wait("저장되지 않은 씬 보존: "+scene.name);return;}
                if(Removed.Any(name=>scene.path==Root+"/"+name+".unity")){Wait("삭제 대상이 열려 있음. 사용자가 다른 씬을 연 뒤 진행: "+scene.name);return;}
            }
            try
            {
                if(!File.Exists(Archive)||!File.Exists(Root+"/Situations.unity")||Resources.Load<AnimationLabMotionSet>("ReviewMotions")==null){Wait("백업/새 씬/모션 데이터 준비 대기");return;}
                bool old=File.Exists(Root+"/Walk.unity"),normal=File.Exists(Root+"/Normal.unity");
                if(old&&normal){Wait("Walk/Normal 충돌. 어느 씬도 덮어쓰지 않음");return;}
                string existing=Root+(old?"/Walk.unity":"/Normal.unity");
                if(AssetDatabase.AssetPathToGUID(existing)!=WalkGuid){Wait("기존 Walk GUID가 달라짐. 중단");return;}
                using(var file=File.OpenRead(Archive))using(var zip=new ZipArchive(file,ZipArchiveMode.Read))
                {
                    if(old&&(!Matches(zip,"Walk.unity")||!Matches(zip,"Walk.unity.meta"))){Wait("백업 이후 Walk 편집 감지. 현재 편집을 보존하고 중단");return;}
                    foreach(string name in Removed)
                    {
                        string path=Root+"/"+name+".unity";if(!File.Exists(path))continue;
                        if(!Matches(zip,name+".unity")||!Matches(zip,name+".unity.meta")){Wait("백업 이후 편집 감지: "+name);return;}
                        if(EditorBuildSettings.scenes.Any(s=>s.path==path)){Wait("빌드 목록에서 삭제 대상 참조 발견: "+name);return;}
                    }
                }
                if(old){string result=AssetDatabase.MoveAsset(Root+"/Walk.unity",Root+"/Normal.unity");if(result.Length>0)throw new IOException(result);}
                foreach(string name in Removed){string path=Root+"/"+name+".unity";if(File.Exists(path)&&!AssetDatabase.DeleteAsset(path))throw new IOException("AssetDatabase deletion failed: "+path);}
                if(AssetDatabase.AssetPathToGUID(Root+"/Normal.unity")!=WalkGuid)throw new IOException("Walk GUID preservation failed");
                File.Delete(Request);EditorApplication.update-=TryApply;
                Debug.Log("NORMAL_SITUATIONS_APPLIED: Walk GUID preserved; four obsolete review scenes archived/removed; no scene save, Play, camera or BuildSettings changes.");
            }
            catch(Exception e){Wait(e.Message);}
        }
    }
}
#endif
