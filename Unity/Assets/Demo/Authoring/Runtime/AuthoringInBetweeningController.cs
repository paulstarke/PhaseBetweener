using UnityEngine;
using ONNX;
using System;
using UltimateIK;
using Unity.Barracuda;
using System.Collections.Generic;

namespace AnimationAuthoring
{
	public class AuthoringInBetweeningController : NeuralONNXAnimation
	{
		public enum PHASES {NoPhases, LocalPhases, DeepPhases};
		public Authoring Authoring;
		public PHASES PhaseSelection = PHASES.DeepPhases;
		public bool ModelIncludesStyleLabels = false;
		[Range (0,1)] public float LerpDurationFactor = 0.25f;
		//[Range (0,0.25f)] public float SampleOffset = 0.1f;
		[Range (0,1)] public float TrajectoryControl = 0.9f;
		[Range (0,1)] public float TrajectoryCorrection = 0.9f;
		public float StartTimestamp = 0f;
		public bool LerpInputPose = true;
		public bool BlendToTargetSpace = true;
		public bool Postprocessing = true;
		public Camera Camera = null;
		public bool DrawGating = true;
		public bool DrawGUI = true;
		public bool DrawDebug = true;
		public bool ShowBiDirectional = true;

		private TimeSeries TimeSeries;
		private RootSeries RootSeries;
		private StyleSeries StyleSeries;
		private ContactSeries ContactSeries;
		private PhaseSeries PhaseSeries;
		private int Channels = 5;
		private DeepPhaseSeries DeepPhaseSeries;
		private float ContactPower = 3f;
		private float BoneContactThreshold = 0.5f;
		private float RootTimestamp = 0f;
		private float InBetweeningTime = 0f;
		private float[] TimeDeltas = null;
		private List<float[]> GatingHistory = new List<float[]>();
		private int FrameCounter = 0;
		private ControlPoint NextCP;
		private InputPoseData InputPose;
		private Matrix4x4 PredictedRoot;
		private Matrix4x4[] RootTrajectoryPrediction;
		private Matrix4x4[] TargetTrajectoryPrediction; 
		private Matrix4x4 PredictedTargetRoot;
		private Matrix4x4[] RootPosePrediction; 
		private Matrix4x4[] TargetPosePrediction; 

		// IK
		private IK LeftHandIK;
		private IK RightHandIK;
		private IK LeftFootIK;
		private IK RightFootIK;
		private Camera GetCamera()
		{
			return Camera == null ? Camera.main : Camera;
		}
		Path Path
			{
				get
				{
					return Authoring.path;
				}
		}

