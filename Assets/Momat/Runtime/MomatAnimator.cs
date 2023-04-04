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

        public Transform target;
        
        public Transform[] transforms;

        // Start is called before the first frame update
        void Start()
        {            
            transforms = GetComponentsInChildren<Transform>();
            Vector3 t = new Vector3(5.74800652e-10f, 0.391991794f, 3.7252903e-09f);
            Quaternion q = Quaternion.Euler(55.9311409f,19.4762878f,20.2901363f);
            
            // Quaternion q_diff = Quaternion.Euler(305.066101f,356.521271f,176.025391f);
            
            /*var mat = transforms[3].localToWorldMatrix * Matrix4x4.TRS(t, q, Vector3.one) * transforms[4].worldToLocalMatrix;
            Debug.Log(mat.rotation.eulerAngles);
            Transform[] targetTransforms = target.GetComponentsInChildren<Transform>();
            for (int i = 0; i < targetTransforms.Length; i++)
            {
                if (targetTransforms[i].name == "def_l_ankle")
                {
                    mat = targetTransforms[i - 1].worldToLocalMatrix * mat * targetTransforms[i].localToWorldMatrix;
                    Debug.Log(mat.rotation.eulerAngles);
                }
            }*/
            
            animator = GetComponent<Animator>();
            CreatePlayableGraph();
        }
    
        // Update is called once per frame
        void Update()
        {
            t = t1;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                /*var childs = transform.GetComponentsInChildren<Transform>();
                for (int i = 0; i < childs.Length; i++)
                {
                    var child = childs[i];
                    Debug.Log( i + " " + child.name + "pos: " + child.position + "  rot: " + child.rotation.eulerAngles);
                    Debug.Log(child.worldToLocalMatrix);
                }*/
            }
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