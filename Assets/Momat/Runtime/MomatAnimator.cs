using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;


namespace Momat.Runtime
{
    public class MomatAnimator : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private RuntimeAnimationData runtimeAnimationData;
        protected Animator animator;
        protected PlayableGraph playableGraph;
        private UpdateAnimationPoseJob job;
        public float deltaTime;
        public static int t = 0;
        [Range(0,65)]
        public int t1;

        // Start is called before the first frame update
        void Start()
        {
            animator = GetComponent<Animator>();
            CreatePlayableGraph();
        }
    
        // Update is called once per frame
        void Update()
        {
            t = t1;
        }
        
        void CreatePlayableGraph()
        {
            var deltaTimeProperty = animator.BindSceneProperty(transform, typeof(MomatAnimator), "deltaTime");
            
            job = new UpdateAnimationPoseJob();
            job.Setup(animator, GetComponentsInChildren<Transform>(), runtimeAnimationData, deltaTimeProperty);
            
            playableGraph = PlayableGraph.Create($"Momat_{transform.name}");
            var output = AnimationPlayableOutput.Create(playableGraph, "output", animator);
            var playable = AnimationScriptPlayable.Create(playableGraph, job);
            output.SetSourcePlayable(playable);

            playableGraph.Play();
        }

        private void OnDisable()
        {
            job.Dispose();
            playableGraph.Destroy();
        }
    }

}