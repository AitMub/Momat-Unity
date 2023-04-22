using System.Collections;
using System.Collections.Generic;
using PlasticGui;
using Unity.Mathematics;
using UnityEngine;

namespace Momat.Editor
{
    internal struct TrajectoryPoint
    {
        internal Vector3 position;
        internal float timeStamp;
            
        public TrajectoryPoint(Transform transform, float timeStamp = 0f)
        {
            this.position = transform.position;
            this.timeStamp = timeStamp;
        }
            
        public TrajectoryPoint(AffineTransform transform, float timeStamp = 0f)
        {
            this.position = transform.t;
            this.timeStamp = timeStamp;
        }
    
        public TrajectoryPoint(float3 position, float timeStamp = 0f)
        {
            this.position = position;
            this.timeStamp = timeStamp;
        }
    }
}

