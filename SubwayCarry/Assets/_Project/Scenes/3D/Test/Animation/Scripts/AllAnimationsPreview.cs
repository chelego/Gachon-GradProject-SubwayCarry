using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab {
public sealed class AllAnimationsPreview:MonoBehaviour {
 public GameObject[] stages; public string[] labels; public int selected;
 void Start(){Select(selected);}
 public void Select(int index){if(stages==null||index<0||index>=stages.Length)return;for(int i=0;i<stages.Length;i++)if(stages[i]!=null)stages[i].SetActive(false);selected=index;stages[index].SetActive(true);}
 void OnGUI(){GUILayout.BeginArea(new Rect(Screen.width-300,18,282,250),GUI.skin.box);GUILayout.Label("ALL ANIMATIONS");for(int i=0;i<stages.Length;i++)if(GUILayout.Button((i==selected?"> ":"")+labels[i]))Select(i);GUILayout.Label("Choose a stage, then use its playback controls.\nRight drag: orbit | Wheel: zoom");GUILayout.EndArea();}
}}