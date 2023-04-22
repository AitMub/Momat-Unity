using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Vector3 = System.Numerics.Vector3;

namespace Momat.Runtime
{
    public class TrajectoryPoint
    {
        public AffineTransform transform;
        public float timeStamp;
        
        public TrajectoryPoint(Transform transform, float timeStamp = 0f)
        {
            this.transform = new AffineTransform(transform.position, transform.rotation);
            this.timeStamp = timeStamp;
        }
        
        public TrajectoryPoint(AffineTransform transform, float timeStamp = 0f)
        {
            this.transform = transform;
            this.timeStamp = timeStamp;
        }

        public TrajectoryPoint(float3 position, float timeStamp = 0f)
        {
            this.transform = new AffineTransform(position, quaternion.identity);
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

