
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Globalization;
using System.Threading;
public class InBetweeningModule : Module {

	public SamplePose FuturePose;
	public GameObject TargetActor;
	public SamplePose PastPose;
	public Channel[] Channels = new Channel[0];
	public TextAsset RegularTextAsset;
	public TextAsset MirroredTextAsset;
	public int ChannelCount = 5;
	[NonSerialized] private bool ShowParameters = false;
	[NonSerialized] private bool ShowNormalized = false;
	[NonSerialized] private bool DrawSampledFutureTargetPose = true;
	[NonSerialized] private bool DrawPhaseSpace = true;
	[NonSerialized] private bool DrawPivot = true;
	public override ID GetID() {
		return ID.InBetweening;
	}

	public override void DerivedResetPrecomputation() {

	}

	public override ComponentSeries DerivedExtractSeries(TimeSeries global, float timestamp, bool mirrored) {
		DeepPhaseSeries instance = new DeepPhaseSeries(global, Channels.Length);
		for(int i=0; i<instance.Samples.Length; i++) {
			instance.Phases[i] = GetPhaseVectors(timestamp + instance.Samples[i].Timestamp, mirrored);
			instance.Amplitudes[i] = GetAmplitudes(timestamp + instance.Samples[i].Timestamp, mirrored);
			instance.Frequencies[i] = GetFrequencies(timestamp + instance.Samples[i].Timestamp, mirrored);
		}
		// instance.Phases = GetPhaseVectors(timestamp, mirrored);
		// instance.Amplitudes = GetAmplitudes(timestamp, mirrored);
		// instance.Frequencies = GetFrequencies(timestamp, mirrored);
		// instance.ComputeAlignment();
		instance.DrawScene = DrawPhaseSpace;
		instance.DrawGUI = DrawPhaseSpace;
		return instance;
	}

	protected override void DerivedInitialize() {

	}

	protected override void DerivedLoad(MotionEditor editor) {
		
	}

	protected override void DerivedCallback(MotionEditor editor) {

	}

	protected override void DerivedGUI(MotionEditor editor) {

	}

	protected override void DerivedDraw(MotionEditor editor) {
		if(!DrawSampledFutureTargetPose) return;
		//FuturePose = SampleFuturePose(editor.GetTimestamp(), editor.Mirror, editor.GetActor().GetBoneNames(), minFrames: 60, maxFrames: 60);
		if(FuturePose != null) {
			editor.GetActor().Draw(FuturePose.Pose, UltiDraw.Green.Darken(0.5f), editor.GetActor().JointColor, Actor.DRAW.Skeleton);
		} else editor.GetActor().Draw(SampleFuturePose(editor.GetTimestamp(), editor.Mirror, editor.GetActor().GetBoneNames(), minFrames: 1, maxFrames: 60).Pose, UltiDraw.Green.Darken(0.5f), editor.GetActor().JointColor, Actor.DRAW.Skeleton);
/* 		if(PastPose != null) {
			editor.GetActor().Draw(PastPose.Pose, UltiDraw.Purple, editor.GetActor().JointColor, Actor.DRAW.Skeleton);
		} else editor.GetActor().Draw(SampleFuturePose(editor.GetTimestamp(), editor.Mirror, editor.GetActor().GetBoneNames(), minFrames: -30, maxFrames: -1).Pose, UltiDraw.Purple, editor.GetActor().JointColor, Actor.DRAW.Skeleton); */
/* 		UltiDraw.Begin();
		for(int i=0; i<FuturePose.Velocities.Length; i++) {
				UltiDraw.DrawArrow(
					FuturePose.Pose[i].GetPosition(),
					FuturePose.Pose[i].GetPosition() + FuturePose.Velocities[i] * 0.2f,
					0.75f,
					0.0075f,
					0.05f,
					UltiDraw.Orange.Opacity(0.5f)
				);
		} 
		UltiDraw.End(); */
	}

