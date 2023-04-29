using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        private Vector3[] positions = new Vector3[10];
        
        private void OnDrawGizmos()
        {
            if (Application.isPlaying && enabled)
            {
                DrawTrajectory(pastLocalTrajectory, Color.blue);
                DrawTrajectory(futureLocalTrajectory, Color.cyan);

                var debugNextPoseFeatureVector = runtimeAnimationData.GetFeatureVector(nextPose);
                DrawTrajectory(debugNextPoseFeatureVector.trajectory, Color.yellow);
            }
        }

        private void DrawTrajectory(RuntimeTrajectory trajectory, Color color)
        {
            int length = trajectory.trajectoryData.Count + 1;
            positions[0] = transform.position;
            
            int index = 1;
            foreach (var trajectoryPoint in trajectory.trajectoryData)
            {
                var localPosition = new Vector4
                    (trajectoryPoint.transform.t.x, trajectoryPoint.transform.t.y, trajectoryPoint.transform.t.z, 1.0f);
                positions[index] =  transform.localToWorldMatrix * localPosition;
                index++;
            }
            
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
        
        private void DrawTrajectory(List<float3> trajectory, Color color)
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
                    (trajectoryPoint.x, trajectoryPoint.y, trajectoryPoint.z, 1.0f);
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
