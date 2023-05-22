using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateIK;
using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
#endif

namespace AnimationAuthoring
{
    [Serializable]
    public class IKModule : Module
    {
        // MOTION DATA
        public Transform Character;
        public Matrix4x4[] TargetPoseTransformations = new Matrix4x4[0];
        public Vector3[] TargetPoseVelocities = new Vector3[0];
        public MotionData Data;
        public string[] Assets = new string[0];
        public string[] Enums = new string[0];
        public string Filter = string.Empty;
        public bool Mirror = false;
        public bool VisualizeTargetPose = true;
        public bool EnableTargetPose = true; // For runtime interpolation
        public int AssetIndex = -1;
        public float Timestamp = 0f;
        public int[] BoneMapping = null;

        // ROOT
        public int Root = 0;
        public int RightShoulder, LeftShoulder, RightHip, LeftHip, Neck, Hips;

        //SUBSOLVER
        public UltimateIK.ACTIVATION Activation = UltimateIK.ACTIVATION.Constant;
        public SubsolverSetup[] Solvers = new SubsolverSetup[0];

        public Actor Actor {
            get
            {
                if(Authoring.GetActor() == null){
                    Debug.LogWarning("A character cant be found in the scene. Removing module " + GetID() + " from Controlpoint " + Authoring.Path.ControlPoints.IndexOf(ControlPoint));
                    if(ControlPoint.HasModule<IKModule>()) ControlPoint.RemoveModule(GetID());
                }
                return Authoring.Actor;
            }
        }
        public override ID GetID() 
        {
		    return ID.Betweening;
	    }
        protected override void DerivedInitialize()
        {
            LoadMocap();
            InitSolvers();
        }
        protected override void DerivedRemove()
        {
            Utility.Destroy(Character.gameObject);
        }

        protected override void DerivedCallback() {
            LoadFrame(Timestamp);
        }

    #region MotionData
        public void LoadMocap(){
            ApplyFilter();
            LoadCurrentAsset();
        }

        public void ApplyCharacter() {
            int indexCP = Authoring.Path.ControlPoints.IndexOf(ControlPoint);
            if(indexCP == -1) return;
            if(VisualizeTargetPose) {
                if(Character == null){
                    Character = Transform.Instantiate(Actor.gameObject).transform;
                    Transparency transparency = Character.gameObject.AddComponent<Transparency>();
                    transparency.SetTransparency(0.5f);
                    Character.SetParent(Authoring.transform);
                }
                Character.name = "Pose" + indexCP;
                foreach(Component c in Character.GetComponents(typeof(Component))) {
                    if(! (c is Actor || c is Transform || c is Transparency || c is LODGroup)){
                        Component.DestroyImmediate(c);
                    }
                }
                foreach(Light l in Character.GetComponentsInChildren<Light>()) {
                    l.enabled = false;
                }
                foreach(SkinnedMeshRenderer r in Character.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    r.enabled = true;
                }
                foreach(Renderer r in Character.GetComponentsInChildren<Renderer>()) {
                    r.enabled = true;
                }
                Actor actor = Character.GetComponent<Actor>();
                actor.DrawSkeleton = false;

				for(int b=0; b<actor.Bones.Length; b++) {
					Matrix4x4 bone = TargetPoseTransformations[b];
					actor.Bones[b].GetTransform().position = bone.GetPosition();
					actor.Bones[b].GetTransform().rotation = bone.GetRotation();
				}
            } else {
                if(Character != null) {
                    Utility.Destroy(Character.gameObject);
                }
            }
        }

        public static int FindIndex(string[] guids, string guid) {
            return System.Array.FindIndex(guids, x => x == guid);
        }
        public void ApplyFilter() {
            string[] AuthoringAssets = Authoring.AssetLoader.Assets;
            List<string> assets = new List<string>();
            List<string> enums = new List<string>();
            for(int i=0; i<AuthoringAssets.Length; i++) {
                if(Filter == string.Empty) {
                    Add(i);
                } else {
                    bool value = Utility.GetAssetName(AuthoringAssets[i]).ToLowerInvariant().Contains(Filter.ToLowerInvariant());
                    if(value) {
                        Add(i);
                    }
                }
            }
            Assets = assets.ToArray();
            Enums = enums.ToArray();

            void Add(int index) {
                assets.Add(AuthoringAssets[index]);
                enums.Add("[" + (index+1) + "]" + " " + Utility.GetAssetName(AuthoringAssets[index]));
            }
        }

