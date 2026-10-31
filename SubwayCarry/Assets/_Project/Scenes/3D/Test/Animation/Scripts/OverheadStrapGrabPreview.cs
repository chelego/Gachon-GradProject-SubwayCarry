using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
 [ExecuteAlways] public sealed class OverheadStrapGrabPreview:MonoBehaviour
 {
  public GameObject mannequin;
  public AnimationClip clip;
  public RenderPipelineAsset previewPipeline;
  public float clipTime;
  public bool paused,repeat=true;
  [Range(.1f,2f)]public float speed=1;
  public int completedSequences;
  RenderPipelineAsset previousPipeline;bool previousBackground;
  void OnEnable(){previousPipeline=QualitySettings.renderPipeline;if(previewPipeline!=null)QualitySettings.renderPipeline=previewPipeline;if(Application.isPlaying){previousBackground=Application.runInBackground;Application.runInBackground=true;}}
  void OnDisable(){if(QualitySettings.renderPipeline==previewPipeline)QualitySettings.renderPipeline=previousPipeline;if(Application.isPlaying)Application.runInBackground=previousBackground;}
  void Start(){if(!Application.isPlaying)return;if(mannequin==null||clip==null){Debug.LogError("Assign the mannequin and saved strap animation.",this);enabled=false;return;}Restart();}
  public void Restart(){clipTime=0;paused=false;Sample();}
  public void Sample(){if(mannequin!=null&&clip!=null){clipTime=Mathf.Clamp(clipTime,0,clip.length);clip.SampleAnimation(mannequin,clipTime);}}
  void Update(){if(!Application.isPlaying)return;var k=Keyboard.current;if(k!=null){if(k.spaceKey.wasPressedThisFrame)paused=!paused;if(k.rKey.wasPressedThisFrame)Restart();}if(paused||clip==null)return;clipTime+=Time.deltaTime*speed;if(clipTime>clip.length){completedSequences++;if(repeat)clipTime%=clip.length;else{clipTime=clip.length;paused=true;}}Sample();}
  void OnGUI(){if(!Application.isPlaying||clip==null)return;GUILayout.BeginArea(new Rect(18,18,380,180),GUI.skin.box);
   GUILayout.Label("OVERHEAD STRAP GRAB | 60 FPS");
   string phase=clipTime<.5f?"Stand":clipTime<2.5f?"Raise arm":clipTime<3?"Close fingers":clipTime<5?"Hold":clipTime<5.5f?"Release":clipTime<7.5f?"Lower arm":"Stand";
   GUILayout.Label(phase+" | "+Mathf.RoundToInt(clipTime*60)+" / "+Mathf.RoundToInt(clip.length*60)+"F");
   GUILayout.BeginHorizontal();if(GUILayout.Button(paused?"Resume [Space]":"Pause [Space]"))paused=!paused;if(GUILayout.Button("Restart [R]"))Restart();repeat=GUILayout.Toggle(repeat,"Repeat");GUILayout.EndHorizontal();
   float seek=GUILayout.HorizontalSlider(clipTime,0,clip.length);if(Mathf.Abs(seek-clipTime)>.001f){clipTime=seek;paused=true;Sample();}
   GUILayout.Label("Speed "+speed.ToString("F2")+"x | completed "+completedSequences);speed=GUILayout.HorizontalSlider(speed,.1f,2f);GUILayout.EndArea();
  }
 }
}