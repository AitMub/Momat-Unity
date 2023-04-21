using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Momat.Runtime
{
    public struct TrajectoryPoint
    {
        public AffineTransform transform;
        public float timeStamp;

        public TrajectoryPoint(Transform transform, float timeStamp = 0f)
        {
            this.transform = new AffineTransform(transform.position, transform.rotation);
            this.timeStamp = timeStamp;
        }
    }
    
    public class RuntimeTrajectory
    {
        public LinkedList<TrajectoryPoint> trajectoryData;

        public RuntimeTrajectory()
        {
            trajectoryData = new LinkedList<TrajectoryPoint>();
        }
    }
}

