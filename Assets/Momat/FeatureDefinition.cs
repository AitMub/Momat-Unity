using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Momat
{
    [Serializable]
    public class TrajectoryFeatureDefinition
    {
        public List<float> trajectoryTimeStamps;
    }
        
    [Serializable]
    public class PoseFeatureDefinition
    {
        public int comparedPastFuturePoseNum;
        public bool comparePosition = true;
        public bool compareVelocity = false;
        public List<string> comparedJoint;
    }
}

