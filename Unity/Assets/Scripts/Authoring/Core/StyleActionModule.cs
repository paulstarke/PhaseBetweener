using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateIK;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace AnimationAuthoring
{
    [System.Serializable]
    public class StyleActionModule : Module
    {
        public List<Style> Styles = new List<Style>();

    	public override ID GetID() 
        {
		    return ID.StyleAction;
	    }
        protected override void DerivedInitialize()
        {
            SetStyles(Authoring.StyleInfos);
        }
        protected override void DerivedRemove()
        {

        }
    
        public List<Style> GetStyles()
        {
            return Styles;
        }
        public void SetStyles(List<Style> styles)
        {
            List<Style> newStyles = new List<Style>();
            foreach(Style s in styles){
                newStyles.Add(new Style(s.Name, s.Color));
            } 
            for(int i=0; i < GetStyles().Count; i++) {
                newStyles[i].Value = GetStyles()[i].Value;
            }
            Styles = newStyles;
        }
        public void RemoveStyle(int index)
        {
            Styles.RemoveAt(index);
        }
        public float[] GetStyleValues()
        {
            float[] f = new float[GetStyles().Count];
            for (int i = 0; i < f.Length; i++)
            {
                f[i] = GetStyles()[i].Value;
            }
            return f;
        }
        public Color GetMixedColor()
        {
            Color color = new Color(0,0,0,1);
            foreach (Style style in GetStyles())
            {   
                color += style.Color * style.Value;
            }
            color.r = Mathf.Clamp(color.r, 0f, 1f);
            color.b = Mathf.Clamp(color.b, 0f, 1f);
            color.g = Mathf.Clamp(color.g, 0f, 1f);
            color.a = 1f;
            return color;
        }

        protected override void DerivedCallback() {
            SetStyles(Authoring.StyleInfos);
        }

        
        protected override void DerivedInspector() {
        #if UNITY_EDITOR
            EditorGUILayout.LabelField("Styles [" + GetStyles().Count + "]", Utility.GetFontColor(Color.white), GUILayout.Width(80f));

            foreach(Style style in GetStyles()) {
                style.Value = EditorGUILayout.Slider(style.Name, style.Value, 0f, 1f);
            }
        #endif
        }
        
    }
}