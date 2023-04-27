using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Random = System.Random;


namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private float blendTime = 0.1f;
        [SerializeField] private float playbackSpeed = 1.0f;
        [Range(0,1)]
        [SerializeField] private float weight;
        
        [SerializeField] private RuntimeAnimationData runtimeAnimationData;
        
        private Animator animator;
        private PlayableGraph playableGraph;
        private UpdateAnimationPoseJob updateAnimationPoseJob;

        private AnimationGenerator animationGenerator;

        private Clock animatorClock;

        private PastTrajectoryRecorder pastTrajectoryRecorder;
        private RuntimeTrajectory pastLocalTrajectory => pastTrajectoryRecorder.PastLocalTrajectory;
        private RuntimeTrajectory futureLocalTrajectory;

        private AffineTransform[] comparedJointRootSpaceT;
        private List<int> parentIndices;
        
        private PoseIdentifier nextPose;

        private void Start()
        {
            animationGenerator = new AnimationGenerator(runtimeAnimationData, blendTime);
            animatorClock = new Clock();
            pastTrajectoryRecorder = new PastTrajectoryRecorder(runtimeAnimationData.trajectoryFeatureDefinition, transform);
            futureLocalTrajectory = new RuntimeTrajectory();
            comparedJointRootSpaceT = new AffineTransform[runtimeAnimationData.ComparedJointTransformGroupLen];
            parentIndices = runtimeAnimationData.rig.GenerateParentIndices();

            animator = GetComponent<Animator>();
            CreatePlayableGraph();
        }
    
        private void Update()
        {
            animatorClock.Tick(Time.deltaTime);

            if (CheckNeedUpdatePose())
            {
                SwitchPose();
            }
            
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

        private bool CheckNeedUpdatePose()
        {
            if (animatorClock.TimeFromLastTimeStamp > updateInterval)
            {
                animatorClock.SetTimeStamp();
                return true;
            }

            return false;
        }

        private void SwitchPose()
        {
            nextPose = SearchNextPose();
            animationGenerator.BeginPlayPose(nextPose);
        }

        private PoseIdentifier SearchNextPose()
        {
            var poseIdentifier = new PoseIdentifier();
            float minCost = float.MaxValue;

            ComputeComparedJointTransform();
            
            foreach (var featureVector in runtimeAnimationData.GetPlayablePoseFeatureVectors(updateInterval + blendTime))
            {
                var cost = ComputeCost(featureVector);
                if (cost < minCost)
                {
                    poseIdentifier = featureVector.poseIdentifier;
                    minCost = cost;
                }
            }
            
            nextPose = poseIdentifier;
            return poseIdentifier;
        }

        private float ComputeCost(in FeatureVector featureVector)
        {
            float futureTrajCost = 0;
            float pastTrajCost = 0;
            int i = 0;

            foreach (var trajectoryPoint in futureLocalTrajectory.trajectoryData)
            {
                futureTrajCost += Vector3.Distance(featureVector.trajectory[i], trajectoryPoint.transform.t);
                i++;
            }

            foreach (var trajectoryPoint in pastLocalTrajectory.trajectoryData)
            {
                pastTrajCost += Vector3.Distance(featureVector.trajectory[i], trajectoryPoint.transform.t);
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

        public void SetFutureLocalTrajectory(RuntimeTrajectory futureTrajectory)
        {
            futureLocalTrajectory = futureTrajectory;
        }
    }
}