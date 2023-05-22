using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace AnimationAuthoring
{
        
    [System.Serializable]
    public class AssetLoader
    {
        public string[] Folders = new string[0];
        public bool[] Imports = new bool[0];
        public string[] Assets = new string[0];

        // IMPORT
        private bool Importing = false;
        private float Progress = 0f;
        private int CurrentFile = 0;
        private int TotalFiles = 0;

        public bool IsFolderValid(int index) {
            #if UNITY_EDITOR
            return AssetDatabase.IsValidFolder(GetFolder(index));
            #else
            return false;
            #endif
        }

        public string[] GetFolders() {
            List<string> folders = new List<string>();
            for(int i=0; i<Folders.Length; i++) {
                if(IsFolderValid(i) && Imports[i]) {
                    folders.Add(GetFolder(i));
                }
            }
            return folders.ToArray();
        }

        public string GetFolder(int index) {
            return "Assets/" + Folders[index];
        }

        public IEnumerator Import() {
            Importing = true;

            string[] folders = GetFolders();
            if(folders.Length == 0) {
                Assets = new string[0];
            } else {
                #if UNITY_EDITOR
                string[] candidates = AssetDatabase.FindAssets("t:MotionData", folders);
                TotalFiles = candidates.Length;
                List<string> assets = new List<string>();
                for(int i=0; i<candidates.Length; i++) {
                    if(!Importing) {
                        break;
                    }
                    CurrentFile += 1;
                    MotionData asset = (MotionData)AssetDatabase.LoadMainAssetAtPath(Utility.GetAssetPath(candidates[i]));
                    assets.Add(candidates[i]);
                    if(i % 10 == 0) {
                        Resources.UnloadUnusedAssets();
                    }
                    Progress = (float)(i+1) / candidates.Length;
                    yield return new WaitForSeconds(0f);
                }
                if(Importing) {
                    Assets = assets.ToArray();
                }
                #else
                    yield return new WaitForSeconds(0f);
                #endif
            }

            CurrentFile = 0;
            TotalFiles = 0;
            Progress = 0f;
            Importing = false;
        }

    
        public void Inspector() {
            #if UNITY_EDITOR
            using(new EditorGUILayout.VerticalScope ("Box")) {
                using(new EditorGUILayout.VerticalScope ("Box")) {
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("[" + Assets.Length +"]" + " Assets");
                    if(GUILayout.Button("Add Folder")) {
                        ArrayExtensions.Append(ref Folders, string.Empty);
                        ArrayExtensions.Append(ref Imports, true);
                    }
                    if(GUILayout.Button("Remove Folder")) {
                        ArrayExtensions.Shrink(ref Folders);
                        ArrayExtensions.Shrink(ref Imports);
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    using(new EditorGUILayout.VerticalScope("Box")) {
                        for(int i=0; i<Folders.Length; i++) {
                            EditorGUILayout.BeginHorizontal();
                            Utility.SetGUIColor(IsFolderValid(i) ? (Imports[i] ? UltiDraw.DarkGreen : UltiDraw.Gold) : UltiDraw.DarkRed);
                            Folders[i] = EditorGUILayout.TextField(Folders[i]);
                            Imports[i] = EditorGUILayout.Toggle(Imports[i], GUILayout.Width(20f));
                            Utility.ResetGUIColor();
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    if(!Importing) {
                        if(Utility.GUIButton("Import", UltiDraw.DarkGrey, UltiDraw.White)) {
                            EditorCoroutines.StartCoroutine(Import(), this);
                        }
                    } else {
                        EditorGUI.DrawRect(new Rect(EditorGUILayout.GetControlRect().x, EditorGUILayout.GetControlRect().y, Progress * EditorGUILayout.GetControlRect().width, 25f), UltiDraw.Green.Opacity(0.75f));
                        EditorGUI.LabelField(new Rect(EditorGUILayout.GetControlRect()), CurrentFile + " / " + TotalFiles);
                        EditorGUI.BeginDisabledGroup(!Importing);
                        if(Utility.GUIButton(!Importing ? "Aborting" : "Stop", !Importing ? UltiDraw.Gold : UltiDraw.DarkRed, UltiDraw.White)) {
                            Importing = false;
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                }
            }
            #endif
        }
    }
}