		protected override void Setup()
		{	
			if(Authoring == null) {
				Debug.LogWarning("Animation Authoring is not linked to " + name);
			}
			if(NeuralNetwork.Model == null) {
				Debug.LogWarning("Neural Network is not linked in " + name);
			}
			for(int i = 0; i < Path.ControlPoints.Count; i++) {
				if(!Path.ControlPoints[i].HasModule<IKModule>()) {
					Debug.LogWarning("ControlPoint " + i + " has no IK Module");
				}
			}

			TimeSeries = new TimeSeries(6, 6, 1f, 1f, 5);
			RootSeries = new RootSeries(TimeSeries, transform);
			StyleSeries = new StyleSeries(TimeSeries, new string[] { "Move", "Aim", "Crawl" }, new float[] { 1f, 0f, 0f});
			if(Authoring.StyleInfos.Count < StyleSeries.Styles.Length-1){
				Debug.LogWarning(name + " has " + (StyleSeries.Styles.Length-1) + " Styles, but " + Authoring.name + " has " + Authoring.StyleInfos.Count + ".");
			}
			string[] contactBones = new string[0];
			if(Authoring.Topology == TOPOLOGY.Biped){
				contactBones = new string[] {"Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot"};
			}
			if(Authoring.Topology == TOPOLOGY.Quadruped){
				contactBones = new string[] {"LeftHandSite", "RightHandSite", "LeftFootSite", "RightFootSite"};
			}
			ContactSeries = new ContactSeries(TimeSeries, contactBones);

			if(PhaseSelection == PHASES.LocalPhases) {
				PhaseSeries = new PhaseSeries(TimeSeries, contactBones);
			} else if(PhaseSelection == PHASES.DeepPhases) {
				DeepPhaseSeries = new DeepPhaseSeries(TimeSeries, Channels);
			}

			TimeDeltas = new float[TimeSeries.KeyCount];

			//Set Character to Start Keyframe
			Actor.transform.position = Path.Points[Path.GetPointIndex(StartTimestamp)].GetPosition();
			Actor.transform.LookAt(Path.Points[Path.GetPointIndex(StartTimestamp + Path.TimeDelta)].GetPosition());
			ControlPoint start = Path.GetControlPoint(StartTimestamp, 0);
			IKModule startKeyFrame = start.GetModule<IKModule>();
			for (int i = 0; i < Actor.Bones.Length; i++)
			{
				Actor.Bones[i].SetVelocity(startKeyFrame.TargetPoseVelocities[i]);
				Actor.Bones[i].GetTransform().position = startKeyFrame.TargetPoseTransformations[i].GetPosition();
				Actor.Bones[i].GetTransform().rotation = Quaternion.LookRotation(startKeyFrame.TargetPoseTransformations[i].GetForward(), startKeyFrame.TargetPoseTransformations[i].GetUp());
			}
			
			//Set Trajectory
			for (int i = 0; i < TimeSeries.Pivot; i++)
			{
				Point point = Path.GetPoint(StartTimestamp);
				RootSeries.SetPosition(i, point.GetPosition());
				RootSeries.SetDirection(i, point.GetDirection());
				RootSeries.SetVelocity(i, Path.CalculatePointVelocity(StartTimestamp, Path.TimeDelta));
			}

			for (int i = TimeSeries.Pivot; i < TimeSeries.Samples.Length; i++)
			{
				float timestamp = StartTimestamp + ((i - TimeSeries.Pivot) / Framerate);
				Point point = Path.GetPoint(timestamp);

				RootSeries.SetPosition(i, point.GetPosition());
				RootSeries.SetDirection(i, point.GetDirection());
				RootSeries.SetVelocity(i, Path.CalculatePointVelocity(timestamp, Path.TimeDelta));
			}

			//Set start on path
			RootTimestamp = StartTimestamp;
			FrameCounter = (int) (RootTimestamp * Framerate);
			NextCP = Path.GetControlPoint(RootTimestamp, 1);

			RootTrajectoryPrediction = new Matrix4x4[TimeSeries.FutureKeys+1];
			TargetTrajectoryPrediction = new Matrix4x4[TimeSeries.FutureKeys+1];
			RootPosePrediction = new Matrix4x4[Actor.Bones.Length];
			TargetPosePrediction = new Matrix4x4[Actor.Bones.Length];

			if(Authoring.Topology == TOPOLOGY.Biped){
				LeftHandIK = IK.Create(Actor.FindTransform("LeftShoulder"), Actor.GetBoneTransforms("LeftHand"));
				RightHandIK = IK.Create(Actor.FindTransform("RightShoulder"), Actor.GetBoneTransforms("RightHand"));
				LeftFootIK = IK.Create(Actor.FindTransform("LeftUpLeg"), Actor.GetBoneTransforms("LeftFoot"));
				RightFootIK = IK.Create(Actor.FindTransform("RightUpLeg"), Actor.GetBoneTransforms("RightFoot"));
			}
			if(Authoring.Topology == TOPOLOGY.Quadruped){
				LeftHandIK = IK.Create(Actor.FindTransform("LeftForeArm"), Actor.GetBoneTransforms("LeftHandSite"));
				RightHandIK = IK.Create(Actor.FindTransform("RightForeArm"), Actor.GetBoneTransforms("RightHandSite"));
				LeftFootIK = IK.Create(Actor.FindTransform("LeftLeg"), Actor.GetBoneTransforms("LeftFootSite"));
				RightFootIK = IK.Create(Actor.FindTransform("RightLeg"), Actor.GetBoneTransforms("RightFootSite"));
			}

			RootSeries.DrawGUI = DrawGUI;
			StyleSeries.DrawGUI = DrawGUI;
			ContactSeries.DrawGUI = DrawGUI;
			if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.DrawGUI = DrawGUI;
			if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.DrawGUI = DrawGUI;
			RootSeries.DrawScene = DrawDebug;
			StyleSeries.DrawScene = DrawDebug;
			ContactSeries.DrawScene = DrawDebug;
			if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.DrawScene = DrawDebug;
			if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.DrawScene = DrawDebug;
		}
		
		private void SetRootTimestamp(){
			FrameCounter++;
			NextCP = Path.GetControlPoint(RootTimestamp, 1);
			float deltaUpdate = (1f/Framerate) * FrameCounter;
			float tClosest = Path.GetClosestTimeOnPath(Actor.GetRoot().GetWorldMatrix().GetPosition());
			float tUpdate = deltaUpdate;
			RootTimestamp = Path.isLooping ? tUpdate % Path.GetTotalTime() : tUpdate;
		}
		private void BlendInputTarget(){
			//BLENDING STATE
			IKModule ikModule = NextCP.GetModule<IKModule>();
			float lerpDuration = LerpDurationFactor * Path.TimeInterval;
			float timeElapsed = GetDeltaTimeOffset();
			
			if (timeElapsed < lerpDuration) //0.5 * 30 = 15 samples
			{
				float weight = timeElapsed / lerpDuration;

				Matrix4x4 targetRoot = ikModule.GetTargetRoot();
				float sampleOffset = weight * Path.TimeInterval;
				Point p = Path.GetPoint(Path.GetControlPointTimestamp(RootTimestamp, 0) + sampleOffset);
				
				Matrix4x4[] linearPose = new Matrix4x4[InputPose.Transformations.Length];
				Vector3[] linearVel = new Vector3[InputPose.BoneVelocities.Length];
				for(int i = 0; i<linearPose.Length; i++) {
					linearPose[i] = Utility.Interpolate(Path.GetControlPoint(RootTimestamp, 0).GetModule<IKModule>().TargetPoseTransformations[i], ikModule.TargetPoseTransformations[i], weight);
					linearVel[i] = Vector3.Lerp(Path.GetControlPoint(RootTimestamp, 0).GetModule<IKModule>().TargetPoseVelocities[i], ikModule.TargetPoseVelocities[i], weight);
				}
				Matrix4x4 inputRoot = ikModule.GetTargetRoot(linearPose);

				for(int i = 0; i<InputPose.Transformations.Length; i++) {
					InputPose.Transformations[i] = linearPose[i].TransformationFromTo(inputRoot, p.GetTransformation());
					InputPose.BoneVelocities[i] = linearVel[i].GetDirectionFromTo(inputRoot, p.GetTransformation());
				}
				InputPose.Root = ikModule.GetTargetRoot(InputPose.Transformations);
			}
		}

