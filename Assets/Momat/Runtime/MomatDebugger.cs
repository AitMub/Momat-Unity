using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Momat.Runtime
{
    public class MomatDebugger : MonoBehaviour
    {
        [SerializeField] private RuntimeAnimationData runtimeAnimationData;

        private Animator animator;
        private PlayableGraph playableGraph;
        private UpdateAnimationPoseJob updateAnimationPoseJob;
        
        private AnimationGenerator animationGenerator;

        [SerializeField] private PoseIdentifier playPose;

        [SerializeField] private int debugShowAnimationID;
        [SerializeField] private int debugShowFrameID;
        [SerializeField] private bool debugShowTrajectory;
        
        private Vector3[] positions = new Vector3[10];
        
        // Start is called before the first frame update
        void Start()
        {
            animationGenerator = new AnimationGenerator(runtimeAnimationData, 0.1f);
            animationGenerator.BeginPlayPose(playPose);
            
            animator = GetComponent<Animator>();
            CreatePlayableGraph();
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

        // Update is called once per frame
        void Update()
        {
            animationGenerator.Update(Time.deltaTime);
        }
        
        private void OnDisable()
        {
            updateAnimationPoseJob.Dispose();
            playableGraph.Destroy();
        }

        private void OnDrawGizmos()
        {
            if (debugShowTrajectory && runtimeAnimationData != null)
            {
                var debugPoseFeatureVector = runtimeAnimationData.GetFeatureVector(new PoseIdentifier
                    { animationID = debugShowAnimationID, frameID = debugShowFrameID });
                DrawTrajectory(debugPoseFeatureVector.trajectory, Color.green);
            }
        }

        private void DrawTrajectory(List<AffineTransform> trajectory, Color color)
        {
            var timeStamps = runtimeAnimationData.trajectoryFeatureDefinition.trajectoryTimeStamps;

            int tIndex = 0;
            for (int i = 0; i < timeStamps.Count + 1; i++)
            {
                if (i > 0 && timeStamps[i - 1] > 0 && timeStamps[i] < 0)
                {
                    positions[i] = transform.position;
                    continue;
                }

                var trajectoryPoint = trajectory[tIndex];
                var localPosition = new Vector4
                    (trajectoryPoint.t.x, trajectoryPoint.t.z, trajectoryPoint.t.z, 1.0f);
                positions[i] =  transform.localToWorldMatrix * localPosition;
                
                tIndex++;
            }
            
            int length = trajectory.Count + 1;

            Gizmos.color = color;
            for (int i = 0; i < length; i++)
            {
                Gizmos.DrawSphere(positions[i], 0.05f);
            }

            for (int i = 0; i < length - 1; i++)
            {
                Gizmos.DrawLine(positions[i], positions[i + 1]);
            }
        }
    }
}

