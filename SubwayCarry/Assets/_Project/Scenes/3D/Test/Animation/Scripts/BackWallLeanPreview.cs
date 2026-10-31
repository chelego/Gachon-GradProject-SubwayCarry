using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
 [ExecuteAlways]
 public sealed class BackWallLeanPreview:MonoBehaviour
 {
  public GameObject mannequin;
  public AnimationClip sequenceClip, basicClip, shiftClip;
  public RenderPipelineAsset previewPipeline;
  public int selected;
  public float clipTime;
  public bool paused, repeat=true;
  [Range(.1f,2f)] public float speed=1;
  public int completedSequences;
  RenderPipelineAsset previousPipeline;
  bool previousBackground;
  public AnimationClip Current=>selected==1?basicClip:selected==2?shiftClip:sequenceClip;
  void OnEnable(){previousPipeline=QualitySettings.renderPipeline;if(previewPipeline!=null)QualitySettings.renderPipeline=previewPipeline;if(Application.isPlaying){previousBackground=Application.runInBackground;Application.runInBackground=true;}}
  void OnDisable(){if(QualitySettings.renderPipeline==previewPipeline)QualitySettings.renderPipeline=previousPipeline;if(Application.isPlaying)Application.runInBackground=previousBackground;}
  void Start(){if(!Application.isPlaying)return;if(mannequin==null||sequenceClip==null||basicClip==null||shiftClip==null){Debug.LogError("Assign the mannequin and all three saved back lean clips.",this);enabled=false;return;}Select(0);}
  public void Select(int index){selected=Mathf.Clamp(index,0,2);clipTime=0;paused=false;Sample();}
  public void Sample(){if(mannequin==null||Current==null)return;clipTime=Mathf.Clamp(clipTime,0,Current.length);Current.SampleAnimation(mannequin,clipTime);}
  void Update(){
   if(!Application.isPlaying)return;
   var k=Keyboard.current;if(k!=null){if(k.digit1Key.wasPressedThisFrame)Select(0);if(k.digit2Key.wasPressedThisFrame)Select(1);if(k.digit3Key.wasPressedThisFrame)Select(2);if(k.spaceKey.wasPressedThisFrame)paused=!paused;if(k.rKey.wasPressedThisFrame)Select(selected);}
   if(paused||Current==null)return;clipTime+=Time.deltaTime*speed;
   if(clipTime>Current.length){completedSequences++;if(repeat)clipTime%=Current.length;else{clipTime=Current.length;paused=true;}}Sample();
  }
  void OnGUI(){
   if(!Application.isPlaying||Current==null)return;
   GUILayout.BeginArea(new Rect(18,18,380,200),GUI.skin.box);
   GUILayout.Label("BACK WALL LEAN | 60 FPS");
   GUILayout.BeginHorizontal();if(GUILayout.Button("Full [1]"))Select(0);if(GUILayout.Button("Basic [2]"))Select(1);if(GUILayout.Button("Shift loop [3]"))Select(2);GUILayout.EndHorizontal();
   GUILayout.Label(Current.name+" | "+Mathf.RoundToInt(clipTime*60)+" / "+Mathf.RoundToInt(Current.length*60)+"F");
   GUILayout.BeginHorizontal();if(GUILayout.Button(paused?"Resume [Space]":"Pause [Space]"))paused=!paused;if(GUILayout.Button("Restart [R]"))Select(selected);repeat=GUILayout.Toggle(repeat,"Repeat");GUILayout.EndHorizontal();
   float seek=GUILayout.HorizontalSlider(clipTime,0,Current.length);if(Mathf.Abs(seek-clipTime)>.001f){clipTime=seek;paused=true;Sample();}
   GUILayout.Label("Speed "+speed.ToString("F2")+"x | completed "+completedSequences);speed=GUILayout.HorizontalSlider(speed,.1f,2f);
   GUILayout.EndArea();
  }
 }
}