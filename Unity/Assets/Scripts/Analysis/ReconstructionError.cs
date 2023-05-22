#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using AI4Animation;

public class ReconstructionError : MonoBehaviour {

	public int Frames = 100;
    public InBetweeningController Controller;
    public UltiDraw.GUIRect Rect = new UltiDraw.GUIRect(0.5f, 0.75f, 0.5f, 0.2f);
    public float LineStrength = 0.00125f;
	public bool AutoYMax = true;
	public float YMax = 1f;

    private List<float> L2Pvalues;
    private List<float> L2Qvalues;
    private Actor Actor;
    private MotionData Asset;

	void Start() {
        L2Pvalues = new List<float>();
        L2Qvalues = new List<float>();
        Actor = Controller.Actor;
        Asset = Controller.Asset;
	}

	void LateUpdate () {
		float L2Pvalue = 0f;
        float L2Qvalue = 0f;
		for(int j=0; j<Actor.Bones.Length; j++) {
			Matrix4x4 transformation = Actor.Bones[j].GetTransform().GetWorldMatrix(); //GetLocalMatrix();
			Matrix4x4 transformationGT = Asset.GetFrame(Controller.GetRootTimestamp() - 1f/Controller.Framerate).GetBoneTransformation(Asset.Source.FindBone(Actor.Bones[j].GetName()).Name, Controller.Mirror);
			L2Pvalue += Mathf.Sqrt(
						Mathf.Pow((transformation.GetPosition().x - transformationGT.GetPosition().x),2.0f) +
						Mathf.Pow((transformation.GetPosition().y - transformationGT.GetPosition().y),2.0f) +
						Mathf.Pow((transformation.GetPosition().z - transformationGT.GetPosition().z),2.0f)
			);
            L2Qvalue += Mathf.Sqrt(
						Mathf.Pow((transformation.GetRotation().x - transformationGT.GetRotation().x),2.0f) +
						Mathf.Pow((transformation.GetRotation().y - transformationGT.GetRotation().y),2.0f) +
						Mathf.Pow((transformation.GetRotation().z - transformationGT.GetRotation().z),2.0f) +
                        Mathf.Pow((transformation.GetRotation().w - transformationGT.GetRotation().w),2.0f)
			);  
			//L2Qvalue += Quaternion.Angle(transformation.GetRotation(), transformationGT.GetRotation());
            //L2Qvalue += Mathf.Abs(Quaternion.Dot(transformation.GetRotation(), transformationGT.GetRotation()));
		}
		L2Pvalue /= Actor.Bones.Length;
        L2Qvalue /= Actor.Bones.Length;
		//L2Pvalue *= Framerate;

		L2Pvalues.Add(L2Pvalue);
		L2Qvalues.Add(L2Qvalue);

/* 		while(L2Pvalues.Count > Frames) {
			L2Pvalues.RemoveAt(0);
            L2Qvalues.RemoveAt(0);
		} */
	}

	public float[][] GetValues() {
		float[][] values = new float[2][];
        values[0] = L2Pvalues.ToArray();
        values[1] = L2Qvalues.ToArray();
		return values;
	}

	void OnRenderObject() {
		UltiDraw.Begin();
		UltiDraw.PlotFunctions(Rect.GetCenter(), Rect.GetSize(), GetValues(), UltiDraw.Dimension.X, lineColors: GetColors(), thickness:LineStrength);
		UltiDraw.End();

		Matrix4x4[] gtPose = new Matrix4x4[Actor.Bones.Length];
		for(int j=0; j<gtPose.Length; j++) {
			gtPose[j] = Asset.GetFrame(Controller.GetRootTimestamp() - 1f/Controller.Framerate).GetBoneTransformation(Asset.Source.FindBone(Actor.Bones[j].GetName()).Name, Controller.Mirror);
		}

		Actor.Draw(gtPose, UltiDraw.Purple, Actor.JointColor, Actor.DRAW.Skeleton);
	}

	void OnGUI() {
		float size = 0.05f;
		UltiDraw.Begin();
        UltiDraw.OnGUILabel(new Vector2(Rect.X - 0.5f * Rect.W, Rect.Y - 0.5f*Rect.H - size), Rect.GetSize(), size/2f, "L2P:" + Utility.Round(L2Pvalues.ToArray().Mean(), 1).ToString(), GetColors()[0]);
        UltiDraw.OnGUILabel(new Vector2(Rect.X + 0.5f * Rect.W, Rect.Y - 0.5f*Rect.H - size), Rect.GetSize(), size/2f, "L2Q:" + Utility.Round(L2Qvalues.ToArray().Mean(), 1).ToString(), GetColors()[1]);
		UltiDraw.End();
	}

    private Color[] GetColors() {
		return new Color[2]{UltiDraw.Red, UltiDraw.Blue};
	}
}
#endif