		private void Control()
		{
			SetRootTimestamp();
			
			Path.GetControlPoint(RootTimestamp, 0).GetModule<IKModule>().VisualizeTargetPose = false;
			Path.GetControlPoint(RootTimestamp, 0).GetModule<IKModule>().ApplyCharacter();
			Path.GetControlPoint(RootTimestamp, 1).GetModule<IKModule>().VisualizeTargetPose = true;
			Path.GetControlPoint(RootTimestamp, 1).GetModule<IKModule>().ApplyCharacter();

			IKModule ikModule = NextCP.GetModule<IKModule>();
			InputPose = new InputPoseData(ikModule.TargetPoseTransformations, ikModule.GetTargetRoot(), ikModule.TargetPoseVelocities);
			if(LerpInputPose)
			{
				BlendInputTarget();
			}

			//Update Past
			RootSeries.Increment(0, TimeSeries.Samples.Length - 1);
			StyleSeries.Increment(0, TimeSeries.Samples.Length - 1);

			int loopIndex = 0;
			//Trajectory
			for (int i = TimeSeries.Pivot; i < TimeSeries.Samples.Length; i++)
			{
				float timestamp = RootTimestamp + (float)(loopIndex * Path.TimeDelta);
				Point point = Path.GetPoint(timestamp);

				//Root Positions
				RootSeries.SetPosition(i,
					Vector3.Lerp(
						RootSeries.GetPosition(i),
						point.GetPosition(),
						TrajectoryControl
					)
				);

				//Root Rotations
				RootSeries.SetDirection(i,
					Vector3.Slerp(
						RootSeries.GetDirection(i),
						point.GetDirection(),
						TrajectoryControl
					)
				);

				//Root Velocities
				RootSeries.SetVelocity(i,
					Vector3.Lerp(
						RootSeries.GetVelocity(i),
						Path.CalculatePointVelocity(timestamp, Path.TimeDelta),
						TrajectoryControl
					)
				);
				loopIndex += 1;
			}
			
			if(ModelIncludesStyleLabels) {
				//Action Values
				loopIndex = 0;
				for (int i = TimeSeries.Pivot; i < TimeSeries.Samples.Length; i++)
				{
					float timestamp = RootTimestamp + (float)(loopIndex * Path.TimeDelta);
					Point point = Path.GetPoint(timestamp);
				
					for (int j = 0; j < StyleSeries.Styles.Length; j++)
					{
						StyleSeries.Values[i][j] = Mathf.Lerp(
							StyleSeries.Values[i][j],
							Path.CalculatePointStyleValue(timestamp, j),
							TrajectoryControl
						);
					}
					loopIndex += 1;
				} 
			}

		}

		protected override void Feed()
		{
			Control();

			if(NeuralNetwork.Model == null) { return; }
			//Get Root
			Matrix4x4 root = Actor.GetRoot().GetWorldMatrix();

			//Input Timeseries - Resolution = 1
			for (int i = 0; i < TimeSeries.KeyCount; i++)
			{
				int index = TimeSeries.GetKey(i).Index;
				NeuralNetwork.FeedXZ(RootSeries.GetPosition(index).GetRelativePositionTo(InputPose.Root));
				NeuralNetwork.FeedXZ(RootSeries.GetDirection(index).GetRelativeDirectionTo(InputPose.Root));
				NeuralNetwork.FeedXZ(RootSeries.Velocities[index].GetRelativeDirectionTo(InputPose.Root));
				float t = Path.TimeInterval - GetDeltaTimeOffset() - TimeSeries.GetKey(i).Timestamp;
				NeuralNetwork.Feed(t);
				TimeDeltas[i] = t;

				if(ModelIncludesStyleLabels)
					NeuralNetwork.Feed(StyleSeries.Values[index]);
			}

			//Input Character
			for (int i = 0; i < Actor.Bones.Length; i++)
			{
				NeuralNetwork.Feed(Actor.Bones[i].GetTransform().position.GetRelativePositionTo(root));
				NeuralNetwork.Feed(Actor.Bones[i].GetTransform().forward.GetRelativeDirectionTo(root));
				NeuralNetwork.Feed(Actor.Bones[i].GetTransform().up.GetRelativeDirectionTo(root));
				NeuralNetwork.Feed(Actor.Bones[i].GetVelocity().GetRelativeDirectionTo(root));
			}

			//Input TargetPose
			for (int i = 0; i < InputPose.Transformations.Length; i++)
			{
				NeuralNetwork.Feed(InputPose.Transformations[i].GetPosition().GetRelativePositionTo(root));
				NeuralNetwork.Feed(InputPose.Transformations[i].GetForward().GetRelativeDirectionTo(root));
				NeuralNetwork.Feed(InputPose.Transformations[i].GetUp().GetRelativeDirectionTo(root));
				NeuralNetwork.Feed(InputPose.BoneVelocities[i].GetRelativeDirectionTo(root));
			}

			//Input Contacts
			for (int i = 0; i <= TimeSeries.PivotKey; i++)
			{
				int index = TimeSeries.GetKey(i).Index;
				NeuralNetwork.Feed(ContactSeries.Values[index]);
			}

			//Input Gating Features
			if(PhaseSelection == PHASES.LocalPhases){
				NeuralNetwork.Feed(PhaseSeries.GetAlignment());
			} else if(PhaseSelection == PHASES.DeepPhases){
				NeuralNetwork.Feed(DeepPhaseSeries.GetAlignment());
			}
		}
		