        public MotionData GetAsset() {
            return Data;
        }

        public MotionData GetAsset(int index) {
            return GetAsset(Assets[index]);
        }

        public MotionData GetAsset(string guid) {
            #if UNITY_EDITOR
            if(guid == null || guid == string.Empty) {
                return null;
            } else {
                MotionData asset = (MotionData)AssetDatabase.LoadAssetAtPath(Utility.GetAssetPath(guid), typeof(MotionData));
                asset.SetPrecomputable(false);
                return asset;
            }
            #else
            return null;
            #endif
        }

        public void LoadPreviousAsset() {
            int pivot = FindIndex(Assets, Utility.GetAssetGUID(Data));
            if(pivot > 0) {
                LoadData(Assets[Mathf.Max(pivot-1, 0)]);
            }
        }

        public void LoadCurrentAsset() {
            int pivot = FindIndex(Assets, Utility.GetAssetGUID(Data));
            if(pivot < Assets.Length-1 && Assets.Length > 0) {
                LoadData(Assets[Mathf.Clamp(Mathf.Min(pivot, Assets.Length-1), 0, Assets.Length)]);
            }
        }

        public void LoadNextAsset() {
            int pivot = FindIndex(Assets, Utility.GetAssetGUID(Data));
            if(pivot < Assets.Length-1) {
                LoadData(Assets[Mathf.Min(pivot+1, Assets.Length-1)]);
            }
        }
        public int[] GetBoneMapping() {
            if(Data == null) {
                return null;
            }
            if(BoneMapping == null || BoneMapping.Length != Actor.Bones.Length) {
                BoneMapping = Data.Source.GetBoneIndices(Actor.GetBoneNames());
            }
            return BoneMapping;
        }

        public MotionData LoadData(string guid) {
            if(guid != null && Data != null && Data.GetName() == Utility.GetAssetName(guid)) {
                return Data;
            }
            MotionData data = GetAsset(guid);
            if(Data != data) {
                //Load Next
                Data = data;
                if(Data != null) {
                    //Data.Load(this);
                    LoadFrame(0f);
                }
                //Assign Variables
                AssetIndex = FindIndex(Assets, Utility.GetAssetGUID(Data));
            }
            return Data;
        }
        public Frame GetCurrentFrame() {
            return Data == null ? null : Data.GetFrame(Timestamp);
        }

        public void LoadFrame(float timestamp) {
            if(Data == null) {
                TargetPoseTransformations = new Matrix4x4[0];
                TargetPoseVelocities =  new Vector3[0];
                return;
            }
            
            Physics.autoSyncTransforms = true;
            Timestamp = timestamp;
            Data.SetPrecomputable(false);

            InitialiseBones();
            List<int> bones = new List<int>();
            for(int i=0; i<Actor.Bones.Length; i++) {
                if(GetBoneMapping()[i] == -1) {
                    Debug.Log("Bone " + Actor.Bones[i].GetName() + " could not be mapped.");
                } else {    
                    bones.Add(GetBoneMapping()[i]);
                }
            }
            TargetPoseTransformations = GetCurrentFrame().GetBoneTransformations(bones.ToArray(), Mirror);
            TargetPoseVelocities = GetCurrentFrame().GetBoneVelocities(bones.ToArray(), Mirror);
            //Transform from root to controlpoint
            for(int i = 0; i < TargetPoseTransformations.Length; i++){
                TargetPoseTransformations[i] = Matrix4x4Extensions.TransformationFromTo(TargetPoseTransformations[i], RootTransformation(timestamp, Mirror), ControlPoint.GetTransformation());
                TargetPoseVelocities[i] = TargetPoseVelocities[i].GetDirectionFromTo(RootTransformation(timestamp, Mirror), ControlPoint.GetTransformation());
            }

            ApplyCharacter();
        }

