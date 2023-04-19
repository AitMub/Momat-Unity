using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Random = System.Random;


namespace Momat.Runtime
{
    public class MomatAnimator : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 2f;
        [SerializeField] private float blendTime = 0.1f;
        [SerializeField] private float playbackSpeed = 1.0f;
        
        [SerializeField] private RuntimeAnimationData runtimeAnimationData;
        
        private Animator animator;
        private PlayableGraph playableGraph;
        private UpdateAnimationPoseJob updateAnimationPoseJob;

        private AnimationGenerator animationGenerator;

        private Clock animatorClock;

        void Start()
        {
            animatorClock = new Clock();
            animationGenerator = new AnimationGenerator(runtimeAnimationData, blendTime, playbackSpeed);
            
            animator = GetComponent<Animator>();
            CreatePlayableGraph();
        }
    
        void Update()
        {
            animatorClock.Tick(Time.deltaTime);

            if (CheckNeedUpdatePose())
            {
                SwitchPose();
            }
            
            animationGenerator.Update(Time.deltaTime);
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
            var nextPose = SearchNextPose();
            animationGenerator.BeginPlayPose(nextPose);
        }

        private PoseIdentifier SearchNextPose()
        {
            PoseIdentifier poseIdentifier;
            poseIdentifier.animationID = UnityEngine.Random.Range(1, 2);
            poseIdentifier.frameID = UnityEngine.Random.Range(0, 1);
            return poseIdentifier;
        }

        private void OnDisable()
        {
            updateAnimationPoseJob.Dispose();
            playableGraph.Destroy();
        }
    }

}