	private void LoadPhases(TextAsset textAsset, Channel[] channels, bool isMirrored) {
		Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

		string[][] matrix = Utility.LoadTxt(textAsset);
		
		if(!isMirrored) {
			for(int i=0; i < Data.Frames.Length; i++){
				for(int j=0; j < channels.Length; j++){
					channels[j].RegularPhaseValues[i] = float.Parse(matrix[i][j + 0 * channels.Length]);
					channels[j].RegularFrequencies[i] = float.Parse(matrix[i][j + 1 * channels.Length]);
					channels[j].RegularAmplitudes[i] = float.Parse(matrix[i][j + 2 * channels.Length]);
					channels[j].RegularOffsets[i] = float.Parse(matrix[i][j + 3 * channels.Length]);
				}
			}
		}
		if(isMirrored) {
			for(int i=0; i < Data.Frames.Length; i++){
				for(int j=0; j < channels.Length; j++){
					channels[j].MirroredPhaseValues[i] = float.Parse(matrix[i][j + 0 * channels.Length]);
					channels[j].MirroredFrequencies[i] = float.Parse(matrix[i][j + 1 * channels.Length]);
					channels[j].MirroredAmplitudes[i] = float.Parse(matrix[i][j + 2 * channels.Length]);
					channels[j].MirroredOffsets[i] = float.Parse(matrix[i][j + 3 * channels.Length]);
				}
			}
		}
	}
	
	public void ImportPhases(){
		CreateChannels(ChannelCount);
		if(MirroredTextAsset != null){
			LoadPhases(MirroredTextAsset, Channels, true);
		}
		if(RegularTextAsset != null){
			LoadPhases(RegularTextAsset, Channels, false);
		}
	}

