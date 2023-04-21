using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Momat.Runtime
{
    public class PostTrajectoryRecorder
    {
        private RuntimeTrajectory trajectory;
        private RuntimeTrajectory postTrajectory;
        private readonly float earliestTimeStamp;

        public RuntimeTrajectory PostTrajectory => postTrajectory;

        public PostTrajectoryRecorder(List<float> recordTimeStamp, Transform oriTransform)
        {
            earliestTimeStamp = recordTimeStamp[^1];

            trajectory = new RuntimeTrajectory();
            postTrajectory = new RuntimeTrajectory();

            for (int i = 0; i < recordTimeStamp.Count; i++)
            {
                postTrajectory.trajectoryData.AddLast(new TrajectoryPoint(oriTransform, recordTimeStamp[i]));
            }
        }

        public void Record(Transform transform, float deltaTime)
        {
            var iter = trajectory.trajectoryData.First;
            while (iter != null)
            {
                if (iter.Value.timeStamp < earliestTimeStamp)
                {
                    trajectory.trajectoryData.Remove(iter);
                }

                var iterForPostTraj = postTrajectory.trajectoryData.First;
                while (iterForPostTraj != null)
                {
                    if (iter.Value.timeStamp > iterForPostTraj.Value.timeStamp &&
                        iter.Value.timeStamp - deltaTime < iterForPostTraj.Value.timeStamp)
                    {
                        iterForPostTraj.Value.transform = iter.Value.transform;
                    }

                    iterForPostTraj = iterForPostTraj.Next;
                }

                iter.Value.timeStamp -= deltaTime;

                iter = iter.Next;
            }

            trajectory.trajectoryData.AddFirst(new TrajectoryPoint(transform));
        }
    }
}