		//Elapsed Time
		private float GetDeltaTimeOffset() {
			//timeInput = Mathf.Min(remainingTime, 2f)
			float deltaTimeOffset = (Path.TimeInterval - (Path.GetTimestampByControlPoint(NextCP) - Path.GetTimestampByPointIndex(Path.GetPointIndex(RootTimestamp))));
			if (Path.isLooping && NextCP == Path.ControlPoints[0])
            {
				deltaTimeOffset = (Path.TimeInterval - (Path.GetTotalTime() - RootTimestamp));
			}
			return deltaTimeOffset;
		}
		//Remaining time is Path.Timeinterval - GetDeltaTimeOffset();

		private float GetTimeOffsetWeight() {
			float initialTimeOffset = Path.TimeInterval;
			return Mathf.Clamp(GetDeltaTimeOffset() / initialTimeOffset, 0f, 1f);
		}

		protected override void Read()
		{
			if(NeuralNetwork.Model == null) { return; }

			//Update Past States
			ContactSeries.Increment(0, TimeSeries.Pivot);
			if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.Increment(0, TimeSeries.Pivot);
			if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.Increment(0, TimeSeries.Pivot);

			Matrix4x4 currentRoot = Actor.GetRoot().GetWorldMatrix();
			Matrix4x4 currentTargetRoot = InputPose.Root;
			
			Matrix4x4 predictedRoot = Matrix4x4.TRS(NeuralNetwork.ReadXZ().GetRelativePositionFrom(currentRoot), Quaternion.LookRotation(NeuralNetwork.ReadXZ().normalized.GetRelativeDirectionFrom(currentRoot), Vector3.up), Vector3.one);
			Vector3 predictedRootVelocity = NeuralNetwork.ReadXZ().GetRelativeDirectionFrom(currentRoot);

			Matrix4x4 predictedTargetRoot = Matrix4x4.TRS(NeuralNetwork.ReadXZ().GetRelativePositionFrom(currentTargetRoot), Quaternion.LookRotation(NeuralNetwork.ReadXZ().normalized.GetRelativeDirectionFrom(currentTargetRoot), Vector3.up), Vector3.one);
			Vector3 predictedTargetVelocity = NeuralNetwork.ReadXZ().GetRelativeDirectionFrom(currentTargetRoot);

			float timeOffsetWeight = GetTimeOffsetWeight();

			timeOffsetWeight = Mathf.Pow(timeOffsetWeight, 2.0f);//.SmoothStep(2.0f, 0.5f);
			InBetweeningTime = timeOffsetWeight;

			//Update Root State
			Matrix4x4 root = Utility.Interpolate(
								predictedRoot,
								predictedTargetRoot,
								BlendToTargetSpace ? timeOffsetWeight : 0f
							);
			PredictedRoot = predictedRoot;
			PredictedTargetRoot = predictedTargetRoot;
			RootTrajectoryPrediction[0] = predictedRoot;
			TargetTrajectoryPrediction[0] = predictedTargetRoot;
			
			Vector3 vel = Vector3.Lerp(
								predictedRootVelocity,
								predictedTargetVelocity,
								BlendToTargetSpace ? timeOffsetWeight : 0f
							);
			RootSeries.Transformations[TimeSeries.Pivot] = root;
			RootSeries.Velocities[TimeSeries.Pivot] = vel;
			
			if(ModelIncludesStyleLabels){
				//Read Root Style
				for(int j=0; j<StyleSeries.Styles.Length; j++) {
					StyleSeries.Values[TimeSeries.Pivot][j] = Mathf.Lerp(
						StyleSeries.Values[TimeSeries.Pivot][j], 
						NeuralNetwork.Read(0f, 1f), 
						TrajectoryCorrection
					);
				} 
			}

			//Read Future States
			int loopIndex = 0;
			Vector3[] rootKeyPositions = new Vector3[TimeSeries.FutureKeys];
			Vector3[] rootKeyDirections = new Vector3[TimeSeries.FutureKeys];
			Vector3[] rootKeyVelocities = new Vector3[TimeSeries.FutureKeys];

			for (int i = TimeSeries.PivotKey + 1; i < TimeSeries.KeyCount; i++)
			{
				int index = TimeSeries.GetKey(i).Index;

				rootKeyPositions[loopIndex] = NeuralNetwork.ReadXZ().GetRelativePositionFrom(root);
				rootKeyDirections[loopIndex] = NeuralNetwork.ReadXZ().normalized.GetRelativeDirectionFrom(root);
				rootKeyVelocities[loopIndex] = NeuralNetwork.ReadXZ().GetRelativeDirectionFrom(root);
				
				RootTrajectoryPrediction[loopIndex+1] = Matrix4x4.TRS(rootKeyPositions[loopIndex], Quaternion.LookRotation(rootKeyDirections[loopIndex], Vector3.up), Vector3.one);

				loopIndex++;
			}

			loopIndex = 0;
			
			
			for (int i = TimeSeries.PivotKey + 1; i < TimeSeries.KeyCount; i++) 
			{
				int index = TimeSeries.GetKey(i).Index;

				Vector3 targetPosition = NeuralNetwork.ReadXZ().GetRelativePositionFrom(currentTargetRoot);
				Vector3 targetDirection = NeuralNetwork.ReadXZ().normalized.GetRelativeDirectionFrom(currentTargetRoot);
				Vector3 targetVelocity = NeuralNetwork.ReadXZ().GetRelativeDirectionFrom(currentTargetRoot);

				TargetTrajectoryPrediction[loopIndex+1] = Matrix4x4.TRS(targetPosition, Quaternion.LookRotation(targetDirection, Vector3.up), Vector3.one);

				//float distance = Vector3.Distance(rootKeyPositions[loopIndex], currentTargetRoot.GetPosition());
				//float weight = Mathf.Pow((float)(loopIndex+1)/TimeSeries.FutureKeys, distance*distance);
				float weight = BlendToTargetSpace ? timeOffsetWeight : 0f; //timeOffsetWeight

				Vector3 position = Utility.Interpolate(rootKeyPositions[loopIndex], targetPosition, weight);
				Vector3 direction = Vector3.Slerp(rootKeyDirections[loopIndex], targetDirection, weight).normalized;
				Vector3 velocity = Vector3.Lerp(rootKeyVelocities[loopIndex], targetVelocity, weight); 

				Matrix4x4 m = Matrix4x4.TRS(position, Quaternion.LookRotation(direction, Vector3.up), Vector3.one);
				// weight = Mathf.Pow((float)(index+1) / (float)Samples.Length, 2.0f);
				RootSeries.Transformations[index] = Utility.Interpolate(RootSeries.Transformations[index], m, TrajectoryCorrection);
				RootSeries.Velocities[index] = Vector3.Lerp(RootSeries.Velocities[index], velocity, TrajectoryCorrection);
				
				if(ModelIncludesStyleLabels) {
					//Styles
					for(int j=0; j<StyleSeries.Styles.Length; j++) {
						StyleSeries.Values[index][j] = Mathf.Lerp(StyleSeries.Values[index][j], NeuralNetwork.Read(0f, 1f), TrajectoryCorrection);
					} 
				}
				loopIndex++;
			}

			//Read Posture
			Vector3[] positions = new Vector3[Actor.Bones.Length];
			Vector3[] forwards = new Vector3[Actor.Bones.Length];
			Vector3[] upwards = new Vector3[Actor.Bones.Length];
			Vector3[] velocities = new Vector3[Actor.Bones.Length];

			
			for (int i = 0; i < Actor.Bones.Length; i++)
			{
				Vector3 position = NeuralNetwork.ReadVector3().GetRelativePositionFrom(root);
				Vector3 forward = NeuralNetwork.ReadVector3().normalized.GetRelativeDirectionFrom(root);
				Vector3 upward = NeuralNetwork.ReadVector3().normalized.GetRelativeDirectionFrom(root);
				Vector3 velocity = NeuralNetwork.ReadVector3().GetRelativeDirectionFrom(root);

				RootPosePrediction[i] = Matrix4x4.TRS(position, Quaternion.LookRotation(forward, upward), Vector3.one); 

				velocities[i] = velocity;
				if(BlendToTargetSpace) {
					positions[i] = position;
				} else {
					positions[i] = Vector3.Lerp(Actor.Bones[i].GetTransform().position + velocities[i] / Framerate, position, 0.5f);
				}
				forwards[i] = forward;
				upwards[i] = upward;
			}

			
			//Blend root and target space
			for (int i = 0; i < Actor.Bones.Length; i++)
			{
				// Vector3 targetRootPosition = NeuralNetwork.ReadVector3().GetRelativePositionFrom(currentTargetRoot); //TargetPose.Transformations[i]
				Matrix4x4 targetMatrix = Matrix4x4.TRS(InputPose.Transformations[i].GetPosition(), currentTargetRoot.GetRotation(), Vector3.one);

				Vector3 targetRootPosition = NeuralNetwork.ReadVector3().GetRelativePositionFrom(targetMatrix);
				Vector3 targetRootForward = NeuralNetwork.ReadVector3().normalized.GetRelativeDirectionFrom(targetMatrix);
				Vector3 targetRootUpward = NeuralNetwork.ReadVector3().normalized.GetRelativeDirectionFrom(targetMatrix);
				Vector3 targetRootVelocity = NeuralNetwork.ReadVector3().GetRelativeDirectionFrom(targetMatrix);

				TargetPosePrediction[i] = Matrix4x4.TRS(targetRootPosition, Quaternion.LookRotation(targetRootForward, targetRootUpward), Vector3.one); 

				Vector3 position = Vector3.Lerp(positions[i], targetRootPosition, timeOffsetWeight);

				if(BlendToTargetSpace) {
					//Dont blend dog tail
					if(Authoring.Topology == TOPOLOGY.Quadruped && i>22){
						positions[i] = Vector3.Lerp(Actor.Bones[i].GetTransform().position + velocities[i] / Framerate, position, 0.5f);
						continue;
					}

					velocities[i] = Vector3.Lerp(velocities[i], targetRootVelocity, timeOffsetWeight);
					positions[i] = Vector3.Lerp(Actor.Bones[i].GetTransform().position + velocities[i] / Framerate, position, 0.5f);
					forwards[i] = Vector3.Slerp(forwards[i], targetRootForward, timeOffsetWeight);
					upwards[i] = Vector3.Slerp(upwards[i], targetRootUpward, timeOffsetWeight);
				}
			}

			//Update Contacts
			float[] contacts = NeuralNetwork.Read(ContactSeries.Bones.Length, 0f, 1f);
			for (int i = 0; i < ContactSeries.Bones.Length; i++)
			{
				ContactSeries.Values[TimeSeries.Pivot][i] = contacts[i].SmoothStep(ContactPower, BoneContactThreshold);
			}

			//Update Phases
			if(PhaseSelection == PHASES.LocalPhases) {	
				for (int i = TimeSeries.PivotKey; i < TimeSeries.KeyCount; i++)
				{
					int index = TimeSeries.GetKey(i).Index;
					float stability = 1f; //TrajectoryCorrection
					for (int b = 0; b < PhaseSeries.Bones.Length; b++)
					{
						Vector2 update = NeuralNetwork.ReadVector2();
						Vector3 state = NeuralNetwork.ReadVector2();
						float phase = Utility.PhaseValue(
							Vector2.Lerp(
								Utility.PhaseVector(Mathf.Repeat(PhaseSeries.Phases[index][b] + Utility.PhaseValue(update), 1f)),
								Utility.PhaseVector(Mathf.Repeat(PhaseSeries.Phases[index][b] + Utility.SignedPhaseUpdate(PhaseSeries.Phases[index][b], Utility.PhaseValue(state)), 1f)),
								stability).normalized
							);
						PhaseSeries.Amplitudes[index][b] = update.magnitude;
						PhaseSeries.Phases[index][b] = phase;
					}
				}
			} else if(PhaseSelection == PHASES.DeepPhases) {
				DeepPhaseSeries.UpdateAlignment(NeuralNetwork.Read((1+DeepPhaseSeries.FutureKeys) * DeepPhaseSeries.Channels * 4), 0.5f, 1f/Framerate);
			}
			//Interpolate Timeseries
			RootSeries.Interpolate(TimeSeries.Pivot, TimeSeries.Samples.Length);
			if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.Interpolate(TimeSeries.Pivot, TimeSeries.Samples.Length);
			if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.Interpolate(TimeSeries.Pivot, TimeSeries.Samples.Length);
			
			//Assign Posture
			transform.position = RootSeries.GetPosition(TimeSeries.Pivot);
			transform.rotation = RootSeries.GetRotation(TimeSeries.Pivot);

			for (int i = 0; i < Actor.Bones.Length; i++)
			{
				Actor.Bones[i].SetVelocity(velocities[i]);
				Actor.Bones[i].GetTransform().position = positions[i];
				Actor.Bones[i].GetTransform().rotation = Quaternion.LookRotation(forwards[i], upwards[i]);
			}

			//Correct Twist
			Actor.RestoreAlignment();

			if(Authoring.Topology == TOPOLOGY.Biped) {
				float[] bodyContacts = new float[3] {
					ContactSeries.Values[TimeSeries.Pivot][0], //Hips
					ContactSeries.Values[TimeSeries.Pivot][1], //LeftHand
					ContactSeries.Values[TimeSeries.Pivot][2]  //RightHand
				};

				//ArrayExtensions.Append(ref bodyContacts, ContactSeries.Values[TimeSeries.Pivot][4]);
				ProcessFootIKBiped(LeftFootIK, ContactSeries.Values[TimeSeries.Pivot][3], bodyContacts);
				//bodyContacts[3] = ContactSeries.Values[TimeSeries.Pivot][3];
				ProcessFootIKBiped(RightFootIK, ContactSeries.Values[TimeSeries.Pivot][4], bodyContacts);
			}

			if(Authoring.Topology == TOPOLOGY.Quadruped) {
			//Process Contact States
				ProcessFootIKQuadruped(LeftHandIK, ContactSeries.Values[TimeSeries.Pivot][0]);
				ProcessFootIKQuadruped(RightHandIK, ContactSeries.Values[TimeSeries.Pivot][1]);
				ProcessFootIKQuadruped(LeftFootIK, ContactSeries.Values[TimeSeries.Pivot][2]);
				ProcessFootIKQuadruped(RightFootIK, ContactSeries.Values[TimeSeries.Pivot][3]);
			}

			if(PhaseSelection != PHASES.NoPhases && DrawGating) {
				GatingHistory.Add(NeuralNetwork.GetOutput("G").AsFloats());
				if(GatingHistory.Count > 100) {
					GatingHistory.RemoveAt(0);
				}
			}
		}

