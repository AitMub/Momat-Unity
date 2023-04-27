using System;
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
        
        public void Print(){Debug.Log($"{animationID} {frameID}");}
    }

    public struct FeatureVector
    {
        public PoseIdentifier poseIdentifier;
        public List<float3> trajectory;
        public List<AffineTransform> jointRootSpaceT;
    }
    
    public class RuntimeAnimationData : ScriptableObject
    {
        public float frameRate = 30;

        [HideInInspector] public AnimationRig rig;

        public TrajectoryFeatureDefinition trajectoryFeatureDefinition;
        public PoseFeatureDefinition poseFeatureDefinition;
        
        public List<int> animationFrameNum;
        public List<int> animationFrameOffset;
        public int AnimationCnt => animationFrameNum.Count;

        public int TransformGroupLen => rig.NumJoints;
        [HideInInspector]  public List<AffineTransform> transforms;

        public int TrajectoryPointGroupLen => trajectoryFeatureDefinition.trajectoryTimeStamps.Count;
        [HideInInspector] public List<float3> trajectoryPoints;
        
        public int ComparedJointTransformGroupLen => poseFeatureDefinition.comparedJoint.Count;
        [HideInInspector] public List<AffineTransform> comparedJointRootSpaceT;
        
        public RuntimeAnimationData()
        {
            animationFrameNum = new List<int>();
            animationFrameOffset = new List<int>();
            
            transforms = new List<AffineTransform>();
            trajectoryPoints = new List<float3>();
            comparedJointRootSpaceT = new List<AffineTransform>();
        }

        public AffineTransform GetPoseTransform(PoseIdentifier poseIdentifier, int jointIndex)
        {
            int clampedFrameID = Math.Clamp(poseIdentifier.frameID, 0, animationFrameNum[poseIdentifier.animationID] - 1);
            int index = TransformGroupLen * animationFrameOffset[poseIdentifier.animationID] +
                        rig.NumJoints * clampedFrameID + jointIndex;
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
            for (int animationIndex = 0; animationIndex < AnimationCnt; animationIndex++)
            {
                for (int frameIndex = 0; frameIndex + Mathf.FloorToInt(playTime * frameRate) < animationFrameNum[animationIndex]; frameIndex++)
                {
                    var featureVector = new FeatureVector();
                    featureVector.poseIdentifier = new PoseIdentifier
                        { animationID = animationIndex, frameID = frameIndex };

                    int rangeBegin = TrajectoryPointGroupLen * animationFrameOffset[animationIndex]
                                     + frameIndex * TrajectoryPointGroupLen;
                    featureVector.trajectory = trajectoryPoints.GetRange(rangeBegin, TrajectoryPointGroupLen);

                    rangeBegin = ComparedJointTransformGroupLen * animationFrameOffset[animationIndex]
                                 + frameIndex * ComparedJointTransformGroupLen;
                    featureVector.jointRootSpaceT = comparedJointRootSpaceT.GetRange(rangeBegin, ComparedJointTransformGroupLen);
                    
                    yield return featureVector;
                }
            }
        }

        public FeatureVector GetFeatureVector(PoseIdentifier poseIdentifier)
        {
            var featureVector = new FeatureVector();
            featureVector.poseIdentifier = poseIdentifier;

            int rangeBegin = TrajectoryPointGroupLen * animationFrameOffset[poseIdentifier.animationID]
                             + poseIdentifier.frameID * TrajectoryPointGroupLen;
            featureVector.trajectory = trajectoryPoints.GetRange(rangeBegin, TrajectoryPointGroupLen);

            return featureVector;
        }
    }
}