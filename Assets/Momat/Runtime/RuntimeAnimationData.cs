using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace Momat.Runtime
{
    public struct PoseIdentifier
    {
        public int animationID;
        public int frameID;
    }

    public struct FeatureVector
    {
        public PoseIdentifier poseIdentifier;
        public List<float3> trajectory;
        public List<AffineTransform> jointRootSpaceT;
    }
    
    public class RuntimeAnimationData : ScriptableObject
    {
        public TrajectoryFeatureDefinition trajectoryFeatureDefinition;
        public PoseFeatureDefinition poseFeatureDefinition;
        
        [HideInInspector]  public List<AffineTransform> transforms;
        public List<int> animationTransformOffset;

        [HideInInspector] public List<float3> trajectoryPoints;
        public List<int> trajectoryPointOffset;
        public int PoseTrajectoryPointNum => trajectoryFeatureDefinition.trajectoryTimeStamps.Count;

        public List<AffineTransform> comparedJointRootSpaceT;
        public int ComparedJointTransformNum => poseFeatureDefinition.comparedJoint.Count;
        
        [HideInInspector] public AnimationRig rig;

        public readonly float frameRate = 30;

        public int AnimationNum => animationTransformOffset.Count;
        public List<int> animationFrameNum;

        public RuntimeAnimationData()
        {
            transforms = new List<AffineTransform>();
            animationTransformOffset = new List<int>();
            trajectoryPoints = new List<float3>();
            trajectoryPointOffset = new List<int>();
            comparedJointRootSpaceT = new List<AffineTransform>();
            animationFrameNum = new List<int>();
        }

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

        public IEnumerable<FeatureVector> GetPlayablePoseFeatureVectors(float playTime)
        {
            for (int animationIndex = 0; animationIndex < AnimationNum; animationIndex++)
            {
                for (int frameIndex = 0; frameIndex + Mathf.FloorToInt(playTime * frameRate) < animationFrameNum[animationIndex]; frameIndex++)
                {
                    var featureVector = new FeatureVector();
                    featureVector.poseIdentifier = new PoseIdentifier
                        { animationID = animationIndex, frameID = frameIndex };

                    int rangeBegin = trajectoryPointOffset[animationIndex] + frameIndex * PoseTrajectoryPointNum;
                    featureVector.trajectory = trajectoryPoints.GetRange(rangeBegin, PoseTrajectoryPointNum);

                    rangeBegin = animationIndex * ComparedJointTransformNum * animationFrameNum[animationIndex]
                                 + frameIndex * ComparedJointTransformNum;
                    featureVector.jointRootSpaceT = comparedJointRootSpaceT.GetRange(rangeBegin, ComparedJointTransformNum);
                    
                    yield return featureVector;
                }
            }
        }

        public FeatureVector GetFeatureVector(PoseIdentifier poseIdentifier)
        {
            var featureVector = new FeatureVector();
            featureVector.poseIdentifier = poseIdentifier;

            int rangeBegin = trajectoryPointOffset[poseIdentifier.animationID] + poseIdentifier.frameID * PoseTrajectoryPointNum;
            featureVector.trajectory = trajectoryPoints.GetRange(rangeBegin, PoseTrajectoryPointNum);

            return featureVector;
        }
    }
}