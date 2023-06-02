using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Quaternion = System.Numerics.Quaternion;
using Random = System.Random;


namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        private SearchPoseJob searchPoseJob;
        private JobHandle jobHandle;
        
        // feature vectors: NativeArray can not contain array, so store them separately
        private NativeArray<PoseIdentifier> poseIDs;
        private NativeArray<AffineTransform> trajectoryPoints;
        private NativeArray<AffineTransform> jointRootSpaceT;

        private NativeArray<AffineTransform> currTrajectory;
        private NativeArray<AffineTransform> currJointRootSpaceT;

        private NativeArray<float> minCostForEachJob;
        private NativeArray<PoseIdentifier> minCostPose;
        
        [SerializeField]
        private bool searchPoseAsync;
        [SerializeField]
        private int jobLoopCount = 128;
        
        private void PrepareSearchPoseJob()
        {
            var featureVectorArray = GetAllMotionAnimFeatureVectors().ToArray();
            
            poseIDs = new NativeArray<PoseIdentifier>(featureVectorArray.Length, Allocator.Persistent);

            int trajectoryPointStride = runtimeAnimationData.TrajectoryPointGroupLen;
            trajectoryPoints = new NativeArray<AffineTransform>(
                featureVectorArray.Length * trajectoryPointStride, Allocator.Persistent);
            int jointTStride = runtimeAnimationData.ComparedJointTransformGroupLen;
            jointRootSpaceT = new NativeArray<AffineTransform>(
                featureVectorArray.Length * jointTStride, Allocator.Persistent);
            
            for (int i = 0; i < featureVectorArray.Length; i++)
            {
                poseIDs[i] = featureVectorArray[i].poseIdentifier;

                for (int j = i * trajectoryPointStride; j < (i + 1) * trajectoryPointStride; j++)
                {
                    trajectoryPoints[j] = featureVectorArray[i].trajectory[j - i * trajectoryPointStride];
                }
                
                for (int j = i * jointTStride; j < (i + 1) * jointTStride; j++)
                {
                    jointRootSpaceT[j] = featureVectorArray[i].jointRootSpaceT[j - i * jointTStride];
                }
            }

            currTrajectory = new NativeArray<AffineTransform>(
                runtimeAnimationData.TrajectoryPointGroupLen, Allocator.Persistent);
            currJointRootSpaceT = new NativeArray<AffineTransform>(
                runtimeAnimationData.ComparedJointTransformGroupLen, Allocator.Persistent);

            var jobCount = Mathf.CeilToInt(featureVectorArray.Length / (float)jobLoopCount);
            minCostForEachJob = new NativeArray<float>(jobCount, Allocator.Persistent);
            minCostPose = new NativeArray<PoseIdentifier>(jobCount, Allocator.Persistent);
        }
        
        private void BeginSearchPoseJob()
        {
            var currFeatureVector = GetCurrFeatureVector();
            for (int i = 0; i < currFeatureVector.trajectory.Count; i++)
            {
                currTrajectory[i] = currFeatureVector.trajectory[i];
            }
            for (int i = 0; i < currFeatureVector.jointRootSpaceT.Count; i++)
            {
                currJointRootSpaceT[i] = currFeatureVector.jointRootSpaceT[i];
            }
            
            searchPoseJob = new SearchPoseJob
            {
                jobLoopCount = jobLoopCount,
                
                poseIDs = poseIDs,
                trajectoryPoints = trajectoryPoints,
                trajectoryPointStride = runtimeAnimationData.TrajectoryPointGroupLen,
                jointRootSpaceT = jointRootSpaceT,
                jointTStride = runtimeAnimationData.ComparedJointTransformGroupLen,
                
                currTrajectory = currTrajectory,
                currJointRootSpaceT = currJointRootSpaceT,
                
                minCostForEachJob = minCostForEachJob,
                minCostPose = minCostPose
            };
            jobHandle =  searchPoseJob.ScheduleBatch(poseIDs.Length, jobLoopCount);
        }

        private bool IsSearchJobComplete()
        {
            return jobHandle.IsCompleted;
        }

        private PoseIdentifier GetSearchJobResult()
        {
            jobHandle.Complete();
            
            var min = float.MaxValue;
            int index = -1;
            for (int i = 0; i < minCostForEachJob.Length; i++)
            {
                if (minCostForEachJob[i] < min)
                {
                    min = minCostForEachJob[i];
                    index = i;
                }
            }
            return minCostPose[index];
        }
        
        private PoseIdentifier SearchPose(IEnumerable<FeatureVector> featureVectors)
        {
            var poseIdentifier = new PoseIdentifier();
            float minCost = float.MaxValue;

            var currFeatureVector = GetCurrFeatureVector();
            foreach (var featureVector in featureVectors)
            {
                var cost = costComputeFunc(currFeatureVector, featureVector);
                if (cost < minCost)
                {
                    poseIdentifier = featureVector.poseIdentifier;
                    minCost = cost;
                }
            }
            
            return poseIdentifier;
        }

        private IEnumerable<FeatureVector> GetAllMotionAnimFeatureVectors()
        {
            return runtimeAnimationData.GetPlayablePoseFeatureVectors(updateInterval + blendTime, EAnimationType.EMotion);
        }
        
        private IEnumerable<FeatureVector> GetAllIdleAnimFeatureVectors()
        {
            return runtimeAnimationData.GetPlayablePoseFeatureVectors(blendTime, EAnimationType.EIdle);
        }

        private IEnumerable<FeatureVector> GetEventBeginPhasePoseFeatureVectors(int eventID)
        {
            for (int i = 0; i < runtimeAnimationData.eventClipDatas.Length; i++)
            {
                if (runtimeAnimationData.eventClipDatas[i].eventID == eventID)
                {
                    var animationIndex = i + runtimeAnimationData.animationTypeOffset[(int)EAnimationType.EEvent];
                    foreach (var featureVector in runtimeAnimationData.GetPlayablePoseFeatureVectors(animationIndex, 
                                     runtimeAnimationData.eventClipDatas[i].prepareFrame, 
                                     runtimeAnimationData.eventClipDatas[i].beginFrame))
                    {
                        yield return featureVector;
                    }
                }
            }
        }
    }
}