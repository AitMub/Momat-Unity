using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Quaternion = System.Numerics.Quaternion;
using Random = System.Random;


namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private float blendTime = 0.1f;
        [SerializeField] [Range(0,1)] private float weight;
        [SerializeField] [Range(0,2)] private float couldContinuePlayCost = 0.3f;

        [SerializeField] private RuntimeAnimationData runtimeAnimationData;
        private float FrameRate => runtimeAnimationData.frameRate;
        
        private Animator animator;
        private PlayableGraph playableGraph;
        private UpdateAnimationPoseJob updateAnimationPoseJob;

        private IMomatAnimatorState currentState;

        private AnimationGenerator animationGenerator;

        private Clock animatorClock;

        private PastTrajectoryRecorder pastTrajectoryRecorder;
        private RuntimeTrajectory pastLocalTrajectory => pastTrajectoryRecorder.PastLocalTrajectory;
        private RuntimeTrajectory futureLocalTrajectory;

        private AffineTransform[] comparedJointRootSpaceT;
        private List<int> parentIndices;
        
        private PoseIdentifier nextPose;
        private int toPlayEventID;
        
        public delegate float CostComputeFunc(in FeatureVector featureVector);

        private CostComputeFunc costComputeFunc;

        private void Start()
        {
            animationGenerator = new AnimationGenerator(runtimeAnimationData, blendTime);
            animatorClock = new Clock();
            pastTrajectoryRecorder = new PastTrajectoryRecorder(runtimeAnimationData.trajectoryFeatureDefinition, transform);
            futureLocalTrajectory = new RuntimeTrajectory();
            comparedJointRootSpaceT = new AffineTransform[runtimeAnimationData.ComparedJointTransformGroupLen];
            parentIndices = runtimeAnimationData.rig.GenerateParentIndices();

            toPlayEventID = EventClipData.InvalidEventID;
            
            costComputeFunc = ComputeCost;

            SetState(new IdleState());

            animator = GetComponent<Animator>();
            CreatePlayableGraph();
        }
    
        private void Update()
        {
            animatorClock.Tick(Time.deltaTime);

            currentState.Update();
            
            animationGenerator.Update(Time.deltaTime);
            pastTrajectoryRecorder.Record(transform, Time.deltaTime);
        }
        
        private void CreatePlayableGraph()
        {
            updateAnimationPoseJob = new UpdateAnimationPoseJob();
            updateAnimationPoseJob.Setup(animator, GetComponentsInChildren<Transform>(), runtimeAnimationData, animationGenerator);
            
            playableGraph = PlayableGraph.Create($"Momat_{transform.name}_Graph");
            var output = AnimationPlayableOutput.Create(playableGraph, "output", animator);
            var playable = AnimationScriptPlayable.Create(playableGraph, updateAnimationPoseJob);
            output.SetSourcePlayable(playable);

            playableGraph.Play();
        }

        private void BeginPlayPose(PoseIdentifier poseIdentifier, float blendTime, EBlendMode blendMode = EBlendMode.TwoAnimPlayingBlend)
        {
            nextPose = poseIdentifier;
            animationGenerator.BeginPlayPose(poseIdentifier, blendTime, blendMode);
        }

        private PoseIdentifier SearchPoseInFeatureSet(IEnumerable<FeatureVector> featureVectors)
        {
            var poseIdentifier = new PoseIdentifier();
            float minCost = float.MaxValue;

            ComputeComparedJointTransform();
            
            foreach (var featureVector in featureVectors)
            {
                var cost = costComputeFunc(featureVector);
                if (cost < minCost)
                {
                    poseIdentifier = featureVector.poseIdentifier;
                    minCost = cost;
                }
            }
            
            return poseIdentifier;
        }

        private IEnumerable<FeatureVector> GetAllMotionAnimFeatureVectors()
        {
            return runtimeAnimationData.GetPlayablePoseFeatureVectors(updateInterval + blendTime, EAnimationType.EMotion);
        }
        
        private IEnumerable<FeatureVector> GetAllIdleAnimFeatureVectors()
        {
            return runtimeAnimationData.GetPlayablePoseFeatureVectors(blendTime, EAnimationType.EIdle);
        }

        private IEnumerable<FeatureVector> GetEventBeginPhasePoseFeatureVectors(int eventID)
        {
            for (int i = 0; i < runtimeAnimationData.eventClipDatas.Length; i++)
            {
                if (runtimeAnimationData.eventClipDatas[i].eventID == eventID)
                {
                    var animationIndex = i + runtimeAnimationData.animationTypeOffset[(int)EAnimationType.EEvent];
                    foreach (var featureVector in runtimeAnimationData.GetPlayablePoseFeatureVectors(animationIndex, 
                                     runtimeAnimationData.eventClipDatas[i].prepareFrame, 
                                     runtimeAnimationData.eventClipDatas[i].beginFrame))
                    {
                        yield return featureVector;
                    }
                }
            }
        }

        private bool IsIdle()
        {
            return IsIntendingToMove() == false && IsMoving() == false;
        }

        private bool IsIntendingToMove()
        {
            var futureTrajPointDistance = 0f;
            if (futureLocalTrajectory.trajectoryData == null 
                || futureLocalTrajectory.trajectoryData.Count == 0
                || pastLocalTrajectory.trajectoryData == null
                || pastLocalTrajectory.trajectoryData.Count == 0)
            {
                return false;
            }
            
            var trajectoryPoint = futureLocalTrajectory.trajectoryData.First;
            while (trajectoryPoint.Next != null)
            {
                futureTrajPointDistance += trajectoryPoint.Value.DistanceTo(trajectoryPoint.Next.Value);
                trajectoryPoint = trajectoryPoint.Next;
            }
            futureTrajPointDistance += trajectoryPoint.Value.DistanceTo(Vector3.zero); // distance to local position

            return futureTrajPointDistance > 0.05f;
        }

        private bool IsMoving()
        {
            var pastTrajPointDistance = 0f;
            var trajectoryPoint = pastLocalTrajectory.trajectoryData.First;
            pastTrajPointDistance += trajectoryPoint.Value.DistanceTo(Vector3.zero);
            while (trajectoryPoint.Next != null)
            {
                pastTrajPointDistance += trajectoryPoint.Value.DistanceTo(trajectoryPoint.Next.Value);
                trajectoryPoint = trajectoryPoint.Next;
            }

            return pastTrajPointDistance > 0.1f;
        }

        private int GetAnimationFrameCnt(int animationID)
        {
            return runtimeAnimationData.animationFrameNum[animationID];
        }

        private float ComputeCost(in FeatureVector featureVector)
        {
            float futureTrajCost = 0;
            float pastTrajCost = 0;
            int i = 0;

            foreach (var trajectoryPoint in futureLocalTrajectory.trajectoryData)
            {
                futureTrajCost += Vector3.Distance(featureVector.trajectory[i].t, trajectoryPoint.transform.t);
                var angleCost = Vector3.Angle(featureVector.trajectory[i].Forward, trajectoryPoint.transform.Forward) / 180f;
                futureTrajCost += angleCost;
                i++;
            }

            foreach (var trajectoryPoint in pastLocalTrajectory.trajectoryData)
            {
                pastTrajCost += Vector3.Distance(featureVector.trajectory[i].t, trajectoryPoint.transform.t);
                i++;
            }

            float poseCost = 0;
            for (int j = 0; j < featureVector.jointRootSpaceT.Count; j++)
            {
                poseCost += Vector3.Distance(featureVector.jointRootSpaceT[j].t, comparedJointRootSpaceT[j].t);
            }
            
            return (futureTrajCost * 0.8f + pastTrajCost * 0.2f) * weight + poseCost * (1 - weight);
        }

        private void ComputeComparedJointTransform()
        {
            // these transform are based on RuntimeAnimationData instead of transform of joint
            // in game, this is because when setting local transform in UpdateAnimationPoseJob,
            // the transform we set would be modified by unity animator
            for (int i = 0; i < comparedJointRootSpaceT.Length; i++)
            {
                // to-do: Cache jointIndex in Start()
                var jointName = runtimeAnimationData.poseFeatureDefinition.comparedJoint[i];
                var jointIndex = runtimeAnimationData.rig.GetJointIndexFromName(jointName);

                var rootSpaceT = AffineTransform.identity;
                while (jointIndex != 0)
                {
                    rootSpaceT = animationGenerator.GetCurrPoseJointTransform(jointIndex) * rootSpaceT;
                    jointIndex = parentIndices[jointIndex];
                }

                comparedJointRootSpaceT[i] = rootSpaceT;
            }
        }

        private void OnDisable()
        {
            updateAnimationPoseJob.Dispose();
            playableGraph.Destroy();
        }
    }
}