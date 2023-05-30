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
        private Vector3[] directions = new Vector3[10];
        
        private bool showCurrTraj = true;
        private bool showNextPoseTraj = true;
        
        private void OnDrawGizmos()
        {
            if (Application.isPlaying && enabled)
            {
                if (showCurrTraj)
                {
                    DrawTrajectory(pastLocalTrajectory, Color.blue);
                    DrawTrajectory(futureLocalTrajectory, Color.cyan);
                }

                if (showNextPoseTraj)
                {
                    var debugNextPoseFeatureVector = runtimeAnimationData.GetFeatureVector(nextPose);
                    DrawTrajectory(debugNextPoseFeatureVector.trajectory, Color.yellow);
                }
            }
        }

        private void DrawTrajectory(RuntimeTrajectory trajectory, Color color)
        {
            int length = trajectory.trajectoryData.Count + 1;
            positions[0] = transform.position;
            directions[0] = transform.forward;
            
            int index = 1;
            foreach (var trajectoryPoint in trajectory.trajectoryData)
            {
                var localPosition = new Vector4
                    (trajectoryPoint.transform.t.x, trajectoryPoint.transform.t.y, trajectoryPoint.transform.t.z, 1.0f);

                var localToWorldMatrix = transform.localToWorldMatrix;
                positions[index] =  localToWorldMatrix * localPosition;
                directions[index] = localToWorldMatrix * new Vector4(
                    trajectoryPoint.transform.Forward.x, trajectoryPoint.transform.Forward.y, trajectoryPoint.transform.Forward.z, 0.0f);
                
                index++;
            }
            
            Gizmos.color = color;
            for (int i = 0; i < length; i++)
            {
                Gizmos.DrawSphere(positions[i], 0.05f);
                GizmosEx.DrawArrow(positions[i], directions[i]);
            }

            for (int i = 0; i < length - 1; i++)
            {
                Gizmos.DrawLine(positions[i], positions[i + 1]);
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
                    directions[i] = transform.forward;
                    continue;
                }

                var trajectoryPoint = trajectory[tIndex];
                var localPosition = new Vector4
                    (trajectoryPoint.t.x, trajectoryPoint.t.y, trajectoryPoint.t.z, 1.0f);
                
                var localToWorldMatrix = transform.localToWorldMatrix;
                positions[i] =  localToWorldMatrix * localPosition;
                directions[i] = localToWorldMatrix * new Vector4(
                    trajectoryPoint.Forward.x, trajectoryPoint.Forward.y, trajectoryPoint.Forward.z, 0.0f);
                
                tIndex++;
            }
            
            int length = trajectory.Count + 1;

            Gizmos.color = color;
            for (int i = 0; i < length; i++)
            {
                Gizmos.DrawSphere(positions[i], 0.05f);
                GizmosEx.DrawArrow(positions[i], directions[i]);
            }

            for (int i = 0; i < length - 1; i++)
            {
                Gizmos.DrawLine(positions[i], positions[i + 1]);
            }
        }

    }
}
