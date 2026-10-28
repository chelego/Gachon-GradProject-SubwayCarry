#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad] public static class AnimationLabPhoneCarryChecks
    {
        const string Key="SubwayCarry.PhoneCarry.QA";static bool leaving,started,failed;static float began;
        static AnimationLabPhoneCarryChecks(){EditorApplication.update+=Tick;}
        public static void Run(){AnimationLabPhoneBuild.Guard();SessionState.SetInt(Key,1);EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Situations.unity");EditorApplication.isPlaying=true;}
        static void Tick()
        {
            if(SessionState.GetInt(Key,0)==0)return;if(leaving){if(!EditorApplication.isPlaying){SessionState.SetInt(Key,0);EditorApplication.Exit(failed?2:0);}return;}if(!EditorApplication.isPlaying||AnimationLabPhoneCase.Active==null)return;
            try
            {
                var trial=AnimationLabPhoneCase.Active;if(!started){trial.SetReviewMode(4);started=true;began=Time.time;}
                if(Time.time-began<4)return;var layer=trial.Passenger.PhoneLayer;if(layer.IsTransitioning||layer.SettledMode!=AnimationLabPhoneMode.Carry||!layer.PhoneInHand)throw new InvalidOperationException("Carry did not settle");
                var arm=trial.Passenger.transform.Find(trial.Data.rightUpperPath);var lower=trial.Passenger.transform.Find(trial.Data.rightLowerPath);var hand=trial.Passenger.transform.Find(trial.Data.rightHandPath);var a=arm.localRotation;var b=lower.localRotation;var h=hand.localRotation;
                var camera=Camera.main;var actor=trial.Passenger.transform;var target=actor.position+Vector3.up*.98f;var eye=target+actor.right*2.1f+actor.forward*.9f+Vector3.up*.12f;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.fieldOfView=48;
                var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=null;camera.targetTexture=null;
                string directory=Path.GetFullPath("../../../MotionMatching/PhonePassenger/PhoneSet");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,"Carry_Right.png"),pixels.EncodeToPNG());UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
                layer.RestoreBase();float error=Mathf.Max(Quaternion.Angle(a,arm.localRotation),Quaternion.Angle(b,lower.localRotation),Quaternion.Angle(h,hand.localRotation));if(error>.2f)throw new InvalidOperationException("Carry altered the base walk arm beyond the authored grip: "+error);
                Debug.Log("PHONE_CARRY_BASE_WALK_OK armDeltaDegrees="+error+" phoneInHand=true rightSideCapture=true");leaving=true;EditorApplication.isPlaying=false;
            }
            catch(Exception e){Debug.LogException(e);failed=leaving=true;EditorApplication.isPlaying=false;}
        }
    }
}
#endif