        public void LoadFrame(int index) {
            LoadFrame(Data.GetFrame(index).Timestamp);
        }

        public void LoadFrame(Frame frame) {
            if(frame == null){
                TargetPoseTransformations = new Matrix4x4[0];
                TargetPoseVelocities =  new Vector3[0];
                return;
            } 
            LoadFrame(frame.Index);
        }

        public void InitialiseBones(){
            MotionData.Hierarchy.Bone rs = Data.Source.FindBoneContains("RightArm");
            RightShoulder = rs == null ? 0 : rs.Index;
            MotionData.Hierarchy.Bone ls = Data.Source.FindBoneContains("LeftArm");
            LeftShoulder = ls == null ? 0 : ls.Index;
            MotionData.Hierarchy.Bone rh = Data.Source.FindBoneContains("RightHip", "RightUpLeg");
            RightHip = rh == null ? 0 : rh.Index;
            MotionData.Hierarchy.Bone lh = Data.Source.FindBoneContains("LeftHip", "LeftUpLeg");
            LeftHip = lh == null ? 0 : lh.Index;
            MotionData.Hierarchy.Bone n = Data.Source.FindBoneContains("Neck");
            Neck = n == null ? 0 : n.Index;
            MotionData.Hierarchy.Bone h = Data.Source.FindBoneContains("Hips");
            Hips = h == null ? 0 : h.Index;
        }

        public Matrix4x4 RootTransformation(float timestamp, bool mirrored){
            return Matrix4x4.TRS(RootPosition(timestamp, mirrored), RootRotation(timestamp, mirrored), Vector3.one);
        }
        public Matrix4x4 GetTargetRoot(Matrix4x4[] pose = null){
            if(pose == null){
                pose = TargetPoseTransformations;
            }
            return Matrix4x4.TRS(GetRootPosition(pose), GetRootRotation(pose), Vector3.one);
        }

