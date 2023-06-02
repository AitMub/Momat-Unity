using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Animations;
using Unity.Mathematics;
using Unity.VisualScripting;


namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        [BurstCompile(CompileSynchronously = true)]
        internal struct SearchPoseJob : IJobParallelForBatch
        {
            [ReadOnly] public int jobLoopCount;
            // all feature vectors that will be searched
            [ReadOnly] public NativeArray<PoseIdentifier> poseIDs;
            [ReadOnly] public NativeArray<AffineTransform> trajectoryPoints;
            [ReadOnly] public int trajectoryPointStride;
            [ReadOnly] public NativeArray<AffineTransform> jointRootSpaceT;
            [ReadOnly] public int jointTStride;
            
            // curr feature vector
            [ReadOnly] public NativeArray<AffineTransform> currTrajectory;
            [ReadOnly] public NativeArray<AffineTransform> currJointRootSpaceT;

            // output
            public NativeArray<float> minCostForEachJob;
            public NativeArray<PoseIdentifier> minCostPose;

            public void Execute(int startIndex, int count)
            {
                var minCost = float.MaxValue;
                var poseIndex = -1;

                for (int i = startIndex; i < startIndex + count; i++)
                {
                    var cost = ComputeCostNative(i);
                    if (cost < minCost)
                    {
                        minCost = cost;
                        poseIndex = i;
                    }
                }

                var minCostPtr = new MemoryArray<float>(minCostForEachJob, startIndex / jobLoopCount, 1);
                minCostPtr[0] = minCost;

                var minPosePtr = new MemoryArray<PoseIdentifier>(minCostPose, startIndex / jobLoopCount, 1);
                minPosePtr[0] = poseIDs[poseIndex];
            }

            public float ComputeCostNative(int index)
            {
                float futureTrajCost = 0;
                float pastTrajCost = 0;

                for (int i = 0; i < currTrajectory.Length / 2; i++)
                {
                    futureTrajCost += Vector3.Distance(
                            trajectoryPoints[i + index * trajectoryPointStride].t, currTrajectory[i].t);
                    var angleCost = UnityEngine.Quaternion.Angle(
                        trajectoryPoints[i + index * trajectoryPointStride].q, currTrajectory[i].q)  / 180f;
                    futureTrajCost += angleCost;
                }
            
                for (int i = currTrajectory.Length / 2; i < currTrajectory.Length; i++)
                {
                    pastTrajCost += Vector3.Distance(
                        trajectoryPoints[i + index * trajectoryPointStride].t, currTrajectory[i].t);
                }
            

                float poseCost = 0;
                for (int i = 0; i < currJointRootSpaceT.Length; i++)
                {
                    poseCost += Vector3.Distance(
                        jointRootSpaceT[i + index * jointTStride].t, currJointRootSpaceT[i].t);
                }

                var weight = 0.45f;
                return (futureTrajCost * 0.8f + pastTrajCost * 0.2f) * weight + poseCost * (1 - weight);
            }
        }
    }
}