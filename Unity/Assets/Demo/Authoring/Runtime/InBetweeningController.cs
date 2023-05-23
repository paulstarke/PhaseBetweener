using UnityEngine;
using System.Collections.Generic;
using UltimateIK;
using System;
using Unity.Barracuda;

public class InBetweeningController : NeuralONNXAnimation
{
	public enum SKELETON {LaFAN1, Dog};
	public enum PHASES {NoPhases, LocalPhases, DeepPhases};
	//public enum STYLE {Move, Aim, Crawl};
	public SKELETON Skeleton = SKELETON.LaFAN1;
	public PHASES PhaseSelection = PHASES.DeepPhases;
	//public STYLE ActiveStyle = STYLE.Move;
    [Range (0,4)] public float SamplingOffset = 2.0f;
	[Range (0,1)] public float LerpDurationFactor = 0.25f;
	[Range (0,1)] public float TrajectoryControl = 0.5f;
	[Range (0,1)] public float TrajectoryCorrection = 1.0f;
	//[Range (0,1)] public float StyleCorrection = 0.9f;
	public bool LerpInputPose = true;
	public bool BlendToTargetSpace = true;
	public bool Postprocessing = true;
	// [Range (0,1)] public float ThresholdTargetIK = 0.9f;
	public Camera Camera = null;
	public MotionData Asset;
	public List<MotionData> Clips = new List<MotionData>();
	public bool Mirror = true;
	public int StartFrame = 120;

	//Evaluation
	public int EndFrame = 0;

	public Actor FinalActor;
	public GameObject TargetActor;
	public bool DrawGating = true;
	public bool DrawInputSpace = true;
	public bool DrawGUI = true;
	public bool DrawDebug = true;
	public bool ShowBiDirectional = true;

	public bool DrawNetworkSpace = true;
	public bool DrawFirstLayer = true;
	public bool DrawSecondLayer = false;
	public bool DrawThirdLayer = false;

	private TimeSeries TimeSeries;
	private TimeSeries SampleSeries;
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
	private TargetPoseData TargetPose;
    private TargetPoseData InputPose;
	private float[] TimeDeltas = null;
	private List<float[]> GatingHistory = new List<float[]>();
	private int FrameCounter = 0;

	Matrix4x4 PredictedRoot;
	Matrix4x4[] RootTrajectoryPrediction;
	Matrix4x4[] TargetTrajectoryPrediction; 
	Matrix4x4 PredictedTargetRoot;
	Matrix4x4[] RootPosePrediction; 
	Matrix4x4[] TargetPosePrediction; 

	private Matrix4x4[] FinalTransformations;
	private Vector3[] FinalVelocities;

	// IK
	private int LeftHand;
	private int RightHand;
	private int LeftFoot;
	private int RightFoot;
	private IK LeftHandIK;
	private IK RightHandIK;
	private IK LeftFootIK;
	private IK RightFootIK;

	private Camera GetCamera()
	{
		return Camera == null ? Camera.main : Camera;
	}

