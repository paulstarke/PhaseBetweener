using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AnimationAuthoring
{
        
    [System.Serializable]
    public class Style 
    {
        public string Name;
        [HideInInspector] public float Value;
        public Color Color;
        public Style(string name, Color color){
            Name = name;
            Color = color;
        }

        #if UNITY_EDITOR
        public void Inspector() {
            EditorGUILayout.BeginVertical();
            Value = EditorGUILayout.Slider(Name, Value, 0f, 1f);
            Name = EditorGUILayout.TextField("Name", Name);
            Color = EditorGUILayout.ColorField("Color", Color);  
            EditorGUILayout.EndVertical();
            
        }
        #endif
    }
}