        public Vector3 RootPosition(float timestamp, bool mirrored) {
            float start = Data.GetFirstValidFrame().Timestamp;
            float end = Data.GetLastValidFrame().Timestamp;
            if(timestamp < start || timestamp > end) {
                float boundary = Mathf.Clamp(timestamp, start, end);
                float pivot = 2f*boundary - timestamp;
                float clamped = Mathf.Clamp(pivot, start, end);
                return 2f*RootPosition(Data.GetFrame(boundary)) - RootPosition(Data.GetFrame(clamped));
            } else {
                return RootPosition(Data.GetFrame(timestamp));
            }

            Vector3 RootPosition(Frame frame) {
                Vector3 position = frame.GetBoneTransformation(Root, mirrored).GetPosition();
                if(ControlPoint.Ground == 0) {
                    position.y = 0f;
                } else {
                    position = Utility.ProjectGround(position, ControlPoint.Ground);
                }
                return position;
            }
        }
        public Vector3 GetRootPosition(Matrix4x4[] pose) {
            Vector3 position = pose[Root].GetPosition();
            if(ControlPoint.Ground == 0) {
                position.y = 0f;
            } else {
                position = Utility.ProjectGround(position, ControlPoint.Ground);
            }
            return position;
        }
        public Quaternion RootRotation(float timestamp, bool mirrored) {
            float start = Data.GetFirstValidFrame().Timestamp;
            float end = Data.GetLastValidFrame().Timestamp;
            if(timestamp < start || timestamp > end) {
                float boundary = Mathf.Clamp(timestamp, start, end);
                float pivot = 2f*boundary - timestamp;
                float clamped = Mathf.Clamp(pivot, start, end);
                return RootRotation(Data.GetFrame(clamped));
            } else {
                return RootRotation(Data.GetFrame(timestamp));
            }

            Quaternion RootRotation(Frame frame) {
                if(Authoring.Topology == TOPOLOGY.Biped) {
                    Vector3 v1 = Vector3.ProjectOnPlane(frame.GetBoneTransformation(LeftHip, mirrored).GetPosition() - frame.GetBoneTransformation(RightHip, mirrored).GetPosition(), Vector3.up).normalized;
                    Vector3 v2 = Vector3.ProjectOnPlane(frame.GetBoneTransformation(LeftShoulder, mirrored).GetPosition() - frame.GetBoneTransformation(RightShoulder, mirrored).GetPosition(), Vector3.up).normalized;
                    Vector3 v = (v1+v2).normalized;
                    Vector3 forward = Vector3.ProjectOnPlane(-Vector3.Cross(v, Vector3.up), Vector3.up).normalized;
                    return forward == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(forward, Vector3.up);
                }
                if(Authoring.Topology == TOPOLOGY.Quadruped) {
                    Vector3 neck = frame.GetBoneTransformation(Neck, mirrored).GetPosition();
                    Vector3 hips = frame.GetBoneTransformation(Hips, mirrored).GetPosition();
                    Vector3 forward = Vector3.ProjectOnPlane(neck - hips, Vector3.up).normalized;;
                    return forward == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(forward.normalized, Vector3.up);
                }
                if(Authoring.Topology == TOPOLOGY.Custom) {
                    return Quaternion.LookRotation(
                        Vector3.ProjectOnPlane(Quaternion.FromToRotation(Vector3.forward, Axis.ZPositive.GetAxis()) * frame.GetBoneTransformation(Root, mirrored).GetForward(), Vector3.up).normalized, 
                        Vector3.up
                    );
                }
                return Quaternion.identity;
            }
        }
        public Quaternion GetRootRotation(Matrix4x4[] pose) {
            if(Authoring.Topology == TOPOLOGY.Biped) {
                //Data.Source.Bones[LeftHip].Name
                Vector3 v1 = Vector3.ProjectOnPlane(pose[Actor.FindBone(Data.Source.Bones[LeftHip].Name).GetIndex()].GetPosition() - pose[Actor.FindBone(Data.Source.Bones[RightHip].Name).GetIndex()].GetPosition(), Vector3.up).normalized;
                Vector3 v2 = Vector3.ProjectOnPlane(pose[Actor.FindBone(Data.Source.Bones[LeftShoulder].Name).GetIndex()].GetPosition() - pose[Actor.FindBone(Data.Source.Bones[RightShoulder].Name).GetIndex()].GetPosition(), Vector3.up).normalized;
                Vector3 v = (v1+v2).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(-Vector3.Cross(v, Vector3.up), Vector3.up).normalized;
                return forward == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(forward, Vector3.up);
            }
            if(Authoring.Topology == TOPOLOGY.Quadruped) {
                Vector3 neck = pose[Actor.FindBone(Data.Source.Bones[Neck].Name).GetIndex()].GetPosition();
                Vector3 hips = pose[Actor.FindBone(Data.Source.Bones[Hips].Name).GetIndex()].GetPosition();
                Vector3 forward = Vector3.ProjectOnPlane(neck - hips, Vector3.up).normalized;;
                return forward == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
            if(Authoring.Topology == TOPOLOGY.Custom) {
                return Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(Quaternion.FromToRotation(Vector3.forward, Axis.ZPositive.GetAxis()) * pose[Root].GetForward(), Vector3.up).normalized, 
                    Vector3.up
                );
            }
            return Quaternion.identity;
        }

        // is Pose valid?
        public bool ValidatePose(){
            if(Actor == null){
                return false;
            } else {
                return TargetPoseTransformations.Length == Actor.Bones.Length && TargetPoseVelocities.Length == Actor.Bones.Length;
            }
            
        }