	protected override void Setup()
	{	
		TimeSeries = new TimeSeries(6, 6, 1f, 1f, 5);
		SampleSeries = new TimeSeries(6, 6, 1f, 1f, 1);
		RootSeries = new RootSeries(TimeSeries, transform);
		StyleSeries = new StyleSeries(TimeSeries, new string[] {"Move", "Aim", "Crawl" }, new float[] {1f, 0f, 0f});
		
		string[] contactBones = new string[0];
		if(Skeleton == SKELETON.LaFAN1){
			contactBones = new string[] {"Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot"};
		}
		if(Skeleton == SKELETON.Dog){
			contactBones = new string[] {"LeftHandSite", "RightHandSite", "LeftFootSite", "RightFootSite"};
		}
		ContactSeries = new ContactSeries(TimeSeries, contactBones);
		
		if(PhaseSelection == PHASES.LocalPhases) {
			PhaseSeries = new PhaseSeries(TimeSeries, contactBones);
		} else if(PhaseSelection == PHASES.DeepPhases) {
			DeepPhaseSeries = new DeepPhaseSeries(TimeSeries, Channels);
		}
		TimeDeltas = new float[TimeSeries.KeyCount];
		
		RootTimestamp = StartFrame / Framerate;
		FrameCounter = StartFrame;

		TargetPose = new TargetPoseData(RootTimestamp, InBetweeningModule.SampleFuturePose(Asset, RootTimestamp, Mirror, Actor.GetBoneNames(), sampleOffset: SamplingOffset));
		TargetActor.GetComponent<Actor>().SetBoneTransformations(TargetPose.Transformations);
		InputPose = TargetPose;
		RootSeries rootSeriesGT = (RootSeries)Asset.GetModule<RootModule>().ExtractSeries(TimeSeries, RootTimestamp, Mirror);
		Actor.transform.position = rootSeriesGT.GetPosition(TimeSeries.Pivot);
		Actor.transform.LookAt(rootSeriesGT.GetPosition(TimeSeries.Pivot + 1));

		for (int i = 0; i < TimeSeries.Samples.Length; i++)
		{
			float timestamp = RootTimestamp + ((i - TimeSeries.Pivot) / Framerate);

			//Root Positions
			RootSeries.SetPosition(i, rootSeriesGT.GetPosition(i));

			//Root Rotations
			RootSeries.SetDirection(i, rootSeriesGT.GetDirection(i));

			//Root Velocities
			RootSeries.SetVelocity(i, rootSeriesGT.GetVelocity(i));

		}
		RootTrajectoryPrediction = new Matrix4x4[TimeSeries.FutureKeys+1];
		TargetTrajectoryPrediction = new Matrix4x4[TimeSeries.FutureKeys+1];
		RootPosePrediction = new Matrix4x4[Actor.Bones.Length];
		TargetPosePrediction = new Matrix4x4[Actor.Bones.Length];
		FinalTransformations = new Matrix4x4[Actor.Bones.Length];
		FinalVelocities = new Vector3[Actor.Bones.Length];
		//TargetIK = IK.Create(Actor.FindTransform("Chest4"), Actor.FindTransform("LeftWrist"), Actor.FindTransform("RightWrist"));
		if(Skeleton == SKELETON.LaFAN1){
			LeftHand = Actor.FindBone("LeftHand").GetIndex();
			RightHand = Actor.FindBone("RightHand").GetIndex();
			LeftFoot = Actor.FindBone("LeftFoot").GetIndex();
			RightFoot = Actor.FindBone("RightFoot").GetIndex();
			LeftHandIK = IK.Create(Actor.FindTransform("LeftShoulder"), new Transform[]{Actor.Bones[LeftHand].GetTransform()});
			RightHandIK = IK.Create(Actor.FindTransform("RightShoulder"), new Transform[]{Actor.Bones[RightHand].GetTransform()});
			LeftFootIK = IK.Create(Actor.FindTransform("LeftUpLeg"), new Transform[]{Actor.Bones[LeftFoot].GetTransform()});
			RightFootIK = IK.Create(Actor.FindTransform("RightUpLeg"), new Transform[]{Actor.Bones[RightFoot].GetTransform()});
		}
		if(Skeleton == SKELETON.Dog){
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

	private int cycleIndex = 0;
	public void ChangeClip() {
		cycleIndex ++;
		if(cycleIndex >= Clips.Count){
			cycleIndex = 0;
		}
		Asset = Clips[cycleIndex];;
		Setup();
	}

	private void BlendInputTarget(){
        float lerpDuration = LerpDurationFactor * InputPose.InitialTimeOffset;
        float timeElapsed = TargetPose.DeltaTimeOffset;
		//BLENDING STATE
        if (timeElapsed < lerpDuration) //0.5 * 30 = 15 samples
        {
            float weight = timeElapsed / lerpDuration;
            //Debug.Log(weight);
		    InputPose = new TargetPoseData(TargetPose.SampleTimestamp, InBetweeningModule.SampleFuturePose(Asset, TargetPose.SampleTimestamp, Mirror, Actor.GetBoneNames(), sampleOffset: weight * TargetPose.InitialTimeOffset));
			InputPose.DeltaTimeOffset = TargetPose.DeltaTimeOffset;
			InputPose.InitialTimeOffset = TargetPose.InitialTimeOffset;

			Vector3[] linearVel = new Vector3[InputPose.BoneVelocities.Length];
			Vector3[] prevVel = Asset.GetFrame(TargetPose.SampleTimestamp).GetBoneVelocities(Mirror); //(Asset.Source.FindBone(Actor.Bones[j].GetName()).Name, Mirror)
			Vector3[] targetVel = Asset.GetFrame(TargetPose.Timestamp).GetBoneVelocities(Mirror);
			for(int i = 0; i<linearVel.Length; i++) {
				linearVel[i] = Vector3.Lerp(
				Asset.GetFrame(TargetPose.SampleTimestamp).GetBoneVelocity(Asset.Source.FindBone(Actor.Bones[i].GetName()).Name, Mirror),
				Asset.GetFrame(TargetPose.Timestamp).GetBoneVelocity(Asset.Source.FindBone(Actor.Bones[i].GetName()).Name, Mirror),
				weight
				);
			}
			Matrix4x4 targetRoot = Asset.GetModule<RootModule>().GetRootTransformation(TargetPose.Timestamp, Mirror);
			Matrix4x4 inputRoot = Asset.GetModule<RootModule>().GetRootTransformation(InputPose.Timestamp, Mirror);

            for(int i = 0; i<TargetPose.Transformations.Length; i++) {
				InputPose.Transformations[i] = TargetPose.Transformations[i].TransformationFromTo(targetRoot, inputRoot);
/* 				Actor.Bones[i].SetTransformation(Utility.Interpolate(
					Asset.GetFrame(TargetPose.SampleTimestamp).GetBoneTransformation(Asset.Source.FindBone(Actor.Bones[i].GetName()).Name, Mirror),
					TargetPose.Transformations[i],
					weight
				)); */
				InputPose.BoneVelocities[i] = linearVel[i].GetDirectionFromTo(targetRoot, inputRoot);
            }  
        }  else {
		    InputPose = TargetPose;
        } 
	}
	private void Control()
	{
		if(RootTimestamp > Asset.GetTotalTime()) return;

		//Update Timestamps
		FrameCounter++;
		TargetPose.FrameCounter++;
		RootTimestamp = (1f/Framerate) * FrameCounter;
		TargetPose.DeltaTimeOffset = (1f/Framerate) * TargetPose.FrameCounter;

		if(RootTimestamp > TargetPose.Timestamp) {
			TargetPose = new TargetPoseData(RootTimestamp, InBetweeningModule.SampleFuturePose(Asset, RootTimestamp, Mirror, Actor.GetBoneNames(), sampleOffset: SamplingOffset));
			TargetActor.GetComponent<Actor>().SetBoneTransformations(TargetPose.Transformations);
		}
        
		if(LerpInputPose)
		{
			BlendInputTarget();
		} else {
			InputPose = TargetPose;
		}

		//Update Past
		RootSeries.Increment(0, TimeSeries.Samples.Length - 1);
		StyleSeries.Increment(0, TimeSeries.Samples.Length - 1);

		RootSeries rootSeriesGT = (RootSeries)Asset.GetModule<RootModule>().ExtractSeries(SampleSeries, RootTimestamp, Mirror);
		
		//Set Future Trajectory
		// i in [6,12]
		// index in [55, 60, ..., 120]
		for(int i = TimeSeries.PivotKey; i < TimeSeries.KeyCount; i++)
		{	
			int index = TimeSeries.GetKey(i).Index;
			//Root Positions
			RootSeries.SetPosition(index,
				Vector3.Lerp(
					RootSeries.GetPosition(index),
					rootSeriesGT.GetPosition(i),
					TrajectoryControl
				)
			);

			//Root Rotations
			RootSeries.SetDirection(index,
				Vector3.Slerp(
					RootSeries.GetDirection(index),
					rootSeriesGT.GetDirection(i),
					TrajectoryControl
				)
			);

			//Root Velocities
			RootSeries.SetVelocity(index,
				Vector3.Lerp(
					RootSeries.GetVelocity(index),
					rootSeriesGT.GetVelocity(i),
					TrajectoryControl
				)
			);

/* 			StyleSeries styleSeriesGT = (StyleSeries)Asset.GetModule<StyleModule>().ExtractSeries(SampleSeries, RootTimestamp, Mirror);
 			//Style Values
 			//float[] actions = styleSeriesGT.GetStyles(i, new string[] { "Move", "Aiming", "Crouching" });

  			for(int j=0; j<StyleSeries.Styles.Length; j++) {
				StyleSeries.Values[index][j] = (int)ActiveStyle == j ? 1f : 0f; //actions[j], 
			}  

			float t = RootTimestamp + TimeSeries.GetKey(i).Timestamp;
			float durationFactor = 0.25f;
			float thresholdBefore = durationFactor*TargetPose.InitialTimeOffset;
			if(Mathf.Abs(TargetPose.Timestamp - t) <= thresholdBefore){
				float weight = Mathf.Clamp((thresholdBefore - Mathf.Abs(TargetPose.Timestamp - t)) / thresholdBefore, 0f, 1f);
				StyleSeries.Values[index][0] = weight;
				StyleSeries.Values[index][1] = (int)ActiveStyle == 1 ? 1f-weight : 0f;
				StyleSeries.Values[index][2] = (int)ActiveStyle == 2 ? 1f-weight : 0f;
			}

			//Condition after sampling new target
			float thresholdAfter = (1f-durationFactor)*TargetPose.InitialTimeOffset;
			if(Mathf.Abs(TargetPose.Timestamp - t) >= thresholdAfter){
				float weight = Mathf.Clamp((Mathf.Abs(TargetPose.Timestamp - t - thresholdAfter)) / thresholdBefore, 0f, 1f);
				StyleSeries.Values[index][0] = weight;
				StyleSeries.Values[index][1] = (int)ActiveStyle == 1 ? 1f-weight : 0f;
				StyleSeries.Values[index][2] = (int)ActiveStyle == 2 ? 1f-weight : 0f;
			} */


			//StyleSeries.Values[index][0] = 1f;
/* 			//Root Lengths
			RootSeries.SetLength(index,
				Mathf.Lerp(
					RootSeries.GetLength(index),
					rootSeriesGT.GetLength(i),
					TrajectoryControl
				)
			);

			//Root Arcs
			RootSeries.SetArc(index,
				Mathf.Lerp(
					RootSeries.GetArc(index),
					rootSeriesGT.GetArc(i),
					TrajectoryControl
				)
			); */
		}
	}

	protected override void Feed()
	{
		Control();
		//Get Root
		Matrix4x4 root = Actor.GetRoot().GetWorldMatrix();
		Matrix4x4 targetRoot = Asset.GetModule<RootModule>().GetRootTransformation(InputPose.Timestamp, Mirror);
		
		//Input Timeseries - Resolution = 1
		for (int i = 0; i < TimeSeries.KeyCount; i++)
		{
			int index = TimeSeries.GetKey(i).Index;
			NeuralNetwork.FeedXZ(RootSeries.GetPosition(index).GetRelativePositionTo(targetRoot));
			NeuralNetwork.FeedXZ(RootSeries.GetDirection(index).GetRelativeDirectionTo(targetRoot));
			NeuralNetwork.FeedXZ(RootSeries.GetVelocity(index).GetRelativeDirectionTo(targetRoot));
			
/* 			NeuralNetwork.Feed(RootSeries.Lengths[index]);
			NeuralNetwork.Feed(RootSeries.Arcs[index]);  */

			float t = (InputPose.InitialTimeOffset - InputPose.DeltaTimeOffset) - TimeSeries.GetKey(i).Timestamp;
			NeuralNetwork.Feed(t);
			TimeDeltas[i] = t;
			
			//NeuralNetwork.Feed(StyleSeries.Values[index]);
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
/*  		for (int i = 0; i < InputPose.Transformations.Length; i++)
		{
			NeuralNetwork.Feed(InputPose.Transformations[i].GetPosition().GetRelativePositionTo(InputActor.Bones[i].GetTransformation()));
			NeuralNetwork.Feed(InputPose.Transformations[i].GetForward().GetRelativeDirectionTo(InputActor.Bones[i].GetTransformation()));
			NeuralNetwork.Feed(InputPose.Transformations[i].GetUp().GetRelativeDirectionTo(InputActor.Bones[i].GetTransformation()));
			NeuralNetwork.Feed(InputPose.BoneVelocities[i].GetRelativeDirectionTo(InputActor.Bones[i].GetTransformation()));
		}  */

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
	
	
	public float[] GetBoneDistances(Matrix4x4[] from, Matrix4x4[] to) {
		float[] distances = new float[from.Length];
		for(int i=0; i<distances.Length; i++) {
			distances[i] = Vector3.Distance(from[i].GetPosition(), to[i].GetPosition());
		}
		return distances;
	}

	private float GetTargetDistance(){
		return Vector3.Distance(Actor.GetRoot().GetWorldMatrix().GetPosition(), Asset.GetModule<RootModule>().GetRootTransformation(TargetPose.SampleTimestamp, Mirror).GetPosition());
	}

	protected override void Read()
	{
		//Update Past States
		ContactSeries.Increment(0, TimeSeries.Pivot);
		if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.Increment(0, TimeSeries.Pivot);
		if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.Increment(0, TimeSeries.Pivot);

		float timeOffsetWeight = InputPose.GetTimeOffsetWeight();
		timeOffsetWeight = Mathf.Pow(timeOffsetWeight, 2.0f);
		
		Matrix4x4 currentRoot = Actor.GetRoot().GetWorldMatrix();
		Matrix4x4 currentTargetRoot = Asset.GetModule<RootModule>().GetRootTransformation(InputPose.Timestamp, Mirror);
		
		//Back to world space
		Matrix4x4 predictedRoot = Matrix4x4.TRS(NeuralNetwork.ReadXZ().GetRelativePositionFrom(currentRoot), Quaternion.LookRotation(NeuralNetwork.ReadXZ().normalized.GetRelativeDirectionFrom(currentRoot), Vector3.up), Vector3.one);
		Vector3 predictedRootVelocity = NeuralNetwork.ReadXZ().GetRelativeDirectionFrom(currentRoot);

		Matrix4x4 predictedTargetRoot = Matrix4x4.TRS(NeuralNetwork.ReadXZ().GetRelativePositionFrom(currentTargetRoot), Quaternion.LookRotation(NeuralNetwork.ReadXZ().normalized.GetRelativeDirectionFrom(currentTargetRoot), Vector3.up), Vector3.one);
		Vector3 predictedTargetVelocity = NeuralNetwork.ReadXZ().GetRelativeDirectionFrom(currentTargetRoot);
		
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
		//Read Root Style
/* 		for(int j=0; j<StyleSeries.Styles.Length; j++) {
			StyleSeries.Values[TimeSeries.Pivot][j] = Mathf.Lerp(
				StyleSeries.Values[TimeSeries.Pivot][j], 
				NeuralNetwork.Read(0f, 1f), 
				StyleCorrection
			);
		} */

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

			float weight =  timeOffsetWeight; //Mathf.Pow(InputPose.GetTimeOffsetWeight(), Mathf.Pow(Vector3.Distance(RootSeries.Transformations[index].GetPosition(), currentTargetRoot.GetPosition()), 2f));

			Vector3 position = Utility.Interpolate(rootKeyPositions[loopIndex], targetPosition, BlendToTargetSpace ? weight : 0f);
			Vector3 direction = Vector3.Slerp(rootKeyDirections[loopIndex], targetDirection, BlendToTargetSpace ? weight : 0f).normalized;
			Vector3 velocity = Vector3.Lerp(rootKeyVelocities[loopIndex], targetVelocity, BlendToTargetSpace ? weight : 0f); 

			Matrix4x4 m = Matrix4x4.TRS(position, Quaternion.LookRotation(direction, Vector3.up), Vector3.one);
		
			RootSeries.Transformations[index] = Utility.Interpolate(RootSeries.Transformations[index], m,
				TrajectoryCorrection,
				TrajectoryCorrection
			);
			RootSeries.Velocities[index] = Vector3.Lerp(RootSeries.Velocities[index], velocity,
				TrajectoryCorrection
			);
			
			//Styles
/* 			for(int j=0; j<StyleSeries.Styles.Length; j++) {
				StyleSeries.Values[index][j] = Mathf.Lerp(StyleSeries.Values[index][j], NeuralNetwork.Read(0f, 1f), StyleCorrection);
			} */
			loopIndex++;
		}

/* 		for (int i = TimeSeries.PivotKey; i < TimeSeries.KeyCount; i++)
		{
			int index = TimeSeries.GetKey(i).Index;

			float length = NeuralNetwork.Read();
			float arc = NeuralNetwork.Read();
			RootSeries.Lengths[i] = Utility.Interpolate(RootSeries.Lengths[i], length, TrajectoryCorrection);
			RootSeries.Arcs[i] = Utility.Interpolate(RootSeries.Arcs[i], arc, TrajectoryCorrection);
		} */

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
			forwards[i] = forward.normalized;
			upwards[i] = upward.normalized;
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

			TargetPosePrediction[i] = Matrix4x4.TRS(targetRootPosition, Quaternion.LookRotation(targetRootForward.normalized, targetRootUpward.normalized), Vector3.one); 

			float weight = timeOffsetWeight; //Mathf.Pow(TargetPose.GetTimeOffsetWeight(), Mathf.Pow(GetBoneDistances(Actor.GetBoneTransformations(), TargetPose.Transformations)[i], 2f));

			Vector3 position = Vector3.Lerp(positions[i], targetRootPosition, weight);

			if(BlendToTargetSpace) {
				velocities[i] = Vector3.Lerp(velocities[i], targetRootVelocity, weight);
				positions[i] = Vector3.Lerp(Actor.Bones[i].GetTransform().position + velocities[i] / Framerate, position, 0.5f);
				forwards[i] = Vector3.Slerp(forwards[i], targetRootForward.normalized, weight);
				upwards[i] = Vector3.Slerp(upwards[i], targetRootUpward.normalized, weight);
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
		StyleSeries.Interpolate(TimeSeries.Pivot, TimeSeries.Samples.Length);
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

		if(Skeleton == SKELETON.LaFAN1) {
			//FOOT IK
			float[] bodyContacts = new float[3] {
				ContactSeries.Values[TimeSeries.Pivot][0],
				ContactSeries.Values[TimeSeries.Pivot][1],
				ContactSeries.Values[TimeSeries.Pivot][2]
			};
			ProcessFootIK(LeftFootIK, ContactSeries.Values[TimeSeries.Pivot][3], bodyContacts);
			ProcessFootIK(RightFootIK, ContactSeries.Values[TimeSeries.Pivot][4], bodyContacts);
			FinalTransformations = Actor.GetBoneTransformations();
			FinalVelocities = Actor.GetBoneVelocities();

			//LINEAR BLEND IN
			float timeLeft = (InputPose.InitialTimeOffset - InputPose.DeltaTimeOffset);
			float lerpDuration = 0.1f * InputPose.InitialTimeOffset;
			if(timeLeft < lerpDuration){ 
        		float timeElapsed = lerpDuration - timeLeft;
				float weight = timeElapsed / lerpDuration;

				for(int i=0; i<Actor.Bones.Length; i++){
					FinalTransformations[i] = Utility.Interpolate(
						Actor.Bones[i].GetTransformation(),
						TargetPose.Transformations[i],
						weight 
					);

					FinalVelocities[i] = Vector3.Lerp(
						Actor.Bones[i].GetVelocity(),
						TargetPose.BoneVelocities[i],
						weight 
					);
				}		
				//ProcessTargetIK(LeftHandIK, InputPose.Transformations[LeftHand], weight);
			};

			// BLEND OUT
			if(InputPose.DeltaTimeOffset <= lerpDuration) {
				float weight = InputPose.DeltaTimeOffset / lerpDuration;
				for(int i=0; i<Actor.Bones.Length; i++){
					FinalTransformations[i] = Utility.Interpolate(
						Asset.GetFrame(TargetPose.SampleTimestamp).GetBoneTransformation(Asset.Source.FindBone(Actor.Bones[i].GetName()).Name, Mirror),
						Actor.Bones[i].GetTransformation(),
						weight 
					); 

					FinalVelocities[i] = Vector3.Lerp(
						Asset.GetFrame(TargetPose.SampleTimestamp).GetBoneVelocity(Asset.Source.FindBone(Actor.Bones[i].GetName()).Name, Mirror),
						Actor.Bones[i].GetVelocity(),
						weight 
					);
				}	
			}
		}
		
		if(Skeleton == SKELETON.Dog) {
		//Process Contact States
			ProcessFootIKQuadruped(LeftHandIK, ContactSeries.Values[TimeSeries.Pivot][0]);
			ProcessFootIKQuadruped(RightHandIK, ContactSeries.Values[TimeSeries.Pivot][1]);
			ProcessFootIKQuadruped(LeftFootIK, ContactSeries.Values[TimeSeries.Pivot][2]);
			ProcessFootIKQuadruped(RightFootIK, ContactSeries.Values[TimeSeries.Pivot][3]);
		}

		FinalActor.SetBoneTransformations(FinalTransformations);
		FinalActor.SetBoneVelocities(FinalVelocities);

		if(PhaseSelection != PHASES.NoPhases && DrawGating) {
			float[] wGating =  NeuralNetwork.GetOutput("G").AsFloats();
			GatingHistory.Add(wGating);
			if(GatingHistory.Count > 100) {
				GatingHistory.RemoveAt(0);
			}
		}
		
	}

	private void ProcessDistanceUpdate() {
		// solvePosition = targetPosition + distance * (jointPosition - targetPosition).normalized

		for (int i = 0; i < Actor.Bones.Length; i++)
		{
			float distance = NeuralNetwork.Read();
			if((Actor.Bones[i] == Actor.FindBone("LeftToe")) || (Actor.Bones[i] == Actor.FindBone("RightToe"))) continue;

			Actor.Bones[i].GetTransform().position = TargetPose.Transformations[i].GetPosition() + distance * (Actor.Bones[i].GetTransform().position - TargetPose.Transformations[i].GetPosition()).normalized;
		}
	}

	private void ProcessTargetIK(IK ik, Matrix4x4 target, float weight)
	{
		if(!Postprocessing) {
			return;
		}
        for (int i = 0; i < ik.Objectives.Length; i++)
        {
			ik.Objectives[i].SetTarget(
                Utility.Interpolate(
                    ik.Joints[ik.Objectives[i].Joint].Transform.GetWorldMatrix(), // Ausgangspose (verändert sich über mehrere Frames)
                    target, // Target Pose
                    weight
                ) // Interpolation in Frame i
            );
			
			//ik.Objectives[i].SetTarget(Vector3.Lerp(ik.Objectives[i].TargetPosition, target.GetPosition(), weight));
			//ik.Objectives[i].SetTarget(Quaternion.Slerp(ik.Objectives[i].TargetRotation, target.GetRotation(), weight));
        }
        ik.Activation = UltimateIK.ACTIVATION.Linear;
		ik.Iterations = 50;
        ik.Solve();
	}

	private void ProcessFootIK(IK ik, float contact, float[] values)
	{
		if(!Postprocessing) {
			return;
		}
		float weight = contact;
		float threshold = 0.5f;
		if (values.Max() > threshold || (TargetPose.Timestamp - RootTimestamp) <= TargetPose.InitialTimeOffset * 0.1f) weight = 0f;
		ik.Activation = UltimateIK.ACTIVATION.Constant;
		ik.Objectives.First().SetTarget(Vector3.Lerp(ik.Objectives[0].TargetPosition, ik.Joints.Last().Transform.position, 1f - weight));
		ik.Objectives.First().SetTarget(ik.Joints.Last().Transform.rotation);

		ik.Iterations = 50;
		ik.Solve();
	}
	private void ProcessHandIK(IK ik, float contact)
	{
		if(!Postprocessing) {
			return;
		}
		ik.Activation = UltimateIK.ACTIVATION.Linear;
		ik.Objectives.First().SetTarget(Vector3.Lerp(ik.Objectives[0].TargetPosition, ik.Joints.Last().Transform.position, 1f - contact));
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
	private UltiDraw.GUIRect GatingWindow = new UltiDraw.GUIRect(0.25f, 0.85f, 0.5f, 0.3f);
	private UltiDraw.GUIRect NetworkWeightsWindow = new UltiDraw.GUIRect(0.75f, 0.85f, 0.5f, 0.3f);
	private Color ColorRootPrediction = UltiDraw.DarkBlue;
	private Color ColorTargetPrediction = UltiDraw.DarkRed;

	protected override void OnGUIDerived()
	{
		ContactSeries.DrawGUI = DrawGUI;
		StyleSeries.DrawGUI = DrawGUI;
		if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.DrawGUI = DrawGUI;
		if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.DrawGUI = DrawGUI;

		ContactSeries.GUI(GetCamera());
		//StyleSeries.GUI(GetCamera());
		if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.GUI(GetCamera());
		if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.GUI(GetCamera());

		if(ShowBiDirectional) {
			UltiDraw.Begin(GetCamera());
			UltiDraw.OnGUILabel(BlendingRect.GetCenter() + new Vector2(0f, 0.03f), BlendingRect.GetSize(), 0.0175f, "Blending", UltiDraw.Black);
			//UltiDraw.OnGUILabel(new Vector2(BlendingRect.X-0.15f, BlendingRect.Y + 0.1075f) + new Vector2(0f, 0.07f), BlendingRect.GetSize(), 0.0175f, "Time Deltas", UltiDraw.Black);
			UltiDraw.End();
		}
		UltiDraw.Begin(GetCamera());
		//UltiDraw.OnGUILabel(BlendingRect.GetCenter(), BlendingRect.GetSize(), 0.0175f, (InputPose.InitialTimeOffset - InputPose.DeltaTimeOffset).ToString("F1") + "s", UltiDraw.Black);
		UltiDraw.OnGUILabel(BlendingRect.GetCenter(), BlendingRect.GetSize(), 0.0175f, (InputPose.InitialTimeOffset - InputPose.DeltaTimeOffset).ToString("F1") + "s", UltiDraw.White);
		if(DrawGating && PhaseSelection != PHASES.NoPhases) {
			UltiDraw.OnGUILabel(GatingWindow.GetCenter() + new Vector2(0f, -GatingWindow.H * 0.6f), GatingWindow.GetSize(), 0.0175f, "Expert Activation", UltiDraw.Black);
		}
		if(DrawNetworkSpace) {
			UltiDraw.OnGUILabel(NetworkWeightsWindow.GetCenter() + new Vector2(0f, -NetworkWeightsWindow.H * 0.6f), NetworkWeightsWindow.GetSize(), 0.0175f, "Network Weights", UltiDraw.Black);
		}
		UltiDraw.End();
	}

	

	protected override void OnRenderObjectDerived()
	{
		// UltiDraw.PlotFunction(new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.125f), TimeDeltas, -1f, 2f);
/* 		Actor.Draw(TargetPose.Transformations, UltiDraw.Green.Darken(0.5f), Actor.JointColor, Actor.DRAW.Skeleton);
        Actor.Draw(InputPose.Transformations, UltiDraw.Purple.Darken(0.5f), Actor.JointColor, Actor.DRAW.Skeleton);
		UltiDraw.Begin();
		for(int i=0; i<InputPose.BoneVelocities.Length; i++) {
				UltiDraw.DrawArrow(
					InputPose.Transformations[i].GetPosition(),
					InputPose.Transformations[i].GetPosition() + InputPose.BoneVelocities[i] * 0.2f,
					0.75f,
					0.0075f,
					0.05f,
					UltiDraw.Orange.Opacity(0.5f)
				);
		}  
		UltiDraw.End();
		*/
		
		//
		if(ShowBiDirectional) {
			UltiDraw.Begin();
/* 			Matrix4x4 tRoot= Asset.GetModule<RootModule>().GetRootTransformation(InputPose.Timestamp, Mirror);
			UltiDraw.DrawSphere(tRoot.GetPosition(), tRoot.GetRotation(), 0.2f, Color.black); */
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
			//UltiDraw.PlotFunction(new Vector2(BlendingRect.X-0.15f, BlendingRect.Y + 0.1075f), new Vector2(0.2f, 0.1f), TimeDeltas, yMin: -TimeSeries.PastWindow, yMax: Path.TimeInterval + TimeSeries.FutureWindow, thickness: 0.002f, lineColor: ColorRootPrediction.Lerp(ColorTargetPrediction, -TimeDeltas.Min()));
			UltiDraw.End();

			Actor.Draw(RootPosePrediction, ColorRootPrediction, ColorRootPrediction, Actor.DRAW.Skeleton);
			Actor.Draw(TargetPosePrediction, ColorTargetPrediction, ColorTargetPrediction, Actor.DRAW.Skeleton);
		}

		//Ground Truth
		if(DrawDebug) Actor.Draw(Asset.GetFrame(RootTimestamp).GetBoneTransformations(Actor.GetBoneNames(), Mirror), UltiDraw.Orange, UltiDraw.Black, Actor.DRAW.Skeleton);

		if(DrawGating && PhaseSelection != PHASES.NoPhases) {
			UltiDraw.Begin();
			UltiDraw.DrawInterpolationSpace(GatingWindow, GatingHistory, false);
			UltiDraw.End();
		}

		RootSeries.DrawScene = DrawDebug;
		StyleSeries.DrawScene = DrawDebug;
		ContactSeries.DrawScene = DrawDebug;
		if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.DrawScene = DrawDebug;
		if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.DrawScene = DrawDebug;
		RootSeries.Draw(GetCamera());
		//StyleSeries.Draw(GetCamera());
		ContactSeries.Draw(GetCamera());
		if(PhaseSelection == PHASES.LocalPhases) PhaseSeries.Draw(GetCamera());
		if(PhaseSelection == PHASES.DeepPhases) DeepPhaseSeries.Draw(GetCamera());
		
		//Unity.Barracuda.Tensor t = NeuralNetwork.GetOutput("W");

		if(DrawInputSpace) {
			float[] arr = NeuralNetwork.GetInput().AsFloats();
			UltiDraw.Begin();
			UltiDraw.PlotBars(new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.2f), arr, yMin: 0f, backgroundColor:UltiDraw.White);
			UltiDraw.End();
		}

		//Unity.Barracuda.Tensor w0Tensor = NeuralNetwork.GetOutput("W0");
		if (DrawNetworkSpace)
		{
			Unity.Barracuda.Tensor t = new Unity.Barracuda.Tensor(0, 0);
			if (DrawFirstLayer)
			{
				t = NeuralNetwork.GetOutput("W0");
			}
			else if (DrawSecondLayer)
			{
				t = NeuralNetwork.GetOutput("W1");
			}
			else if (DrawThirdLayer)
			{
				t = NeuralNetwork.GetOutput("W2");
			}
			//Debug.Log(t.shape + " or " + t.shape.batch + t.shape.height + t.shape.width + t.shape.channels);

			float[] neuronActivations = new float[t.shape.channels];
			float[] values = t.AsFloats();
			for (int i = 0; i < neuronActivations.Length; i++)
			{
				float result = 0f;
				for (int j = 0; j < t.shape.width; j++)
				{
					result += Math.Abs(values[j + i * t.shape.width]);
				}
				neuronActivations[i] = result;
				//arr[i] = SmoothStep(x, 2f, .8f);
			}
			UltiDraw.Begin();
			UltiDraw.PlotBars(NetworkWeightsWindow.GetCenter(), NetworkWeightsWindow.GetSize(), neuronActivations, yMin: 0f, backgroundColor:UltiDraw.White);
			UltiDraw.End();
/* 			Debug.Log(string.Format("MIN: {0}, MAX: {1} COLS: {2}", arr.Min(), arr.Max(), t.GetCols()));
			UltiDraw.DrawGUIFunction(new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), arr, YMin, YMax, Color.white, Color.black); */
			
		}
		 
	}

	public struct TargetPoseData
	{
		public float SampleTimestamp;
		public float InitialTimeOffset;
		public float Timestamp;
		public float DeltaTimeOffset;
		public Matrix4x4[] Transformations;

		public Vector3[] BoneVelocities;
		public int FrameCounter;
		public TargetPoseData(float sampleTimestamp, InBetweeningModule.SamplePose samplePose)
		{
			this.SampleTimestamp = sampleTimestamp;
			this.InitialTimeOffset = samplePose.TimeOffset;
			this.Timestamp = sampleTimestamp + samplePose.TimeOffset;
			this.DeltaTimeOffset = 0f;
			this.Transformations = samplePose.Pose;
			this.BoneVelocities = samplePose.Velocities;
			this.FrameCounter = 0;
		}

		public float GetTimeOffsetWeight() {
			return Mathf.Clamp(DeltaTimeOffset / InitialTimeOffset, 0f, 1f);
		}
	}
	public ContactSeries GetContactSeries() {
		return ContactSeries;
	}
	public float GetRootTimestamp(){
		return RootTimestamp;
	}

	public TargetPoseData GetInputPose(){
		return InputPose;
	}
}

