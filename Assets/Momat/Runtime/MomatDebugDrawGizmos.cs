using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        private Vector3[] positions = new Vector3[10];
        
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                DrawTrajectory(postTrajectory, Color.blue);
                DrawTrajectory(futureTrajectory, Color.blue);
            }
        }

        private void DrawTrajectory(RuntimeTrajectory trajectory, Color color)
        {
            int length = trajectory.trajectoryData.Count + 1;
            positions[0] = transform.position;
            
            int index = 1;
            foreach (var trajectoryPoint in trajectory.trajectoryData)
            {
                positions[index] = trajectoryPoint.transform.t;
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
    }
}
