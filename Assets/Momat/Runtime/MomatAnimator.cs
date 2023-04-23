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
        
        [SerializeField] private RuntimeAnimationData runtimeAnimationData;
        
        private Animator animator;
        private PlayableGraph playableGraph;
        private UpdateAnimationPoseJob updateAnimationPoseJob;

        private AnimationGenerator animationGenerator;

        private Clock animatorClock;

        private PastTrajectoryRecorder pastTrajectoryRecorder;
        private RuntimeTrajectory pastLocalTrajectory => pastTrajectoryRecorder.PastLocalTrajectory;
        private RuntimeTrajectory futureLocalTrajectory;
        
        private PoseIdentifier nextPose;

        private void Start()
        {
            animationGenerator = new AnimationGenerator(runtimeAnimationData, blendTime, playbackSpeed);
            animatorClock = new Clock();
            pastTrajectoryRecorder = new PastTrajectoryRecorder(runtimeAnimationData.trajectoryFeatureDefinition, transform);
            futureLocalTrajectory = new RuntimeTrajectory();

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
            float cost = 0;
            int i = 0;

            foreach (var trajectoryPoint in futureLocalTrajectory.trajectoryData)
            {
                cost += Vector3.Distance(featureVector.trajectory[i], trajectoryPoint.transform.t);
                i++;
            }

            foreach (var trajectoryPoint in pastLocalTrajectory.trajectoryData)
            {
                cost += Vector3.Distance(featureVector.trajectory[i], trajectoryPoint.transform.t);
                i++;
            }

            return cost;
        }

        private void OnDisable()
        {
            updateAnimationPoseJob.Dispose();
            playableGraph.Destroy();
        }

        public void SetFutureLocalTrajectory(RuntimeTrajectory futureTrajectory)
        {
            this.futureLocalTrajectory = futureTrajectory;
        }
    }
}