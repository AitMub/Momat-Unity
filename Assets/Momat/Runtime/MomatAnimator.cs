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
        [SerializeField] private AnimationClip[] animationClips;
        protected Animator animator;
        protected PlayableGraph playableGraph;
        // private UpdateAnimationPoseJob job;
        

        // Start is called before the first frame update
        void Start()
        {
            animator = GetComponent<Animator>();
            // CreatePlayableGraph();
        }
    
        // Update is called once per frame
        void Update()
        {
            
        }
        
        /*void CreatePlayableGraph()
        {
            // var deltaTimeProperty = animator.BindSceneProperty(transform, typeof(Kinematica), "_deltaTime");
            
            job = new UpdateAnimationPoseJob();
            job.Setup(animator, GetComponentsInChildren<Transform>());
            
            playableGraph = PlayableGraph.Create($"Momat_{transform.name}");
            var output = AnimationPlayableOutput.Create(playableGraph, "output", animator);
            var playable = AnimationScriptPlayable.Create(playableGraph, job);
            output.SetSourcePlayable(playable);

            playableGraph.Play();
        }*/
    }

}