        // Draw when Transformations are valid (existing)
        public void DrawTargetPose(Color color){
            if(ValidatePose() && VisualizeTargetPose) {
                Actor.Draw(TargetPoseTransformations, EnableTargetPose ? color : UltiDraw.DarkRed, Actor.JointColor, Actor.DRAW.Skeleton);
                UltiDraw.Begin();
/*                 for(int i=0; i<TargetPoseTransformations.Length; i++) {
                    UltiDraw.DrawArrow(
                        TargetPoseTransformations[i].GetPosition(),
                        TargetPoseTransformations[i].GetPosition() + TargetPoseVelocities[i] * 0.2f,
                        0.75f,
                        0.0075f,
                        0.05f,
                        UltiDraw.Orange.Opacity(0.5f)
                    );
                } */
                UltiDraw.End();
            } 
        }
        #endregion

    #region Subsolver
        public void InitSolvers() {    
            if(Authoring.Topology == TOPOLOGY.Biped) {
                Solvers = new SubsolverSetup[5];
                Solvers[0] = new SubsolverSetup(Actor, "LeftHand", "LeftArm", "LeftHand", 1f);
                Solvers[1] = new SubsolverSetup(Actor, "RightHand", "RightArm", "RightHand", 1f);
                Solvers[2] = new SubsolverSetup(Actor, "LeftFoot", "LeftUpLeg", "LeftFoot", 1f);
                Solvers[3] = new SubsolverSetup(Actor, "RightFoot", "RightUpLeg", "RightFoot", 1f);
                Solvers[4] = new SubsolverSetup(Actor, "Head", "Spine", "Head", 1f);
            }
            else if(Authoring.Topology == TOPOLOGY.Quadruped) {
                Solvers = new SubsolverSetup[0];
            }
            else {
                Solvers = new SubsolverSetup[0];
            }
        }

        public static void Solve(Actor actor, SubsolverSetup[] setups, Matrix4x4[] referencePose, UltimateIK.ACTIVATION activation, float weight) {
            for(int i=0; i<setups.Length; i++) {
                if(!setups[i].Active) { continue; }

                IK solver = IK.Create(actor.FindTransform(setups[i].Root), actor.FindTransform(setups[i].EndEffector));
                int targetIndex = actor.FindBone(setups[i].EndEffector).GetIndex();
                Matrix4x4 target = referencePose[targetIndex];
                solver.Objectives[0].SetTarget(
                    Utility.Interpolate (
                        solver.Joints[solver.Objectives[0].Joint].Transform.GetWorldMatrix(), // Ausgangspose
                        target, // Target Pose
                        weight * setups[i].Weight
                    ) // Interpolation in Frame i
                );

                solver.Activation = activation;
                solver.Solve();
            }
        }

        [Serializable]
        public class SubsolverSetup {
            public string Name;
            public string Root;
            public string EndEffector;
            public float Weight;
            public bool Active;
            public int RootIndex, EndEffectorIndex;
            public SubsolverSetup(Actor actor, string name, string root, string ee, float w) {
                Name = name;
                Root = root;
                EndEffector = ee;
                Weight = w;
                Actor.Bone r = actor.FindBone(root);
                RootIndex = r == null ? 0 : r.GetIndex();
                Actor.Bone e = actor.FindBone(ee);
                EndEffectorIndex = e == null ? 0 : e.GetIndex();
                Active = true;
            }
        }
    #endregion