		private void ProcessFootIKBiped(IK ik, float contact, float[] values)
		{
			if(!Postprocessing) {
                return;
            }
			float weight = contact;
			
			ik.Activation = UltimateIK.ACTIVATION.Constant;
			ik.Objectives.First().SetTarget(Vector3.Lerp(ik.Objectives[0].TargetPosition, ik.Joints.Last().Transform.position, 1f - weight));

			//ik.Objectives.First().SetTarget(Quaternion.Slerp(ik.Objectives[0].TargetRotation, ik.Bones.Last().Transform.rotation, 1f-contact));

			ik.Objectives.First().SetTarget(ik.Joints.Last().Transform.rotation);

			ik.Iterations = 50;
			ik.Solve();
		}

		private void ProcessFootIKQuadruped(IK ik, float contact)
		{
            if(!Postprocessing) {
                return;
            }
            ik.Activation = UltimateIK.ACTIVATION.Linear;
            for(int i=0; i<ik.Objectives.Length; i++) {
                ik.Objectives[i].SetTarget(Vector3.Lerp(ik.Objectives[i].TargetPosition, ik.Joints[ik.Objectives[i].Joint].Transform.position, 1f-contact));
                ik.Objectives[i].SetTarget(ik.Joints[ik.Objectives[i].Joint].Transform.rotation);
            }
            ik.Iterations = 25;
            ik.Solve();
		}

