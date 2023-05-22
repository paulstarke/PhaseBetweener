using UnityEngine;
using ONNX;
#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class NeuralONNXAnimation : MonoBehaviour {

	public ONNXNetwork NeuralNetwork = null;
	public Actor Actor;

	public float InferenceTime {get; private set;}
	public float Framerate = 30f;

	protected abstract void Setup();
	protected abstract void Feed();
	protected abstract void Read();
	protected abstract void OnGUIDerived();
	protected abstract void OnRenderObjectDerived();

	void Reset() {
		//NeuralNetwork = GetComponent<ONNXNetwork>();
		Actor = GetComponent<Actor>();
	}
    void Awake() {
        //Create a new inference session before running the network at each frame.
        NeuralNetwork.CreateSession();
    }
    void OnDestroy() {
        //Close the session which disposes allocated memory.
        NeuralNetwork.CloseSession();
    }
    void Start() {
		Actor = GetComponent<Actor>();
		Setup();
    }

	public virtual void  Update() {
		System.DateTime t = Utility.GetTimestamp();
		Utility.SetFPS(Mathf.RoundToInt(Framerate));
		if(Time.time < 1f) return;
		if(NeuralNetwork != null) {
			Feed();
			//Run the inference.
            NeuralNetwork.RunSession();
			Read();
		}
		InferenceTime = (float)Utility.GetElapsedTime(t);
	}

    void OnGUI() {
		if(NeuralNetwork != null) {
			OnGUIDerived();
		}
    }

	void OnRenderObject() {
		if(NeuralNetwork != null && Application.isPlaying) {
			OnRenderObjectDerived();
		}
	}

	#if UNITY_EDITOR
	[CustomEditor(typeof(NeuralONNXAnimation), true)]
	public class NeuralONNXAnimation_Editor : Editor {

		public NeuralONNXAnimation Target;

		void Awake() {
			Target = (NeuralONNXAnimation)target;
		}

		public override void OnInspectorGUI() {
			Undo.RecordObject(Target, Target.name);

			DrawDefaultInspector();

			EditorGUILayout.HelpBox("Inference Time: " + 1000f*Target.InferenceTime + "ms", MessageType.None);

			if(GUI.changed) {
				EditorUtility.SetDirty(Target);
			}
		}

	}
	#endif

}