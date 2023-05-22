using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace AnimationAuthoring
{
    public enum TOPOLOGY {Biped, Quadruped, Custom};

    public class Authoring : MonoBehaviour
    {
        DisplaySettings DisplaySettings;
        [SerializeField] public Path Path;
        [SerializeField] public List<Style> StyleInfos = new List<Style>();
        public LayerMask GroundInput = ~1;
        public Actor Actor;
        public TOPOLOGY Topology = TOPOLOGY.Biped;
        [SerializeField] public AssetLoader AssetLoader = new AssetLoader();
        public bool DrawScene = true;
        // path display settings
        public bool ShowTransformTool = true;
        public bool DisplayPath = true;
        public bool DisplayControlPoints = true;
        public bool DisplayLabels = true;
        public bool DisplayDirections = true;
        public bool DisplayTargetPoses = true;
        public bool DisplaySettingsFoldout;
        // Editor display states
        public bool ShowDisplayOptions;
        public bool ShowPathOptions = true;
        public bool ShowStyleOptions = true;     
        public bool MotionImportOptions = true;

        // Reset to default values
        // Reset button in the Inspector's context menu or when adding the component the first time
        void Reset(){
            // Find character in scene
            if(GetActor() == null) Debug.LogWarning("A character cant be found in the scene.");
            // Set Ground layer
            GroundInput = LayerMask.GetMask("Ground");

            // Default Styles
            StyleInfos.Add(new Style("Idle", UltiDraw.DarkGreen));
            StyleInfos.Add(new Style("Move", UltiDraw.DarkRed));
            
            // Load Mocap
            ArrayExtensions.Append(ref AssetLoader.Folders, "Demo/Authoring/MotionCapture/Biped/LaFAN1/testing");
            ArrayExtensions.Append(ref AssetLoader.Imports, true);
            #if UNITY_EDITOR
            EditorCoroutines.StartCoroutine(AssetLoader.Import(), AssetLoader);
            #endif
            ResetPath();
        }

        public Actor GetActor(){
            if(Actor == null) {
                Actor findActor = GameObject.FindObjectOfType<Actor>();
                if(findActor !=null){
                    Actor = findActor;
                    Debug.Log("Linked " + findActor + " to " + this.name + "!");
                } 
            }
            return Actor;
        }
        public Path path
        {
            get
            {
                return Path == null ? CreatePath() : Path;
            }
        }
        public Path CreatePath()
        {
            Path = new Path(this);
            return Path;
        }

        public Path ResetPath()
        {
            //Delete transforms
            Transform[] childs = transform.GetChilds();
            foreach(Transform child in childs) {
                Utility.Destroy(child.gameObject);
            }

            Path = CreatePath();
            Path.TransformPathRelativeToTarget(transform.GetWorldMatrix());
            return Path;
        }

        public void DrawPathPoints()
        {
            if (Path != null)
            {
                if (DisplaySettings == null)
                {
                    DisplaySettings = DisplaySettings.Load();
                }

                // Draw All Segments
                for (int i = 0; i < Path.NumSegments; i++)
                {   
                    float timestamp = i*Path.TimeInterval;
                    Point[] points = Path.GetPointsInSegment(timestamp);
                    
                    Color mixedColor = new Color(0,0,0,1);
                    for(int j = 0; j < points.Length; j++){
                        int nextI = j + 1;
                        mixedColor = Color.Lerp(Path.ControlPoints[Path.GetClosestIndex(timestamp)].GetColor(), Path.ControlPoints[Path.GetClosestIndex(timestamp+Path.TimeInterval)].GetColor(), (float)j/(float)points.Length);

                        if(DisplayDirections){
                            UltiDraw.DrawLine(points[j].GetPosition(), points[j].GetPosition() + points[j].GetDirection().normalized * DisplaySettings.directionsLength, 0.025f, 0f, DisplaySettings.directions);
                        }

                        if (nextI >= points.Length)
                        {
                            mixedColor = Color.Lerp(Path.ControlPoints[Path.GetClosestIndex(timestamp)].GetColor(), Path.ControlPoints[Path.GetClosestIndex(timestamp+Path.TimeInterval)].GetColor(), (float)nextI/(float)points.Length);
                            UltiDraw.DrawLine(points[j].GetPosition(), Path.CalculatePointPosition(timestamp+Path.TimeInterval), DisplaySettings.pathThickness, mixedColor);
                            break;
                        }
                        UltiDraw.DrawLine(points[j].GetPosition(), points[nextI].GetPosition(), DisplaySettings.pathThickness, mixedColor);
                    }
                }
            }
        }

        public void DrawTargetPoses(bool showTargetPoses)
        {
            if(!showTargetPoses) {
                SetChildsActive(false);
                return;
            }
            for(int i = 0; i < Path.NumControlPoints; i++){
                ControlPoint cp = Path.ControlPoints[i];

                if(cp.HasModule<IKModule>()){
                    //Draw Actor
                    IKModule m = cp.GetModule<IKModule>();
                    if(DisplaySettings != null)m.DrawTargetPose(DisplaySettings.targetPoses);
                }
            }   
            SetChildsActive(true);
        }

#if UNITY_EDITOR
        // Draw the path when path objected is not selected (if enabled in settings)
        void OnDrawGizmos()
        {
            // Only draw path gizmo if the path object is not selected
            // (editor script is resposible for drawing when selected)
            GameObject selectedObj = UnityEditor.Selection.activeGameObject;
            if (selectedObj != gameObject)
            {
                if (DisplaySettings == null)
                {
                    DisplaySettings = DisplaySettings.Load();
                }

                if (Path != null)
                {
                    if (DisplaySettings.visibleWhenNotSelected)
                    {
                        Draw();
                    }
                }
            }
        }
#endif
        //Runtime draw (Gameview)
        public void Draw(Camera canvas=null, bool showTargetPoses = true) {
            if(DrawScene) {
                UltiDraw.Begin(canvas);
                DrawPathPoints();
                UltiDraw.End();
                
/*                 for(int i = 0; i < Path.NumControlPoints; i++){
                    ControlPoint cp = Path.ControlPoints[i];

                    if(!cp.HasModule<IKModule>()) continue;
                    //Draw Actor
                    IKModule m = cp.GetModule<IKModule>();
                    m.DrawTargetPose(DisplaySettings.targetPoses);
                }    */
            }
        }

        public void SetChildsActive(bool b) {
            Transform[] childs = transform.GetChilds();
            foreach(Transform child in childs) {
                child.gameObject.SetActive(b);
            }
        }

        public void SortChildren(){
            List<Transform> children = new List<Transform>(transform.GetChilds());
			children.Sort((Transform t1, Transform t2) => { return t1.name.CompareTo(t2.name); });
			for (int i = 0; i < children.Count; ++i)
			{
                #if UNITY_EDITOR
				Undo.SetTransformParent(children[i], children[i].parent, "Sort Children");
                #endif
				children[i].SetSiblingIndex(i);
			}
        }
    }
}