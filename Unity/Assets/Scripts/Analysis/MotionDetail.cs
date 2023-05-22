#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using AI4Animation;

public class MotionDetail : MonoBehaviour {

	public int Frames = 100;
	public float LineStrength = 0.00125f;
	public UltiDraw.GUIRect Rect = new UltiDraw.GUIRect(0.5f, 0.75f, 0.5f, 0.2f);
	public Actor[] Actors = new Actor[0];
	public bool AutoYMax = true;
	public float YMax = 1f;

	public int Framerate = 30;
	public bool UseDeltaTime = false;

	private List<Matrix4x4[]> PreviousTransformations;
	private List<List<float>> Values;

	private int Index = 0;

	void Start() {
		PreviousTransformations = new List<Matrix4x4[]>();
		Values = new List<List<float>>();
		for(int i=0; i<Actors.Length; i++) {
			PreviousTransformations.Add(new Matrix4x4[Actors[i].Bones.Length]);
			for(int j=0; j<PreviousTransformations[i].Length; j++) {
				PreviousTransformations[i][j] = Matrix4x4.identity;
			}
			Values.Add(new List<float>());
		}
		//Utility.SetFPS(Framerate);
	}

	void LateUpdate () {
		int index = Index;
		// TODO: For now, only analysis main inspected character
/*		Index = MotionEditor.GetInstance().GetCurrentFrame().Index;
		bool skip = index > Index;
		for(int i=0; i<Actors.Length; i++) {
			float value = 0f;
			for(int j=0; j<Actors[i].Bones.Length; j++) {
				Matrix4x4 transformation = Actors[i].Bones[j].GetTransform().GetLocalMatrix();
				value += Quaternion.Angle(PreviousTransformations[i][j].GetRotation(), transformation.GetRotation());;
				PreviousTransformations[i][j] = transformation;
			}
			value /= Actors[i].Bones.Length;
			value *= UseDeltaTime ? (1f/Time.deltaTime) : Framerate;
			if(!skip) {
				Values[i].Add(value);
			}
			while(Values[i].Count > Frames) {
				Values[i].RemoveAt(0);
			} 
		} */
		for(int i=0; i<Actors.Length; i++) {
			float value = 0f;
			for(int j=0; j<Actors[i].Bones.Length; j++) {
				Matrix4x4 transformation = Actors[i].Bones[j].GetTransform().GetLocalMatrix();
				value += Quaternion.Angle(PreviousTransformations[i][j].GetRotation(), transformation.GetRotation());;
				PreviousTransformations[i][j] = transformation;
			}
			value /= Actors[i].Bones.Length;
			value *= UseDeltaTime ? (1f/Time.deltaTime) : Framerate;
			
			Values[i].Add(value);
			
/* 			while(Values[i].Count > Frames) {
				Values[i].RemoveAt(0);
			}  */
		} 
	}

	public float[][] GetValues() {
		float[][] values = new float[Values.Count][];
		for(int i=0; i<values.Length; i++) {
			values[i] = Values[i].ToArray();
		}
		return values;
	}

	public float[] GetValues(int actor) {
		return Values[actor].ToArray();
	}

	void OnRenderObject() {
		UltiDraw.Begin();
		UltiDraw.PlotFunctions(Rect.GetCenter(), Rect.GetSize(), GetValues(), UltiDraw.Dimension.X, thickness:LineStrength);
		UltiDraw.End();
	}

	void OnGUI() {
		float size = 0.05f;
		UltiDraw.Begin();
		float[][] values = GetValues();
		for(int i=0; i<Actors.Length; i++) {
			float mean = values[i].Mean();
			float sigma = values[i].Sigma();
			UltiDraw.OnGUILabel(new Vector2(Rect.X - 0.5f * Rect.W, Rect.Y - 0.5f*Rect.H - (i+1)*size), Rect.GetSize(), size/2f, Utility.Round(mean, 1).ToString(), UltiDraw.Red);
			UltiDraw.OnGUILabel(new Vector2(Rect.X, Rect.Y - 0.5f*Rect.H - (i+1)*size), Rect.GetSize(), size/2f, Actors[i].name, UltiDraw.Black);
			UltiDraw.OnGUILabel(new Vector2(Rect.X + 0.5f * Rect.W, Rect.Y - 0.5f*Rect.H - (i+1)*size), Rect.GetSize(), size/2f, Utility.Round(sigma, 1).ToString(), UltiDraw.Red);
		}
		UltiDraw.End();
	}
}
#endif