	protected override void DerivedInspector(MotionEditor editor) {
		#if UNITY_EDITOR
		using(new EditorGUILayout.VerticalScope ("Box")) {
			DrawSampledFutureTargetPose = EditorGUILayout.Toggle("Show Sampled Target Pose", DrawSampledFutureTargetPose);
		}
		using(new EditorGUILayout.VerticalScope ("Box")) {
			TargetActor = EditorGUILayout.ObjectField("Target Actor", TargetActor, typeof(GameObject), true) as GameObject;
			MirroredTextAsset = EditorGUILayout.ObjectField("Mirrored Text Asset", MirroredTextAsset, typeof(TextAsset), true) as TextAsset;
			RegularTextAsset = EditorGUILayout.ObjectField("Regular Text Asset", RegularTextAsset, typeof(TextAsset), true) as TextAsset;

			ChannelCount = EditorGUILayout.IntField("Channels", ChannelCount);
			if(Utility.GUIButton("Import Phases", UltiDraw.BlackGrey, UltiDraw.White)){
				ImportPhases();
			}

			ShowParameters = EditorGUILayout.Toggle("Show Parameters", ShowParameters);
			ShowNormalized = EditorGUILayout.Toggle("Show Normalized", ShowNormalized);
			DrawPhaseSpace = EditorGUILayout.Toggle("Draw Phase Space", DrawPhaseSpace);
			DrawPivot = EditorGUILayout.Toggle("Draw Pivot", DrawPivot);

			Vector3Int view = editor.GetView();
			float height = 50f;
			float min = -1f;
			float max = 1f;
			float maxAmplitude = 1f;
			float maxFrequency = editor.GetTimeSeries().MaximumFrequency;
			float maxOffset = 1f;
			if(ShowNormalized) {
				maxAmplitude = 0f;
				foreach(Channel c in Channels) {
					maxAmplitude = Mathf.Max(maxAmplitude, (editor.Mirror ? c.MirroredAmplitudes : c.RegularAmplitudes).Max());
				}
			}
			for(int i=0; i<Channels.Length; i++) {
				Channel c = Channels[i];
				EditorGUILayout.BeginHorizontal();

				EditorGUILayout.BeginVertical(GUILayout.Height(height));
				Rect ctrl = EditorGUILayout.GetControlRect();
				Rect rect = new Rect(ctrl.x, ctrl.y, ctrl.width, height);
				EditorGUI.DrawRect(rect, UltiDraw.Black);

				UltiDraw.Begin();

				Vector3 prevPos = Vector3.zero;
				Vector3 newPos = Vector3.zero;
				Vector3 bottom = new Vector3(0f, rect.yMax, 0f);
				Vector3 top = new Vector3(0f, rect.yMax - rect.height, 0f);

				//Zero
				{
					prevPos.x = rect.xMin;
					prevPos.y = rect.yMax - (0f).Normalize(min, max, 0f, 1f) * rect.height;
					newPos.x = rect.xMin + rect.width;
					newPos.y = rect.yMax - (0f).Normalize(min, max, 0f, 1f) * rect.height;
					UltiDraw.DrawLine(prevPos, newPos, UltiDraw.Magenta.Opacity(0.5f));
				}

				//Phase 1D
				for(int j=0; j<view.z; j++) {
					prevPos.x = rect.xMin + (float)(j)/(view.z-1) * rect.width;
					prevPos.y = rect.yMax;
					newPos.x = rect.xMin + (float)(j)/(view.z-1) * rect.width;
					newPos.y = rect.yMax - c.GetPhaseValue(Data.GetFrame(view.x+j).Timestamp, editor.Mirror) * rect.height;
					float weight = c.GetAmplitude(Data.GetFrame(view.x+j).Timestamp, editor.Mirror).Normalize(0f, maxAmplitude, 0f, 1f);
					UltiDraw.DrawLine(prevPos, newPos, UltiDraw.Cyan.Opacity(weight));
				}

				//Phase 2D X
				for(int j=1; j<view.z; j++) {
					prevPos.x = rect.xMin + (float)(j-1)/(view.z-1) * rect.width;
					prevPos.y = rect.yMax - (float)c.GetPhaseVector(Data.GetFrame(view.x+j-1).Timestamp, editor.Mirror).x.Normalize(-1f, 1f, 0f, 1f) * rect.height;
					newPos.x = rect.xMin + (float)(j)/(view.z-1) * rect.width;
					newPos.y = rect.yMax - (float)c.GetPhaseVector(Data.GetFrame(view.x+j).Timestamp, editor.Mirror).x.Normalize(-1f, 1f, 0f, 1f) * rect.height;
					float weight = c.GetAmplitude(Data.GetFrame(view.x+j).Timestamp, editor.Mirror).Normalize(0f, maxAmplitude, 0f, 1f);
					// UltiDraw.DrawLine(prevPos, newPos, UltiDraw.Orange.Opacity(weight));
					UltiDraw.DrawLine(prevPos, newPos, UltiDraw.White.Opacity(weight));
				}
				//Phase 2D Y
				for(int j=1; j<view.z; j++) {
					prevPos.x = rect.xMin + (float)(j-1)/(view.z-1) * rect.width;
					prevPos.y = rect.yMax - (float)c.GetPhaseVector(Data.GetFrame(view.x+j-1).Timestamp, editor.Mirror).y.Normalize(-1f, 1f, 0f, 1f) * rect.height;
					newPos.x = rect.xMin + (float)(j)/(view.z-1) * rect.width;
					newPos.y = rect.yMax - (float)c.GetPhaseVector(Data.GetFrame(view.x+j).Timestamp, editor.Mirror).y.Normalize(-1f, 1f, 0f, 1f) * rect.height;
					float weight = c.GetAmplitude(Data.GetFrame(view.x+j).Timestamp, editor.Mirror).Normalize(0f, maxAmplitude, 0f, 1f);
					// UltiDraw.DrawLine(prevPos, newPos, UltiDraw.Magenta.Opacity(weight));
					UltiDraw.DrawLine(prevPos, newPos, UltiDraw.White.Opacity(weight));
				}

				UltiDraw.End();

				if(DrawPivot) {
					editor.DrawPivot(rect);
					//editor.DrawWindow(editor.GetCurrentFrame(), 1f/Channels[i].GetFrequency(editor.GetTimestamp(), editor.Mirror), Color.green.Opacity(0.25f), rect);
				}

				EditorGUILayout.EndVertical();

				EditorGUILayout.EndHorizontal();

				// EditorGUILayout.HelpBox(Channels[i].GetFrequency(editor.GetTimestamp(), editor.Mirror).ToString(), MessageType.None, true);
			}
			if(ShowParameters) {
				foreach(Channel c in Channels) {
					EditorGUILayout.HelpBox(c.GetManifoldVector(editor.GetTimestamp(), editor.Mirror).ToString("F3") + " / " + c.GetFrequency(editor.GetTimestamp(), editor.Mirror).ToString("F3"), MessageType.None);
				}
				{
					EditorGUILayout.BeginHorizontal();

					EditorGUILayout.BeginVertical(GUILayout.Height(height));
					Rect ctrl = EditorGUILayout.GetControlRect();
					Rect rect = new Rect(ctrl.x, ctrl.y, ctrl.width, height);
					EditorGUI.DrawRect(rect, UltiDraw.Black);

					UltiDraw.Begin();

					Vector3 prevPos = Vector3.zero;
					Vector3 newPos = Vector3.zero;
					Vector3 bottom = new Vector3(0f, rect.yMax, 0f);
					Vector3 top = new Vector3(0f, rect.yMax - rect.height, 0f);

					for(int i=0; i<Channels.Length; i++) {
						Channel c = Channels[i];
						for(int j=1; j<view.z; j++) {
							prevPos.x = rect.xMin + (float)(j-1)/(view.z-1) * rect.width;
							prevPos.y = rect.yMax - (float)c.GetAmplitude(Data.GetFrame(view.x+j-1).Timestamp, editor.Mirror).Normalize(0f, maxAmplitude, 0f, 1f) * rect.height;
							newPos.x = rect.xMin + (float)(j)/(view.z-1) * rect.width;
							newPos.y = rect.yMax - (float)c.GetAmplitude(Data.GetFrame(view.x+j).Timestamp, editor.Mirror).Normalize(0f, maxAmplitude, 0f, 1f) * rect.height;
							UltiDraw.DrawLine(prevPos, newPos, UltiDraw.GetRainbowColor(i, Channels.Length));
						}
					}

					UltiDraw.End();

					if(DrawPivot) {
						editor.DrawPivot(rect);
					}
					
					EditorGUILayout.EndVertical();

					EditorGUILayout.EndHorizontal();
				}
				{
					EditorGUILayout.BeginHorizontal();

					EditorGUILayout.BeginVertical(GUILayout.Height(height));
					Rect ctrl = EditorGUILayout.GetControlRect();
					Rect rect = new Rect(ctrl.x, ctrl.y, ctrl.width, height);
					EditorGUI.DrawRect(rect, UltiDraw.Black);

					UltiDraw.Begin();

					Vector3 prevPos = Vector3.zero;
					Vector3 newPos = Vector3.zero;
					Vector3 bottom = new Vector3(0f, rect.yMax, 0f);
					Vector3 top = new Vector3(0f, rect.yMax - rect.height, 0f);

					for(int i=0; i<Channels.Length; i++) {
						Channel c = Channels[i];
						for(int j=1; j<view.z; j++) {
							prevPos.x = rect.xMin + (float)(j-1)/(view.z-1) * rect.width;
							prevPos.y = rect.yMax - (float)c.GetFrequency(Data.GetFrame(view.x+j-1).Timestamp, editor.Mirror).Normalize(0f, maxFrequency, 0f, 1f) * rect.height;
							newPos.x = rect.xMin + (float)(j)/(view.z-1) * rect.width;
							newPos.y = rect.yMax - (float)c.GetFrequency(Data.GetFrame(view.x+j).Timestamp, editor.Mirror).Normalize(0f, maxFrequency, 0f, 1f) * rect.height;
							UltiDraw.DrawLine(prevPos, newPos, UltiDraw.GetRainbowColor(i, Channels.Length));
						}
					}

					UltiDraw.End();

					if(DrawPivot) {
						editor.DrawPivot(rect);
					}
					
					EditorGUILayout.EndVertical();

					EditorGUILayout.EndHorizontal();
				}
				{
					EditorGUILayout.BeginHorizontal();

					EditorGUILayout.BeginVertical(GUILayout.Height(height));
					Rect ctrl = EditorGUILayout.GetControlRect();
					Rect rect = new Rect(ctrl.x, ctrl.y, ctrl.width, height);
					EditorGUI.DrawRect(rect, UltiDraw.Black);

					UltiDraw.Begin();

					Vector3 prevPos = Vector3.zero;
					Vector3 newPos = Vector3.zero;
					Vector3 bottom = new Vector3(0f, rect.yMax, 0f);
					Vector3 top = new Vector3(0f, rect.yMax - rect.height, 0f);

					for(int i=0; i<Channels.Length; i++) {
						Channel c = Channels[i];
						for(int j=1; j<view.z; j++) {
							prevPos.x = rect.xMin + (float)(j-1)/(view.z-1) * rect.width;
							prevPos.y = rect.yMax - (float)c.GetOffset(Data.GetFrame(view.x+j-1).Timestamp, editor.Mirror).Normalize(-maxOffset, maxOffset, 0f, 1f) * rect.height;
							newPos.x = rect.xMin + (float)(j)/(view.z-1) * rect.width;
							newPos.y = rect.yMax - (float)c.GetOffset(Data.GetFrame(view.x+j).Timestamp, editor.Mirror).Normalize(-maxOffset, maxOffset, 0f, 1f) * rect.height;
							UltiDraw.DrawLine(prevPos, newPos, UltiDraw.GetRainbowColor(i, Channels.Length));
						}
					}

					UltiDraw.End();

					if(DrawPivot) {
						editor.DrawPivot(rect);
					}
					
					EditorGUILayout.EndVertical();

					EditorGUILayout.EndHorizontal();
				}
			}
		}
		#endif
	}
	
