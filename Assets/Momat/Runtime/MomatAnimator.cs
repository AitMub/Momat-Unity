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
        [SerializeField] private int playbackFrameRate = 30;
        
        [SerializeField] private RuntimeAnimationData runtimeAnimationData;
        
        private Animator animator;
        private PlayableGraph playableGraph;
        private UpdateAnimationPoseJob job;

        private AnimationGenerator animationGenerator;

        private Clock animatorClock;

        void Start()
        {
            animationGenerator = new AnimationGenerator(runtimeAnimationData, transform, blendTime, playbackFrameRate);
            animatorClock = new Clock();
            animatorClock.SetTimeStamp();
            
            animator = GetComponent<Animator>();
            CreatePlayableGraph();
        }
    
        void Update()
        {
            UpdateClock();

            if (CheckNeedUpdatePose())
            {
                SwitchPose();
            }
            
            UpdatePose();
        }
        
        private void CreatePlayableGraph()
        {
            job = new UpdateAnimationPoseJob();
            job.Setup(animator, GetComponentsInChildren<Transform>(), runtimeAnimationData, animationGenerator);
            
            playableGraph = PlayableGraph.Create($"Momat_{transform.name}_Graph");
            var output = AnimationPlayableOutput.Create(playableGraph, "output", animator);
            var playable = AnimationScriptPlayable.Create(playableGraph, job);
            output.SetSourcePlayable(playable);

            playableGraph.Play();
        }

        private void UpdateClock()
        {
            animatorClock.Tick(Time.deltaTime);
            animationGenerator.UpdateClock(Time.deltaTime);
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
            animationGenerator.BeginBlendIntoPose(nextPose);
        }

        private void UpdatePose()
        {
            animationGenerator.UpdatePose();
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
            job.Dispose();
            playableGraph.Destroy();
        }
    }

}