        protected override void DerivedInspector() {
            #if UNITY_EDITOR
            if(ValidatePose()){
                Utility.ResetGUIColor();
                Utility.SetGUIColor(UltiDraw.LightGrey);
                using(new EditorGUILayout.VerticalScope ("Box")) {

                    EditorGUILayout.BeginHorizontal();
                    Utility.SetGUIColor(EnableTargetPose ? UltiDraw.DarkGreen : UltiDraw.DarkRed);
                    EnableTargetPose = EditorGUILayout.Toggle(EnableTargetPose, GUILayout.Width(20f));
                    if(Utility.GUIButton("Target Pose", EnableTargetPose ? UltiDraw.DarkGreen : UltiDraw.LightGrey, UltiDraw.White)) {
                        EnableTargetPose = !EnableTargetPose;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if(Utility.GUIButton("Visualize", VisualizeTargetPose ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
                        VisualizeTargetPose = !VisualizeTargetPose;
                        ApplyCharacter();
                    }
                    if(Utility.GUIButton("Mirror", Mirror ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
                        Mirror = !Mirror;
                        LoadFrame(GetCurrentFrame());
                    }
                    EditorGUILayout.EndHorizontal();
                  
                    //IKSolversInspector();
                }
            }
            MoCapInspector();
            #endif
        }

        private void IKSolversInspector() {
            #if UNITY_EDITOR
            if(Solvers.Length > 0) {
                using(new EditorGUILayout.VerticalScope ("Box")) {
                    Utility.ResetGUIColor();
                    Utility.SetGUIColor(UltiDraw.LightGrey);
                    Activation = (UltimateIK.ACTIVATION)EditorGUILayout.EnumPopup("Activation", Activation);
                    foreach(SubsolverSetup solver in Solvers) {
                        EditorGUILayout.BeginHorizontal();
                        if(Utility.GUIButton(solver.Name, solver.Active ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black, 150f)) {
                            solver.Active = !solver.Active;
                        }
                        EditorGUILayout.LabelField("Root:", GUILayout.Width(30f));
                        solver.RootIndex = EditorGUILayout.Popup(solver.RootIndex, Actor.GetBoneNames(), GUILayout.Width(125f));
                        solver.Root = Actor.Bones[solver.RootIndex].GetName();

                        EditorGUILayout.LabelField("End:", GUILayout.Width(30f));
                        solver.EndEffectorIndex = EditorGUILayout.Popup(solver.EndEffectorIndex, Actor.GetBoneNames(), GUILayout.Width(125f));
                        solver.EndEffector = Actor.Bones[solver.EndEffectorIndex].GetName();

                        solver.Weight = EditorGUILayout.Slider(solver.Weight, 0f, 1f, GUILayout.Width(125f));
                        EditorGUILayout.EndHorizontal();
                        GUILayout.FlexibleSpace();
                    }
                }
            }
            #endif
        }
        private void MoCapInspector() {
            #if UNITY_EDITOR
            //DATA SECTION
            Utility.SetGUIColor(UltiDraw.DarkGrey);
            using(new EditorGUILayout.VerticalScope ("Box")) {
                EditorGUILayout.LabelField("Motion Asset", Utility.GetFontColor(Color.white), GUILayout.Width(80f));
                Utility.SetGUIColor(UltiDraw.Mustard);
                using(new EditorGUILayout.VerticalScope ("Box")) {
                    Utility.ResetGUIColor();

                    Utility.SetGUIColor(UltiDraw.LightGrey);
                    EditorGUI.BeginChangeCheck();
                    Data = EditorGUILayout.ObjectField(Data, typeof(MotionData), true) as MotionData;
                    if(EditorGUI.EndChangeCheck()) {
                        LoadFrame(GetCurrentFrame());
                    }
                    Utility.ResetGUIColor();
                    if(Data != null){
                        EditorGUILayout.HelpBox(Data.GetParentDirectoryPath(), MessageType.None);
                    }
                }
                EditorGUILayout.LabelField("Selector", Utility.GetFontColor(Color.white), GUILayout.Width(80f));
                Utility.SetGUIColor(UltiDraw.LightGrey);
                using(new EditorGUILayout.VerticalScope ("Box")) {
                    if(Utility.GUIButton("Sync Assets", UltiDraw.DarkGrey, UltiDraw.White, 150f)) {
                        LoadMocap();
                    }
                    GUILayout.Space(5); 
                    Utility.ResetGUIColor();
                    //Selection Browser
                    int pivot = MotionEditor.FindIndex(Assets, Utility.GetAssetGUID(Data));;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    int selectIndex = EditorGUILayout.Popup(pivot, Enums);
                    
                    if(EditorGUI.EndChangeCheck()) {
                        if(selectIndex != -1) {
                            LoadData(Assets[selectIndex]);
                        }
                    }
                    EditorGUILayout.LabelField("Filter: ", Utility.GetFontColor(Color.white), GUILayout.Width(55f));
                    EditorGUI.BeginChangeCheck();
                    Filter = EditorGUILayout.TextField(Filter, GUILayout.Width(55f));
                    if(EditorGUI.EndChangeCheck()) {
                        LoadMocap();
                    }
                    if(Utility.GUIButton("<", UltiDraw.DarkGrey, UltiDraw.White, 55f)) {
                        LoadPreviousAsset();
                    }
                    if(Utility.GUIButton(">", UltiDraw.DarkGrey, UltiDraw.White, 55f)) {
                        LoadNextAsset();
                    }
                    EditorGUILayout.EndHorizontal();

                    //Slider Browser
                    EditorGUILayout.BeginHorizontal();
                    if(Assets.Length == 0) {
                        EditorGUILayout.IntSlider(0, 0, 0);
                    } else {
                        EditorGUI.BeginChangeCheck();
                        int sliderIndex = EditorGUILayout.IntSlider(pivot+1, 1, Assets.Length);
                        if(EditorGUI.EndChangeCheck()) {
                            LoadData(Assets[sliderIndex-1]);
                        }
                    }
                    EditorGUILayout.LabelField("/ " + Assets.Length, GUILayout.Width(60f));
                    EditorGUILayout.EndHorizontal();

                    if(Data != null) {
                        Frame frame = GetCurrentFrame();
                        MotionData data = Data;
                        Utility.SetGUIColor(UltiDraw.Grey);
                        using(new EditorGUILayout.VerticalScope ("Box")) {
                            Utility.ResetGUIColor();
                            Utility.SetGUIColor(UltiDraw.DarkGrey);
                            using(new EditorGUILayout.VerticalScope ("Box")) {
                                Utility.ResetGUIColor();
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.FlexibleSpace();
                                if(Utility.GUIButton("<", UltiDraw.Grey, UltiDraw.White, 20f, 40f)) {
                                    LoadFrame(Mathf.Max(frame.Timestamp - data.GetDeltaTime(), 0f));
                                }
                                if(Utility.GUIButton(">", UltiDraw.Grey, UltiDraw.White, 20f, 40f)) {
                                    LoadFrame(Mathf.Min(frame.Timestamp + data.GetDeltaTime(), data.GetTotalTime()));
                                }

                                int index = EditorGUILayout.IntSlider(frame.Index, 1, data.GetTotalFrames(), GUILayout.Height(40f));
                                if(index != frame.Index) {
                                    LoadFrame(index);
                                }
                                EditorGUILayout.BeginVertical();
                                EditorGUILayout.LabelField("/ " + data.GetTotalFrames() + " Frames ", Utility.GetFontColor(Color.white), GUILayout.Width(80f));
                                EditorGUILayout.LabelField("[" + frame.Timestamp.ToString("F2") + "s / " + data.GetTotalTime().ToString("F2") + "s]", Utility.GetFontColor(Color.white), GUILayout.Width(80f));
                                EditorGUILayout.EndVertical();
                                GUILayout.FlexibleSpace();
                                EditorGUILayout.EndHorizontal();
                            }
                        }

                        Root = EditorGUILayout.Popup("Root", Root, data.Source.GetBoneNames());
                        RightShoulder = EditorGUILayout.Popup("Right Shoulder", RightShoulder, data.Source.GetBoneNames());
                        LeftShoulder = EditorGUILayout.Popup("Left Shoulder", LeftShoulder, data.Source.GetBoneNames());
                        RightHip = EditorGUILayout.Popup("Right Hip", RightHip, data.Source.GetBoneNames());
                        LeftHip = EditorGUILayout.Popup("Left Hip", LeftHip, data.Source.GetBoneNames());
                        Neck = EditorGUILayout.Popup("Neck", Neck, data.Source.GetBoneNames());
                        Hips = EditorGUILayout.Popup("Hips", Hips, data.Source.GetBoneNames());
                    }
                }
            }
            #endif
        }
    }
}