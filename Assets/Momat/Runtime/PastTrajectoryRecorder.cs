using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Momat.Runtime
{
    public class PastTrajectoryRecorder
    {
        private RuntimeTrajectory worldTrajectory;
        private RuntimeTrajectory pastLocalTrajectory;
        private readonly float earliestTimeStamp;

        public RuntimeTrajectory PastLocalTrajectory => pastLocalTrajectory;
        
        public PastTrajectoryRecorder(TrajectoryFeatureDefinition trajectoryFeatureDefinition, Transform oriTransform)
        {
            var pastRecordTimeStamp = new List<float>();
            for (int i = 0; i < trajectoryFeatureDefinition.trajectoryTimeStamps.Count; i++)
            {
                if (trajectoryFeatureDefinition.trajectoryTimeStamps[i] < 0)
                {
                    pastRecordTimeStamp.Add(trajectoryFeatureDefinition.trajectoryTimeStamps[i]);
                }
            }
            earliestTimeStamp = pastRecordTimeStamp[^1];

            worldTrajectory = new RuntimeTrajectory();
            pastLocalTrajectory = new RuntimeTrajectory();

            for (int i = 0; i < pastRecordTimeStamp.Count; i++)
            {
                pastLocalTrajectory.trajectoryData.AddLast(new TrajectoryPoint(oriTransform, pastRecordTimeStamp[i]));
            }
        }

        public void Record(Transform transform, float deltaTime)
        {
            var iter = worldTrajectory.trajectoryData.First;
            var currTransform = new AffineTransform(transform.position, transform.rotation);
            
            while (iter != null)
            {
                if (iter.Value.timeStamp < earliestTimeStamp)
                {
                    worldTrajectory.trajectoryData.Remove(iter);
                }

                var iterForPostTraj = pastLocalTrajectory.trajectoryData.First;
                while (iterForPostTraj != null)
                {
                    if (iter.Value.timeStamp > iterForPostTraj.Value.timeStamp &&
                        iter.Value.timeStamp - deltaTime < iterForPostTraj.Value.timeStamp)
                    {
                        iterForPostTraj.Value.transform =  currTransform.inverse() * iter.Value.transform;
                    }

                    iterForPostTraj = iterForPostTraj.Next;
                }

                iter.Value.timeStamp -= deltaTime;

                iter = iter.Next;
            }

            worldTrajectory.trajectoryData.AddFirst(new TrajectoryPoint(transform));
        }
    }
}