		private UltiDraw.GUIRect BlendingRect = new UltiDraw.GUIRect(0.5f, 0.8f, 0.2f, 0.015f);
		private Color ColorRootPrediction = UltiDraw.DarkBlue.Darken(0.5f);
		private Color ColorTargetPrediction = UltiDraw.DarkRed;
		protected override void OnGUIDerived()
		{
			ContactSeries.DrawGUI = DrawGUI;
			StyleSeries.DrawGUI = DrawGUI;
			if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.DrawGUI = DrawGUI;
			if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.DrawGUI = DrawGUI;	

			ContactSeries.GUI(GetCamera());
			StyleSeries.GUI(GetCamera());
			if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.GUI(GetCamera());
			if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.GUI(GetCamera());

			if(ShowBiDirectional) {
				UltiDraw.Begin(GetCamera());
				UltiDraw.OnGUILabel(BlendingRect.GetCenter() + new Vector2(0f, 0.03f), BlendingRect.GetSize(), 0.0175f, "Blending", UltiDraw.Black);
				UltiDraw.OnGUILabel(new Vector2(BlendingRect.X-0.15f, BlendingRect.Y + 0.1075f) + new Vector2(0f, 0.07f), BlendingRect.GetSize(), 0.0175f, "Time Deltas", UltiDraw.Black);
				UltiDraw.End();
			}
			UltiDraw.Begin(GetCamera());
			UltiDraw.OnGUILabel(BlendingRect.GetCenter(), BlendingRect.GetSize(), 0.0175f, (Authoring.Path.TimeInterval - GetDeltaTimeOffset()).ToString("F1") + "s", UltiDraw.White);
			UltiDraw.End();
/* 			UltiDraw.GUIRect Rect = new UltiDraw.GUIRect(0.5f, 0.75f, 0.5f, 0.2f);
			float size = 0.05f;
			UltiDraw.Begin();
			UltiDraw.OnGUILabel(new Vector2(Rect.X - 0.5f * Rect.W, Rect.Y - 0.5f*Rect.H - size), Rect.GetSize(), size/2f, "PE: " + Utility.Round(PEValues.ToArray().Mean(), 4).ToString(), UltiDraw.Red);
			UltiDraw.OnGUILabel(new Vector2(Rect.X, Rect.Y - 0.5f*Rect.H - size), Rect.GetSize(), size/2f, "" + PEValues.Count + " Deviation: " + Utility.Round(DeviationValues.ToArray().Mean(), 4).ToString(), UltiDraw.Red);
			UltiDraw.OnGUILabel(new Vector2(Rect.X + 0.5f * Rect.W, Rect.Y - 0.5f*Rect.H - size), Rect.GetSize(), size/2f, "RE: " + Utility.Round(REValues.ToArray().Mean(), 2).ToString(), UltiDraw.Red);
			UltiDraw.End(); */
		}

