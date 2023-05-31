using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEditor;
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
        
        // animation data
        [SerializeField] private RuntimeAnimationData runtimeAnimationData;
        private float FrameRate => runtimeAnimationData.frameRate;

        private Animator animator;
        private PlayableGraph playableGraph;
        private UpdateAnimationPoseJob updateAnimationPoseJob;

        private IMomatAnimatorState currentState;

        private AnimationGenerator animationGenerator;

        private Clock animatorClock;
        
        private PoseIdentifier nextPose;
        
        // runtime feature vector
        private PastTrajectoryRecorder pastTrajectoryRecorder;
        private RuntimeTrajectory pastLocalTrajectory => pastTrajectoryRecorder.PastLocalTrajectory;
        private RuntimeTrajectory futureLocalTrajectory;

        private AffineTransform[] comparedJointRootSpaceT;
        private List<int> parentIndices;
        
        private int toPlayEventID;

        public delegate float CostComputeFunc(in FeatureVector featureVector1, in FeatureVector featureVector2);
        private CostComputeFunc costComputeFunc;

        private void Start()
        {
            animationGenerator = new AnimationGenerator(runtimeAnimationData, blendTime);
            animatorClock = new Clock();
            pastTrajectoryRecorder = new PastTrajectoryRecorder(runtimeAnimationData.trajectoryFeatureDefinition, transform);
            futureLocalTrajectory = new RuntimeTrajectory();
            comparedJointRootSpaceT = new AffineTransform[runtimeAnimationData.ComparedJointTransformGroupLen];
            parentIndices = runtimeAnimationData.rig.GenerateParentIndices();

            PrepareSearchPoseJob();

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
            
            // user may add child gameobjects to this gameobject, but updateAnimationPoseJob doesn't need them
            var transforms =  CollectPrefabContainTransform();
            updateAnimationPoseJob.Setup(animator, transforms, runtimeAnimationData, animationGenerator);
            
            playableGraph = PlayableGraph.Create($"Momat_{transform.name}_Graph");
            var output = AnimationPlayableOutput.Create(playableGraph, "output", animator);
            var playable = AnimationScriptPlayable.Create(playableGraph, updateAnimationPoseJob);
            output.SetSourcePlayable(playable);

            playableGraph.Play();
        }

        private Transform[] CollectPrefabContainTransform()
        {
            var allTransformsInGameobjects = new LinkedList<Transform>(GetComponentsInChildren<Transform>());
            var array = runtimeAnimationData.rig.joints;
            
            LinkedListNode<Transform> linkedListNode = allTransformsInGameobjects.First;
            int arrayIndex = 0;

            while (linkedListNode != null && arrayIndex < array.Length)
            {
                if (linkedListNode.Value.name.Equals(array[arrayIndex].name))
                {
                    linkedListNode = linkedListNode.Next;
                    arrayIndex++;
                }
                else
                {
                    var nextNode = linkedListNode.Next;
                    allTransformsInGameobjects.Remove(linkedListNode);
                    linkedListNode = nextNode;
                }
            }

            // remove remaining node
            while (linkedListNode != null)
            {
                var nextNode = linkedListNode.Next;
                allTransformsInGameobjects.Remove(linkedListNode);
                linkedListNode = nextNode;
            }

            return allTransformsInGameobjects.ToArray();
        }

        private void BeginPlayPose(PoseIdentifier poseIdentifier, float blendTime, EBlendMode blendMode = EBlendMode.TwoAnimPlayingBlend)
        {
            nextPose = poseIdentifier;
            animationGenerator.BeginPlayPose(poseIdentifier, blendTime, blendMode);
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

        private FeatureVector GetCurrFeatureVector()
        {
            ComputeComparedJointTransform();
            
            var featureVector = new FeatureVector
            {
                trajectory = new List<AffineTransform>
                    (futureLocalTrajectory.trajectoryData.Count + pastLocalTrajectory.trajectoryData.Count),
                jointRootSpaceT = new List<AffineTransform>(comparedJointRootSpaceT)
            };

            foreach (var trajectoryPoint in futureLocalTrajectory.trajectoryData)
            {
                featureVector.trajectory.Add(trajectoryPoint.transform);
            }
            foreach (var trajectoryPoint in pastLocalTrajectory.trajectoryData)
            {
                featureVector.trajectory.Add(trajectoryPoint.transform);
            }
            
            return featureVector;
        }

        private static float ComputeCost(in FeatureVector featureVector1, in FeatureVector featureVector2)
        {
            float futureTrajCost = 0;
            float pastTrajCost = 0;

            for (int i = 0; i < featureVector1.trajectory.Count / 2; i++)
            {
                futureTrajCost += Vector3.Distance(
                        featureVector1.trajectory[i].t, featureVector2.trajectory[i].t);
                var angleCost = UnityEngine.Quaternion.Angle(
                    featureVector1.trajectory[i].q, featureVector2.trajectory[i].q)  / 180f;
                futureTrajCost += angleCost;
            }
            
            for (int i = featureVector1.trajectory.Count / 2; i < featureVector1.trajectory.Count; i++)
            {
                pastTrajCost += Vector3.Distance(
                        featureVector1.trajectory[i].t, featureVector2.trajectory[i].t);
            }
            

            float poseCost = 0;
            for (int i = 0; i < featureVector1.jointRootSpaceT.Count; i++)
            {
                poseCost += Vector3.Distance(
                    featureVector1.jointRootSpaceT[i].t, featureVector2.jointRootSpaceT[i].t);
            }

            var weight = 0.45f;
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

            poseIDs.Dispose();
            trajectoryPoints.Dispose();
            jointRootSpaceT.Dispose();
            minCostForEachJob.Dispose();
            minCostPose.Dispose();
        }
    }
}