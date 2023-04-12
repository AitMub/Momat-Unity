using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;

namespace Momat.Runtime
{
    public struct PoseIdentifier
    {
        public int animationID;
        public int frameID;
    }
    
    public class RuntimeAnimationData : ScriptableObject
    {
        public List<AffineTransform> transforms;
        public List<int> animationTransformOffset;

        public AnimationRig rig;

        public AffineTransform GetTransform(PoseIdentifier poseIdentifier, int jointIndex)
        {
            int index = animationTransformOffset[poseIdentifier.animationID] +
                        rig.NumJoints * poseIdentifier.frameID +
                        jointIndex;
            return transforms[index];
        }
    }
}