		protected override void OnRenderObjectDerived()
		{	
			if(DrawDebug){
				Actor.Draw(InputPose.Transformations, UltiDraw.Purple, Actor.JointColor, Actor.DRAW.Skeleton);
				UltiDraw.Begin();
				UltiDraw.DrawSphere(InputPose.Root.GetPosition(), Quaternion.identity, 0.2f, UltiDraw.Purple);
	/* 			for(int i=0; i<InputPose.BoneVelocities.Length; i++) {
					UltiDraw.DrawArrow(
						InputPose.Transformations[i].GetPosition(),
						InputPose.Transformations[i].GetPosition() + InputPose.BoneVelocities[i] * 0.2f,
						0.75f,
						0.0075f,
						0.05f,
						UltiDraw.Orange.Opacity(0.5f)
					);
				} */

				UltiDraw.DrawLine(Actor.GetRoot().GetWorldMatrix().GetPosition(), Path.GetClosestPositionOnPath(Actor.GetRoot().GetWorldMatrix().GetPosition()), UltiDraw.Black);
				UltiDraw.End();
			}

			if(ShowBiDirectional) {
				UltiDraw.Begin();
				// UltiDraw.DrawSphere(RootSeries.GetPosition(TimeSeries.Pivot), Quaternion.identity, 0.2f, Color.black);
				UltiDraw.DrawCircle(PredictedRoot.GetPosition(), 0.125f, ColorRootPrediction);
				UltiDraw.DrawCircle(PredictedTargetRoot.GetPosition(), 0.125f, ColorTargetPrediction);

				for(int i=0; i<RootTrajectoryPrediction.Length; i++) {
					UltiDraw.DrawCircle(RootTrajectoryPrediction[i].GetPosition(), 0.05f, ColorRootPrediction);
					UltiDraw.DrawArrow(RootTrajectoryPrediction[i].GetPosition(), RootTrajectoryPrediction[i].GetPosition() + 0.1f*RootTrajectoryPrediction[i].GetForward(), 0f, 0f, 0.025f, ColorRootPrediction.Lighten(0.5f));
					if(i<RootTrajectoryPrediction.Length-1) {
						UltiDraw.DrawLine(RootTrajectoryPrediction[i].GetPosition(), RootTrajectoryPrediction[i+1].GetPosition(), UltiDraw.Black);
					}
				}
				for(int i=0; i<TargetTrajectoryPrediction.Length; i++) {
					UltiDraw.DrawCircle(TargetTrajectoryPrediction[i].GetPosition(), 0.05f, ColorTargetPrediction);
					UltiDraw.DrawArrow(TargetTrajectoryPrediction[i].GetPosition(), TargetTrajectoryPrediction[i].GetPosition() + 0.1f*TargetTrajectoryPrediction[i].GetForward(), 0f, 0f, 0.025f, ColorTargetPrediction.Darken(0.5f));
					if(i<TargetTrajectoryPrediction.Length-1) {
						UltiDraw.DrawLine(TargetTrajectoryPrediction[i].GetPosition(), TargetTrajectoryPrediction[i+1].GetPosition(), UltiDraw.Black);
					}
				} 

				UltiDraw.PlotHorizontalBar(new Vector2(BlendingRect.X, BlendingRect.Y), new Vector2(BlendingRect.W, BlendingRect.H), InBetweeningTime, fillColor: ColorRootPrediction.Lerp(ColorTargetPrediction, InBetweeningTime));
				UltiDraw.PlotFunction(new Vector2(BlendingRect.X-0.15f, BlendingRect.Y + 0.1075f), new Vector2(0.2f, 0.1f), TimeDeltas, yMin: -TimeSeries.PastWindow, yMax: Path.TimeInterval + TimeSeries.FutureWindow, thickness: 0.002f, lineColor: ColorRootPrediction.Lerp(ColorTargetPrediction, -TimeDeltas.Min()));
				UltiDraw.End();

				Actor.Draw(RootPosePrediction, ColorRootPrediction, ColorRootPrediction, Actor.DRAW.Skeleton);
				Actor.Draw(TargetPosePrediction, ColorTargetPrediction, ColorTargetPrediction, Actor.DRAW.Skeleton);
			}
			if(DrawGating && PhaseSelection != PHASES.NoPhases) {
				UltiDraw.Begin();
				UltiDraw.GUIRect GatingWindow = new UltiDraw.GUIRect(0.1f, 0.8f, 0.125f, 0.125f);
				UltiDraw.DrawInterpolationSpace(GatingWindow, GatingHistory);
				UltiDraw.End();
			}
			RootSeries.DrawScene = DrawDebug;
			ContactSeries.DrawScene = DrawDebug;
			StyleSeries.DrawScene = DrawDebug;
			if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.DrawScene = DrawDebug;
			if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.DrawScene = DrawDebug;
			Authoring.DrawScene = DrawDebug;
			RootSeries.Draw(GetCamera());
			ContactSeries.Draw(GetCamera());
			StyleSeries.Draw(GetCamera());
			if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.Draw(GetCamera());
			if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.Draw(GetCamera());
			Authoring.Draw(GetCamera());
		}