    public SamplePose SampleFuturePose(float timestamp, bool mirrored, string[] targetBones, int minFrames = 1, int maxFrames = 60, float sampleOffset = 0f) {
        // if sampleOffset 0 => sample random
        if(sampleOffset == 0f) {
            sampleOffset = UnityEngine.Random.Range((float)(minFrames * Data.GetDeltaTime()), (float)(maxFrames * Data.GetDeltaTime()));
        }
		Frame frame = Data.GetFrame(timestamp + sampleOffset);
		
        SamplePose sample = new SamplePose(
			frame.GetBoneTransformations(targetBones, mirrored),
		 	frame.GetBoneVelocities(targetBones, mirrored),
			sampleOffset);
        return sample;
    }

	public static SamplePose SampleFuturePose(MotionData asset, float timestamp, bool mirrored, string[] targetBones, int minFrames = 15, int maxFrames = 60, float sampleOffset = 0f) {
        // if sampleOffset 0 => sample random
        if(sampleOffset == 0f) {
            sampleOffset = UnityEngine.Random.Range((float)(minFrames * asset.GetDeltaTime()), (float)(maxFrames * asset.GetDeltaTime()));
        }
		Frame frame = asset.GetFrame(timestamp + sampleOffset);
		
        SamplePose sample = new SamplePose(
			frame.GetBoneTransformations(targetBones, mirrored),
		 	frame.GetBoneVelocities(targetBones, mirrored),
			sampleOffset);
        return sample;
    }

