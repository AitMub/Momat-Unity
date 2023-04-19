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

        public readonly float frameRate = 30;

        public AffineTransform GetPoseTransform(PoseIdentifier poseIdentifier, int jointIndex)
        {
            int index = animationTransformOffset[poseIdentifier.animationID] +
                        rig.NumJoints * poseIdentifier.frameID +
                        jointIndex;
            return transforms[index];
        }

        public AffineTransform GetAnimationTransformAtTime(int animationID, float time, int jointIndex)
        {
            float floatFrame = frameRate * time;
            int frameID1 = Mathf.FloorToInt(floatFrame);
            int frameID2 = Mathf.CeilToInt(floatFrame);
            float weight = floatFrame - frameID1;

            var transform1 = GetPoseTransform
                (new PoseIdentifier { animationID = animationID, frameID = frameID1 }, jointIndex);
            var transform2 = GetPoseTransform
                (new PoseIdentifier { animationID = animationID, frameID = frameID2 }, jointIndex);

            return AffineTransform.Interpolate(transform1, transform2, weight);
        }
    }
}