		public class InputPoseData
		{
			public Matrix4x4[] Transformations;
			public Matrix4x4 Root;
			public Vector3[] BoneVelocities;

			public InputPoseData(Matrix4x4[] transformations, Matrix4x4 root, Vector3[] velocities){
				SetTransformations(transformations);
				Root = root;
				SetVelocities(velocities);
			}

			public void SetTransformations(Matrix4x4[] transformations){
				Transformations = new Matrix4x4[transformations.Length];
				for(int i = 0; i<transformations.Length; i++ ){
					Transformations[i] = Matrix4x4.TRS(transformations[i].GetPosition(), transformations[i].GetRotation(), transformations[i].GetScale());
				}
			}
			public void SetVelocities(Vector3[] velocities){
				BoneVelocities = new Vector3[velocities.Length];
				for(int i = 0; i<BoneVelocities.Length; i++ ){
					BoneVelocities[i] = new Vector3(velocities[i].x, velocities[i].y, velocities[i].z);
				}
			}
		}
		public ContactSeries GetContactSeries() {
			return ContactSeries;
		}
		public float GetRootTimestamp(){
			return RootTimestamp;
		}
		
/* 		void LateUpdate(){
			float deviation = Vector3.Distance(Actor.GetRoot().GetWorldMatrix().GetPosition(), Path.GetClosestPositionOnPath(Actor.GetRoot().GetWorldMatrix().GetPosition()));

			DeviationValues.Add(deviation);
		} */
	}
}