    public class SamplePose {
        public Matrix4x4[] Pose;
		public Vector3[] Velocities;
        public float TimeOffset;

        public SamplePose(Matrix4x4[] pose, Vector3[] velocities, float offset) {
            Pose = pose;
			Velocities = velocities;
            TimeOffset = offset;
        }
    }

	public Vector2[] GetManifold(float timestamp, bool mirrored, TimeSeries timeSeries) {
		Vector2[] values = new Vector2[timeSeries.KeyCount * Channels.Length];
		int pivot = 0;
		for(int i=0; i<timeSeries.KeyCount; i++) {
			for(int j=0; j<Channels.Length; j++) {
				values[pivot] = Channels[j].GetManifoldVector(timestamp + timeSeries.GetKey(i).Timestamp, mirrored); pivot += 1;
			}
		}
		return values;
	}

	public Vector2[] GetManifold(float timestamp, bool mirrored) {
		Vector2[] values = new Vector2[Channels.Length];
		for(int i=0; i<values.Length; i++) {
			values[i] = Channels[i].GetManifoldVector(timestamp, mirrored);
		}
		return values;
	}

	public Vector2[] GetPhaseVectors(float timestamp, bool mirrored) {
		Vector2[] values = new Vector2[Channels.Length];
		for(int i=0; i<values.Length; i++) {
			values[i] = Channels[i].GetPhaseVector(timestamp, mirrored);
		}
		return values;
	}

