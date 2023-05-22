using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace AnimationAuthoring
{
    /// Editor class for the creation of the Authoring Path

    [CustomEditor(typeof(Authoring))]
    public class AuthoringEditor : Editor
    {
        #region Fields
        const string helpInfo = "Shift-click to add or insert new points. Control-click to delete points.";
        // Display
        const int inspectorSectionSpacing = 10;
        GUIStyle boldFoldoutStyle;
        // References:
        Authoring Authoring;
        DisplaySettings DisplaySettings;
        Editor GlobalDisplaySettingsEditor;
        Dictionary<DisplaySettings.GeometryType, Action<object[]>> CapFunctions;

        // State variables:
        int SelectedSegmentIndex;
        int ClosestControlPointIndex;
        int HighlightControlPointIndex;
        int SelectedControlPointIndex;
        #endregion

        #region Unity Editor Functions
        Path Path
        {
            get
            {
                return Authoring.path;
            }
        }

        void Awake()
        {
            Authoring = (Authoring)target;
            if (Authoring.Path == null)
            {
                Authoring.Path = Authoring.CreatePath();
            }
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            LoadDisplaySettings();
            ResetState();
        }
        void OnDisable()
        {
            Tools.hidden = false;
        }
        
        public void MarkDirty() {
            EditorUtility.SetDirty(Authoring);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
        public override void OnInspectorGUI()
        {
            // Initialize GUI styles
            if (boldFoldoutStyle == null)
            {
                boldFoldoutStyle = new GUIStyle(EditorStyles.foldout);
                boldFoldoutStyle.fontStyle = FontStyle.Bold;
                boldFoldoutStyle.normal.textColor = UltiDraw.Orange;
            }

            Undo.RecordObject(Authoring, Authoring.name);
            // Draw inspector's
            DrawInspector();

			if(GUI.changed) {
				MarkDirty();
			}
        }

        void OnSceneGUI()
        {
            //To not select other gameobjects in scene with mouse
            int id = GUIUtility.GetControlID(FocusType.Keyboard);
            HandleUtility.AddDefaultControl(id);

            if (!DisplaySettings.visibleBehindObjects)
            {
                UltiDraw.SetDepthRendering(true);
            }

            
            if(Authoring.transform.hasChanged){
                Matrix4x4 target = Matrix4x4.TRS(Authoring.transform.position, Authoring.transform.rotation, Vector3.one);
                Path.TransformPathRelativeToTarget(target);
                Authoring.transform.hasChanged = false;
            }

            EventType eventType = Event.current.type;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                if (eventType != EventType.Repaint && eventType != EventType.Layout)
                {
                    ProcessPathInput(Event.current);
                }

                DrawPathSceneEditor();

                if (check.changed)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                }
            }
            UltiDraw.SetDepthRendering(false);
        }
        #endregion 

        #region Inspectors
        void DrawInspector()
        {   
            EditorGUILayout.HelpBox(helpInfo, MessageType.None);
            Authoring.Actor = (Actor)EditorGUILayout.ObjectField("Character", Authoring.Actor, typeof(Actor), true);
            EditorGUI.BeginChangeCheck();
            Authoring.Topology = (TOPOLOGY)EditorGUILayout.EnumPopup("Topology", Authoring.Topology);
            if(EditorGUI.EndChangeCheck()) {
                foreach(ControlPoint cp in Path.ControlPoints){
                    if(cp.HasModule<IKModule>()) cp.GetModule<IKModule>().InitSolvers();
                }
            }
            LayerMask tmpMask = EditorGUILayout.MaskField("Input Ground Layer", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(Authoring.GroundInput), InternalEditorUtility.layers);
            Authoring.GroundInput = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tmpMask);
            // Path options:
            Authoring.ShowPathOptions = EditorGUILayout.Foldout(Authoring.ShowPathOptions, new GUIContent("Path Options"), true, boldFoldoutStyle);
            if (Authoring.ShowPathOptions)
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.HelpBox("Choose the same framerate as the model is trained on.", MessageType.None);
                    GUILayout.Space(inspectorSectionSpacing);
                    EditorGUILayout.LabelField("Total Time: " + Path.GetTotalTime() + "s");
                    EditorGUILayout.LabelField("Controlpoints: " + Path.NumControlPoints);
                    EditorGUILayout.LabelField("Points: " + Path.NumPoints);
                    Path.FPS = (int)EditorGUILayout.Slider("Framerate", Path.FPS, 30, 60);
                    Path.TimeInterval = EditorGUILayout.Slider("In-betweening time in sec", Path.TimeInterval, 1/Path.FPS, 10f);
                    Path.isLooping = EditorGUILayout.Toggle("Loop", Path.isLooping);
                    Path.AutoDirections = EditorGUILayout.Toggle("Auto Directions", Path.AutoDirections);
                    if (check.changed)
                    {
                        Path.GenerateAllPoints(Path.TimeDelta);
                        //SceneView.RepaintAll();
                        //EditorApplication.QueuePlayerLoopUpdate();
                    }
                }

                if (Utility.GUIButton("Reset", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    Undo.RecordObject(Authoring, "Reset Path");
                    Authoring.ResetPath();              
                    //EditorApplication.QueuePlayerLoopUpdate();
                }

                // Check if out of bounds (can occur after undo operations)
                if (SelectedControlPointIndex >= Path.NumControlPoints) {
                    SelectedControlPointIndex = -1;
                }

                GUILayout.Space(inspectorSectionSpacing);

                // If a ControlPoint has been selected
                if (SelectedControlPointIndex > -1) {
                    Utility.SetGUIColor(UltiDraw.Grey);
                    EditorGUILayout.LabelField("Selected ControlPoint at " + SelectedControlPointIndex * Path.TimeInterval + "s :", Utility.GetFontColor(Color.white), GUILayout.Width(80f)); 
                    SelectedControlPointIndex = (int)EditorGUILayout.Slider( SelectedControlPointIndex, 0, Path.NumControlPoints-1);
                    EditorGUILayout.BeginHorizontal();
                    if(Utility.GUIButton("<", UltiDraw.Cyan, UltiDraw.Black)) {
                        if(SelectedControlPointIndex > 0) SelectedControlPointIndex--;
                        Repaint();
                    }
                    if(Utility.GUIButton(">", UltiDraw.Cyan, UltiDraw.Black)) {
                        if(SelectedControlPointIndex < Path.NumControlPoints-1) SelectedControlPointIndex++;
                        Repaint();
                    }
                    EditorGUILayout.EndHorizontal();      

                    // Controlpoint Inspector           
                    ControlPoint cp = Path.ControlPoints[SelectedControlPointIndex];
                    cp.Inspector();
                    
                }

                GUILayout.Space(inspectorSectionSpacing);
            }
            // Style Info options:
            Authoring.ShowStyleOptions = EditorGUILayout.Foldout(Authoring.ShowStyleOptions, new GUIContent("Style Info Options [" + Authoring.StyleInfos.Count + "]"), true, boldFoldoutStyle);
            EditorGUILayout.HelpBox("Must be in the same order as in the controller.", MessageType.None);
            if (Authoring.ShowStyleOptions)
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    if(Utility.GUIButton("+", UltiDraw.DarkGreen, UltiDraw.White, 75, 20)) {
                        Authoring.StyleInfos.Add(new Style("Style " + (Authoring.StyleInfos.Count + 1), UltiDraw.Black));
                    }
                    GUILayout.Space(inspectorSectionSpacing);
                    foreach(Style style in Authoring.StyleInfos) {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.BeginVertical();
                            style.Name = EditorGUILayout.TextField("Name", style.Name);
                            style.Color = EditorGUILayout.ColorField("Color", style.Color);

                            EditorGUILayout.EndVertical();
                            if(Utility.GUIButton("-", UltiDraw.DarkRed, UltiDraw.White)) {
                                foreach (ControlPoint cp in Path.ControlPoints)
                                {
                                    if(cp.HasModule<StyleActionModule>()) {
                                        cp.GetModule<StyleActionModule>().RemoveStyle(Authoring.StyleInfos.IndexOf(style));
                                    }
                                }
                                Authoring.StyleInfos.Remove(style);
                                break;
                            }
                            EditorGUILayout.EndHorizontal();
                            GUILayout.Space(inspectorSectionSpacing);  
                    }

                    if (check.changed)
                    {
                        foreach(ControlPoint cp in Path.ControlPoints){
                            cp.Callback();
                        }

                        //SceneView.RepaintAll();
                        //EditorApplication.QueuePlayerLoopUpdate();
                    }
                }
                GUILayout.Space(inspectorSectionSpacing);
            }

            // Motion Import Settings
            Authoring.MotionImportOptions = EditorGUILayout.Foldout(Authoring.MotionImportOptions, new GUIContent("Motion Import Options [" + Authoring.AssetLoader.Assets.Length + "]"), true, boldFoldoutStyle);
            if (Authoring.MotionImportOptions)
            {
                EditorGUILayout.HelpBox("Animation clips that can be used at each control point.", MessageType.None);
                Authoring.AssetLoader.Inspector();
            }

            // Editor display options
            Authoring.ShowDisplayOptions = EditorGUILayout.Foldout(Authoring.ShowDisplayOptions, new GUIContent("Display Options"), true, boldFoldoutStyle);
            if (Authoring.ShowDisplayOptions)
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    Authoring.ShowTransformTool = EditorGUILayout.Toggle(new GUIContent("Enable Transforms"), Authoring.ShowTransformTool);
                    Authoring.DisplayPath = EditorGUILayout.Toggle(new GUIContent("Show Path"), Authoring.DisplayPath);
                    Authoring.DisplayControlPoints = EditorGUILayout.Toggle(new GUIContent("Show Control Points"), Authoring.DisplayControlPoints);
                    Authoring.DisplayLabels = EditorGUILayout.Toggle(new GUIContent("Show Labels"), Authoring.DisplayLabels);
                    Authoring.DisplayDirections = EditorGUILayout.Toggle(new GUIContent("Show Directions"), Authoring.DisplayDirections);
                    Authoring.DisplayTargetPoses = EditorGUILayout.Toggle(new GUIContent("Show Target Poses"), Authoring.DisplayTargetPoses);

                    Tools.hidden = !Authoring.ShowTransformTool;
                    GUILayout.Space(inspectorSectionSpacing);
                }
                DrawDisplaySettingsInspector();
            }
        }

        void DrawDisplaySettingsInspector()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                Authoring.DisplaySettingsFoldout = EditorGUILayout.InspectorTitlebar(Authoring.DisplaySettingsFoldout, DisplaySettings);
                if (Authoring.DisplaySettingsFoldout)
                {
                    CreateCachedEditor(DisplaySettings, null, ref GlobalDisplaySettingsEditor);
                    GlobalDisplaySettingsEditor.OnInspectorGUI();
                }
                if (check.changed)
                {
                    //SceneView.RepaintAll();
                }
            }
        }

        #endregion

        #region Scene GUI Draw

        void InputIndexHandler(Event e){
            //Need to check if Key gets released because OnGUI is atleast called twice per frame
            if(e.type == EventType.KeyUp){
                switch (e.keyCode)
                {
                    case KeyCode.Escape:
                        ResetState();
                        Repaint ();
                        break;
                    /*
                    case KeyCode.A:
                        if(SelectedControlPointIndex > 0) SelectedControlPointIndex--;
                        Repaint ();
                        break;
                    case KeyCode.D:
                        if(SelectedControlPointIndex < Path.NumControlPoints-1) SelectedControlPointIndex++;
                        Repaint ();
                        break;
                    */
                }
            }
        }
        void ProcessPathInput(Event e)
        {
            float minDstToControlPoint = DisplaySettings.controlSize * 1f;
            Vector3 mousePoint = Mouse3D.GetMouseWorldPositionInSceneView(SceneView.lastActiveSceneView, e, Authoring.GroundInput);
            ClosestControlPointIndex = Path.GetClosestControlPointIndexInRange(mousePoint, minDstToControlPoint);

            InputIndexHandler(e);

            // Select ControlPoint by Left Mouse click
            if(e.type == EventType.MouseDown && e.button == 0){
                if (ClosestControlPointIndex != -1)
                {
                    SelectedControlPointIndex = ClosestControlPointIndex;
                    Repaint();
                }
            }

            // Drag Selected ControlPoint
            //Debug.Log(e.type);
            if(e.type == EventType.MouseDrag && e.button == 0){
                if (SelectedControlPointIndex != -1)
                {
                    Undo.RecordObject (Authoring, "Move point");
                    Path.MovePoint(SelectedControlPointIndex, mousePoint);
                    float timestamp = SelectedControlPointIndex * Path.TimeInterval; 
                    Path.UpdateAroundControlPoint(timestamp, Path.UPDATETYPE.NONE);
                    Repaint();
                }
            }

            // Shift-left click to add or split segment
            if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
            {
                // Unselect ControlPoint
                SelectedControlPointIndex = -1;

                if (SelectedSegmentIndex != -1)
                {
                    Undo.RecordObject(Authoring, "Split segment");
                    Path.InsertControlPoint(Path.GetClosestTimeOnPath(mousePoint));
                    Repaint ();
                }
                else if (!Path.isLooping)
                {
                    Undo.RecordObject(Authoring, "Add segment");
                    Path.AddControlPoint(mousePoint);
                    e.Use();
                    Repaint ();
                }
            }
            // Control click or backspace/delete to remove point
            if (e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete || ((e.control || e.command) && e.type == EventType.MouseDown && e.button == 0))
            {
                if (ClosestControlPointIndex != -1)
                {
                    Undo.RecordObject(Authoring, "Delete segment");
                    Path.RemoveControlPoint(ClosestControlPointIndex);
                    ResetState();
                }
            }

            // Update Selection Indices on mouse movement
            if (e.type == EventType.MouseMove)
            {
                float minDstToSegment = 0.1f;
                int newSelectedSegmentIndex = -1;

                float timestamp = Path.GetClosestTimeOnPath(mousePoint);
                float dst = Vector3.Distance(mousePoint, Path.CalculatePointPosition(timestamp));
                if (dst < minDstToSegment)
                {
                        minDstToSegment = dst;
                        newSelectedSegmentIndex = Path.GetClosestIndex(timestamp);
                }
                
                if (newSelectedSegmentIndex != SelectedSegmentIndex)
                {
                    SelectedSegmentIndex = newSelectedSegmentIndex;
                }
                HighlightControlPointIndex = ClosestControlPointIndex;
                Repaint ();
            }

        }
        void DrawPathSceneEditor()
        {

            UltiDraw.Begin();
            // Draw All Segments
            for (int i = 0; i < Path.NumSegments; i++)
            {   
                float timestamp = i*Path.TimeInterval;
                Point[] points = Path.GetPointsInSegment(timestamp);
                
                bool selected = (i == SelectedSegmentIndex && Event.current.shift);
                Color mixedColor = new Color(0,0,0,1);
                // Color segmentCol = (i == SelectedSegmentIndex && Event.current.shift) ? DisplaySettings.segmentSelected : DisplaySettings.path;;
                for(int j = 0; j < points.Length; j++){
                    int nextI = j + 1;
                    mixedColor = Color.Lerp(Path.ControlPoints[Path.GetClosestIndex(timestamp)].GetColor(), Path.ControlPoints[Path.GetClosestIndex(timestamp+Path.TimeInterval)].GetColor(), (float)j/(float)points.Length);

                    if(Authoring.DisplayDirections){
                        UltiDraw.DrawLine(points[j].GetPosition(), points[j].GetPosition() + points[j].GetDirection().normalized * DisplaySettings.directionsLength, 0.025f, 0f, DisplaySettings.directions);
                    }
                    if(!Authoring.DisplayPath) continue;

                    if (nextI >= points.Length)
                    {
                        mixedColor = Color.Lerp(Path.ControlPoints[Path.GetClosestIndex(timestamp)].GetColor(), Path.ControlPoints[Path.GetClosestIndex(timestamp+Path.TimeInterval)].GetColor(), (float)nextI/(float)points.Length);
                        UltiDraw.DrawLine(points[j].GetPosition(), Path.CalculatePointPosition(timestamp+Path.TimeInterval), DisplaySettings.pathThickness, selected ? DisplaySettings.segmentSelected : mixedColor);
                        break;
                    }
                    UltiDraw.DrawLine(points[j].GetPosition(), points[nextI].GetPosition(), DisplaySettings.pathThickness, selected ? DisplaySettings.segmentSelected : mixedColor);
                }
            }
            UltiDraw.End();

            //Sceneview draw
            for(int i = 0; i < Path.NumControlPoints; i++){
                ControlPoint cp = Path.ControlPoints[i];
                //Draw ControlPoints
                if (Authoring.DisplayControlPoints){
                    Color cpCol = DisplaySettings.control;
                    if(i == SelectedControlPointIndex) cpCol = DisplaySettings.controlSelected;
                    else if(i == HighlightControlPointIndex) cpCol = DisplaySettings.controlHighlighted;

                    UltiDraw.Begin();
                    CapFunctions[DisplaySettings.controlShape](new object[4] { cp.GetPosition(), Quaternion.identity, DisplaySettings.controlSize, cpCol });
                    UltiDraw.End();
                }
                //Draw Label
                if(Authoring.DisplayLabels){
                    GUIStyle style = new GUIStyle();
                    style.fontSize = (int)HandleUtility.GetHandleSize(cp.GetPosition());
                    Handles.Label(cp.GetPosition(), i +" : " + i*Path.TimeInterval + "s", style);
                }

                if(SelectedControlPointIndex == i) continue;
                
                //Draw Rotation Handle
                if (Authoring.DisplayControlPoints){
                    UltiDraw.Begin();
                    Quaternion rot = Handles.Disc(Quaternion.LookRotation(cp.GetDirection(), Vector3.up), cp.GetPosition(), new Vector3(0, 1, 0), DisplaySettings.controlSize * 3f, true, 0);
                    UltiDraw.End();
                    
                    //Update when rotation changes
                    if(rot != Quaternion.LookRotation(cp.GetDirection(), Vector3.up)){
                        cp.SetDirection(rot.GetForward());
                        float timestamp = i * Path.TimeInterval;
                        Path.UpdateAroundControlPoint(timestamp, Path.UPDATETYPE.NONE);
                    }
                }

            }
            //Draw Actor
            Authoring.DrawTargetPoses(Authoring.DisplayTargetPoses);
            
            //UltiDraw.DrawTranslateGizmo(Path.ControlPoints[SelectedControlPointIndex].GetPosition(), Quaternion.identity, 5f * DisplaySettings.controlSize);
            //UltiDraw.DrawRotateGizmo(Path.ControlPoints[SelectedControlPointIndex].GetPosition(), Quaternion.LookRotation(Path.ControlPoints[SelectedControlPointIndex].GetDirection(), Vector3.up), 5f * DisplaySettings.controlSize);  
            //UltiDraw.DrawScaleGizmo(Path.ControlPoints[SelectedControlPointIndex].GetPosition(), Quaternion.identity, 5f * DisplaySettings.controlSize);  
            
        }
        
        #endregion

        #region Internal methods
        void OnUndoRedo()
        {
            SelectedSegmentIndex = -1;
            Repaint();
        }

        void ResetState () {
            SelectedSegmentIndex = -1;
            ClosestControlPointIndex = -1 ;
            HighlightControlPointIndex = -1;
            SelectedControlPointIndex = -1;

            SceneView.RepaintAll ();
            EditorApplication.QueuePlayerLoopUpdate ();
        }
        void LoadDisplaySettings()
        {
            DisplaySettings = DisplaySettings.Load();

            CapFunctions = new Dictionary<DisplaySettings.GeometryType, Action<object[]>>();
            CapFunctions.Add(DisplaySettings.GeometryType.Circle, (parameters) => UltiDraw.DrawCircle((Vector3)parameters[0], (Quaternion)parameters[1], (float)parameters[2], (Color)parameters[3]));
            CapFunctions.Add(DisplaySettings.GeometryType.Sphere, (parameters) => UltiDraw.DrawSphere((Vector3)parameters[0], (Quaternion)parameters[1], (float)parameters[2], (Color)parameters[3]));
            CapFunctions.Add(DisplaySettings.GeometryType.Cube, (parameters) => UltiDraw.DrawCube((Vector3)parameters[0], (Quaternion)parameters[1], (float)parameters[2], (Color)parameters[3]));
        }


        void OnPathModifed()
        {
            // hasUpdatedScreenSpaceLine = false;
            // hasUpdatedNormalsVertexPath = false;

            RepaintUnfocusedSceneViews();
        }

        void RepaintUnfocusedSceneViews()
        {
            // If multiple scene views are open, repaint those which do not have focus.
            if (SceneView.sceneViews.Count > 1)
            {
                foreach (SceneView sv in SceneView.sceneViews)
                {
                    if (EditorWindow.focusedWindow != (EditorWindow)sv)
                    {
                        sv.Repaint();
                    }
                }
            }
        }
        #endregion
    }
}