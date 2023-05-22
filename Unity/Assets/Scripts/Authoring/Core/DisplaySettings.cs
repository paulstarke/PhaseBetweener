using System.Collections;
using UnityEngine;

namespace AnimationAuthoring
{
    [CreateAssetMenu()]
    public class DisplaySettings : ScriptableObject
    {
        public enum GeometryType { Sphere, Cube, Circle };

        [Header("Appearance")]
        [Range(0,1)]
        public float controlSize = 0.1f;
        [Range(0, 0.1f)]
        public float pathThickness = 0.03f;

        [Tooltip("Should the path still be drawn when behind objects in the scene?")]
        public bool visibleBehindObjects = true;
        [Tooltip("Should the path be drawn even when the path object is not selected?")]
        public bool visibleWhenNotSelected = true;
        public GeometryType controlShape;


        [Header("Controlpoint Colours")]
        public Color control = new Color(0.95f, 0.25f, 0.25f, 0.85f);
        public Color controlHighlighted = new Color(1, 0.57f, 0.4f);
        public Color controlSelected = new Color(1f, 0f, 0f);

        [Header("Path Colours")]
        public Color segmentSelected = new Color(1, 0.6f, 0);

        [Header("Directions")]
        public Color directions = Color.yellow;
        [Range(0,1)]
        public float directionsLength = .4f;

        [Header("Target Poses")]
        public Color targetPoses = UltiDraw.Cyan;

        public static DisplaySettings Load()
        {
            
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DisplaySettings");
            if (guids.Length == 0)
            {
                Debug.LogWarning("Could not find DisplaySettings asset. Will use default settings instead.");
                return ScriptableObject.CreateInstance<DisplaySettings>();
            }
            else
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<DisplaySettings>(path);
            }
            #else
            return null;
            #endif
        }
    }
}