	public float[] GetFrequencies(float timestamp, bool mirrored) {
		float[] values = new float[Channels.Length];
		for(int i=0; i<values.Length; i++) {
			values[i] = Channels[i].GetFrequency(timestamp, mirrored);
		}
		return values;
	}

	public float[] GetAmplitudes(float timestamp, bool mirrored) {
		float[] values = new float[Channels.Length];
		for(int i=0; i<values.Length; i++) {
			values[i] = Channels[i].GetAmplitude(timestamp, mirrored);
		}
		return values;
	}

	public Channel[] CreateChannels(int length) {
		Channels = new Channel[length];
		for(int i=0; i<Channels.Length; i++) {
			Channels[i] = new Channel(this);
		}
		Data.MarkDirty(true, false);
		return Channels;
	}

	public bool VerifyChannels(int length) {
		if(Channels.Length != length || Channels.Any(null)) {
			// Debug.Log("Recreating phase channels in asset: " + Asset.name);
			return false;
		}
		foreach(Channel channel in Channels) {
			if(new object[]{
				channel.RegularPhaseValues,
				channel.RegularFrequencies,
				channel.RegularAmplitudes,
				channel.RegularOffsets,
				channel.MirroredPhaseValues,
				channel.MirroredFrequencies,
				channel.MirroredAmplitudes,
				channel.MirroredOffsets
			}.Any(null)) {
				// Debug.Log("Recreating phase channels in asset: " + Asset.name);
				return false;
			}
		}
		return true;
	}
	[Serializable]
	public class Channel {
		public InBetweeningModule Module;

		public float[] RegularPhaseValues;
		public float[] RegularFrequencies;
		public float[] RegularAmplitudes;
		public float[] RegularOffsets;

		public float[] MirroredPhaseValues;
		public float[] MirroredFrequencies;
		public float[] MirroredAmplitudes;
		public float[] MirroredOffsets;

		public Channel(InBetweeningModule module) {
			Module = module;

			RegularPhaseValues = new float[module.Data.Frames.Length];
			RegularFrequencies = new float[module.Data.Frames.Length];
			RegularAmplitudes = new float[module.Data.Frames.Length];
			RegularOffsets = new float[module.Data.Frames.Length];

			MirroredPhaseValues = new float[module.Data.Frames.Length];
			MirroredFrequencies = new float[module.Data.Frames.Length];
			MirroredAmplitudes = new float[module.Data.Frames.Length];
			MirroredOffsets = new float[module.Data.Frames.Length];
		}

		public Vector2 GetManifoldVector(float timestamp, bool mirrored) {
			return GetAmplitude(timestamp, mirrored) * GetPhaseVector(timestamp, mirrored);
		}

