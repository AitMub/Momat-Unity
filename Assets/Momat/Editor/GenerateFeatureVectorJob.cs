using System;
using System.Collections;
using System.Collections.Generic;
using Momat.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;

namespace Momat.Editor
{
    internal struct ClipFeatureVectors : IDisposable
    {
        public NativeArray<float3> trajectories;

        public void Dispose()
        {
            trajectories.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal struct GenerateFeatureVectorJob : IJobParallelFor, IDisposable
    {
        [ReadOnly] public NativeArray<AffineTransform> localPoses;
        [ReadOnly] public int numJoints;
        [ReadOnly] public float frameRate;
        [ReadOnly] public NativeArray<float> trajectoryTimeStamps;

        public ClipFeatureVectors featureVectors;
        
        public void Execute(int frameIndex)
        {
            var currWorldTransform = localPoses[frameIndex * numJoints];
            var timeStampNum = trajectoryTimeStamps.Length;
            
            MemoryArray<float3> frameTrajectoryPoints = new MemoryArray<float3>
                (featureVectors.trajectories, frameIndex * timeStampNum, timeStampNum);

            for (int i = 0; i < timeStampNum; i++)
            {
                var timeStamp = trajectoryTimeStamps[i];
                var worldTransform = SampleTransform(frameIndex, timeStamp);
                var localTransform = currWorldTransform.inverse() * worldTransform;
                frameTrajectoryPoints[i] = localTransform.t;
            }
        }
        
        public void Dispose()
        {
            trajectoryTimeStamps.Dispose();
        }

        private AffineTransform SampleTransform(int frameIndex, float relativeTime)
        {
            var targetFrameIndex = frameRate * relativeTime + frameIndex;
            if (targetFrameIndex < 0)
            {
                targetFrameIndex = 0;
            }
            else if (targetFrameIndex >= localPoses.Length / numJoints)
            {
                targetFrameIndex = localPoses.Length / numJoints - 1;
            }
            return localPoses[Mathf.CeilToInt(targetFrameIndex * numJoints)];
        }
    }
}
