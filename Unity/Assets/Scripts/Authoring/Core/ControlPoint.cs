using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace AnimationAuthoring
{
    [System.Serializable]
    public class ControlPoint : Point
    {
        public LayerMask Ground = ~1;
        public Authoring Authoring;
	    [SerializeReference] public Module[] Modules = new Module[0];
        public float Speed;
        
        public ControlPoint(Authoring authoring, Vector3 pos, LayerMask layer) : base()
        {
            this.Authoring = authoring;
            this.Ground = layer;
            this.SetPosition(pos);
            this.Speed = 1f;
            AddModule(Module.ID.StyleAction);
            AddModule(Module.ID.Betweening);
        }

        public override void SetDirection(Vector3 dir)
        {
            base.SetDirection(new Vector3(dir.x, dir.y, dir.z));
            Callback();
        }

        public override void SetPosition(Vector3 pos)
        {
            base.SetPosition(Utility.ProjectGround(pos, Ground));
            Callback();
        }

        public void SetDirectionRelativeTo(Vector3 pos){
            SetDirection((pos - GetPosition()).normalized);
        }

        public void TransformFromTo(Matrix4x4 reference, Matrix4x4 target){
            SetTransformation(GetTransformation().TransformationFromTo(reference, target));
            SetPosition(GetPosition().GetPositionFromTo(reference, target));
            SetDirection(GetDirection().GetDirectionFromTo(reference, target));
        }

        public void SetSpeed(float speed)
        {
            Speed = speed;
        }
        public float GetSpeed()
        {
            return Speed;
        }

        public Color GetColor(){
            return HasModule<StyleActionModule>() ? GetModule<StyleActionModule>().GetMixedColor() : UltiDraw.Black;
        }

    #region Module
        public void AddModule(Module.ID type) {
            if(System.Array.Find(Modules, x => x.GetID() == type) != null) {
                Debug.Log("Module of type " + type + " already exists in Controlpoint " + Authoring.Path.ControlPoints.IndexOf(this) + ".");
            } else {
                switch (type)
                {
                case Module.ID.StyleAction:
                    ArrayExtensions.Append(ref Modules, (Module) new StyleActionModule().Initialize(this));
                    #if UNITY_EDITOR
                    EditorUtility.SetDirty(Authoring);
                    #endif
                    break;
                case Module.ID.Betweening:
                    if(Authoring.GetActor() != null) {
                        ArrayExtensions.Append(ref Modules, (Module) new IKModule().Initialize(this));
                        #if UNITY_EDITOR
                        EditorUtility.SetDirty(Authoring);
                        #endif
                    } else {
                        Debug.Log("Module of type " + type + " could not be added to Controlpoint " + Authoring.Path.ControlPoints.IndexOf(this) + ". Please link a actor to " + Authoring.name);
                    }
                    break;
                default:
                    break;
                }
            }
        }

        public void RemoveModule(Module.ID type) {
            Module module = System.Array.Find(Modules, x => x.GetID() == type);
            if(module == null) {
                Debug.Log("Module of type " + type + " does not exist in " + Authoring.Path.ControlPoints.IndexOf(this) + ".");
            } else {
                module.Remove();
                ArrayExtensions.RemoveAt(ref Modules, System.Array.FindIndex(Modules, x => x.GetID() == type));
            }
        }

        public void RemoveAllModules() {
            foreach(Module m in Modules) {
                m.Remove();
            }
            while(Modules.Length > 0) {
                ArrayExtensions.RemoveAt(ref Modules, 0);
            }
        }

        public bool HasModule<T>() {
            return Modules.HasType<T>();
        }

        public T GetModule<T>() {
            return Modules.FindType<T>();
        }

        public void Callback() {
            foreach(Module m in Modules) {
                m.Callback();
            }
        }

        public void GUI() {
            foreach(Module m in Modules) {
                m.GUI();
            }
        }

        public void Draw() {
            foreach(Module m in Modules) {
                m.Draw();
            }
        }
    #endregion

       
        public void Inspector() 
        {
            #if UNITY_EDITOR
            using (new EditorGUILayout.VerticalScope("Box"))
            {
                Utility.ResetGUIColor();
                // -Speed-
                SetSpeed(EditorGUILayout.Slider("Speed", GetSpeed(), 0f, 3f));
                // -Position-
                var currentPosition = GetPosition();
                var newPosition = EditorGUILayout.Vector3Field ("Position", currentPosition);
                if (newPosition != currentPosition) {
                    Undo.RecordObject (Authoring, "Move point");
                    SetPosition(newPosition);
                    Authoring.Path.UpdateAroundControlPoint(Authoring.Path.GetTimestampByControlPoint(this), Path.UPDATETYPE.NONE);
                }
                // -Layer-
                LayerMask tempMask = EditorGUILayout.MaskField("Ground", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(Ground), InternalEditorUtility.layers);
                Ground = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
                
                //MODULES
                Utility.ResetGUIColor();
                Utility.SetGUIColor(UltiDraw.White);
                int module = EditorGUILayout.Popup(0, ArrayExtensions.Concat(new string[1]{"Add Module..."}, Module.GetIDs()));
                if(module > 0) {
                    AddModule((Module.ID)(module-1));
                }
                
                foreach(Module m in Modules) {
                    m.Inspector();
                }
            }
            #endif
        }
    }
}