		public Vector2 GetPhaseVector(float timestamp, bool mirrored) {
			float start = Module.Data.Frames.First().Timestamp;
			float end = Module.Data.Frames.Last().Timestamp;
			if(timestamp < start || timestamp > end) {
				float boundary = Mathf.Clamp(timestamp, start, end);
				float pivot = 2f*boundary - timestamp;
				float repeated = Mathf.Repeat(pivot-start, end-start) + start;
				return Utility.PhaseVector(GetPhaseValue(timestamp, mirrored));
			} else {
				return Utility.PhaseVector(GetPhaseValue(timestamp, mirrored));
			}
		}

		public float GetPhaseValue(float timestamp, bool mirrored) {
			float start = Module.Data.Frames.First().Timestamp;
			float end = Module.Data.Frames.Last().Timestamp;
			if(timestamp < start || timestamp > end) {
				float boundary = Mathf.Clamp(timestamp, start, end);
				float pivot = 2f*boundary - timestamp;
				float repeated = Mathf.Repeat(pivot-start, end-start) + start;
				return
				Mathf.Repeat(
					PhaseValue(boundary, mirrored) -
					Utility.SignedPhaseUpdate(
						PhaseValue(boundary, mirrored),
						PhaseValue(repeated, mirrored)
					), 1f
				);
			} else {
				return PhaseValue(timestamp, mirrored);
			}
		}

		public float GetFrequency(float timestamp, bool mirrored) {
			float start = Module.Data.Frames.First().Timestamp;
			float end = Module.Data.Frames.Last().Timestamp;
			if(timestamp < start || timestamp > end) {
				float boundary = Mathf.Clamp(timestamp, start, end);
				float pivot = 2f*boundary - timestamp;
				float repeated = Mathf.Repeat(pivot-start, end-start) + start;
				return Frequency(repeated, mirrored);
			} else {
				return Frequency(timestamp, mirrored);
			}
		}

		public float GetAmplitude(float timestamp, bool mirrored) {
			float start = Module.Data.Frames.First().Timestamp;
			float end = Module.Data.Frames.Last().Timestamp;
			if(timestamp < start || timestamp > end) {
				float boundary = Mathf.Clamp(timestamp, start, end);
				float pivot = 2f*boundary - timestamp;
				float repeated = Mathf.Repeat(pivot-start, end-start) + start;
				return Amplitude(repeated, mirrored);
			} else {
				return Amplitude(timestamp, mirrored);
			}
		}

		public float GetOffset(float timestamp, bool mirrored) {
			float start = Module.Data.Frames.First().Timestamp;
			float end = Module.Data.Frames.Last().Timestamp;
			if(timestamp < start || timestamp > end) {
				float boundary = Mathf.Clamp(timestamp, start, end);
				float pivot = 2f*boundary - timestamp;
				float repeated = Mathf.Repeat(pivot-start, end-start) + start;
				return Offset(repeated, mirrored);
			} else {
				return Offset(timestamp, mirrored);
			}
		}

		// private Vector2 PhaseVector(float timestamp, bool mirrored) {
		//     return (mirrored ? MirroredPhases : RegularPhases)[Module.Asset.GetFrame(timestamp).Index-1];
		// }
		private float PhaseValue(float timestamp, bool mirrored) {
			return (mirrored ? MirroredPhaseValues : RegularPhaseValues)[Module.Data.GetFrame(timestamp).Index-1];
		}
		private float Frequency(float timestamp, bool mirrored) {
			return (mirrored ? MirroredFrequencies : RegularFrequencies)[Module.Data.GetFrame(timestamp).Index-1];
		}
		private float Amplitude(float timestamp, bool mirrored) {
			return (mirrored ? MirroredAmplitudes : RegularAmplitudes)[Module.Data.GetFrame(timestamp).Index-1];
		}
		private float Offset(float timestamp, bool mirrored) {
			return (mirrored ? MirroredOffsets : RegularOffsets)[Module.Data.GetFrame(timestamp).Index-1];
		}
	}
}