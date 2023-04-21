using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                DrawPostTrajectory();
            }
        }

        private void DrawPostTrajectory()
        {
            var positions = new Vector3[postTrajectoryRecorder.PostTrajectory.trajectoryData.Count + 1];
            positions[0] = transform.position;

            int index = 1;
            foreach (var trajectoryPoint in postTrajectoryRecorder.PostTrajectory.trajectoryData)
            {
                positions[index] = trajectoryPoint.transform.t;
                index++;
            }
            
            Gizmos.color = Color.blue;
            for (int i = 0; i < positions.Length; i++)
            {
                Gizmos.DrawSphere(positions[i], 0.05f);
            }

            for (int i = 0; i < positions.Length - 1; i++)
            {
                Gizmos.DrawLine(positions[i], positions[i + 1]);
            }
        }
    }
}
