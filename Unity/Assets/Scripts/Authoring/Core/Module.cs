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
    [Serializable]
    public abstract class Module
    {
        public enum ID {StyleAction, Betweening, Length};
        private static string[] IDs = null;
        public static string[] GetIDs() {
            if(IDs == null) {
                IDs = new string[(int)Module.ID.Length +1];
                for(int i=0; i<IDs.Length-1; i++) {
                    IDs[i] = ((Module.ID)i).ToString();
                }
            }
            return IDs;
	    }

	    [NonSerialized] public bool Inspect = true;
        [SerializeReference] public ControlPoint ControlPoint;
        public Authoring Authoring;
        public Module Initialize(ControlPoint cp) {
            ControlPoint = cp;
            Authoring = cp.Authoring;
            DerivedInitialize();
            return this;
        }

        public void Callback() {
            DerivedCallback();
        }

        public void Remove(){
            DerivedRemove();
        }
        public void GUI() {

        }

        public void Draw() {

        }

        #if UNITY_EDITOR
        public void Inspector() {
            Utility.SetGUIColor(UltiDraw.DarkGrey);
            using(new EditorGUILayout.VerticalScope ("Box")) {
                Utility.ResetGUIColor();

                Utility.SetGUIColor(UltiDraw.Mustard);
                using(new EditorGUILayout.VerticalScope ("Box")) {
                    Utility.ResetGUIColor();
                    EditorGUILayout.BeginHorizontal();
                    Inspect = EditorGUILayout.Toggle(Inspect, GUILayout.Width(20f));
                    EditorGUILayout.LabelField(GetID().ToString() + " Module");
                    GUILayout.FlexibleSpace();
                    if(Utility.GUIButton("X", UltiDraw.DarkRed, UltiDraw.White, 25f, 20f)) {
                        ControlPoint.RemoveModule(GetID());
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if(Inspect) {
                    Utility.SetGUIColor(UltiDraw.LightGrey);
                    using(new EditorGUILayout.VerticalScope ("Box")) {
                        Utility.ResetGUIColor();
                        DerivedInspector();
                    }
                }
            }
        }
        #endif

	    public abstract ID GetID();
        protected abstract void DerivedInitialize();
        protected abstract void DerivedRemove();
        protected abstract void DerivedInspector();
        protected abstract void DerivedCallback();
    }
}