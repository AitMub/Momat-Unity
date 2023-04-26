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
        public NativeArray<AffineTransform> jointRootSpaceT;

        public void Dispose()
        {
            trajectories.Dispose();
            jointRootSpaceT.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal struct GenerateFeatureVectorJob : IJobParallelFor, IDisposable
    {
        [ReadOnly] public NativeArray<AffineTransform> localPoses;
        [ReadOnly] public int numJoints;
        [ReadOnly] public float frameRate;
        [ReadOnly] public NativeArray<float> trajectoryTimeStamps;
        [ReadOnly] public NativeArray<int> jointIndices;
        [ReadOnly] public NativeArray<int> parentIndices;

        public ClipFeatureVectors featureVectors;
        
        public void Execute(int frameIndex)
        {
            var currWorldTransform = localPoses[frameIndex * numJoints];
            var timeStampNum = trajectoryTimeStamps.Length;
            
            var frameTrajectoryPoints = new MemoryArray<float3>
                (featureVectors.trajectories, frameIndex * timeStampNum, timeStampNum);

            for (int i = 0; i < timeStampNum; i++)
            {
                var timeStamp = trajectoryTimeStamps[i];
                var worldTransform = SampleTransform(frameIndex, timeStamp);
                var localTransform = currWorldTransform.inverse() * worldTransform;
                frameTrajectoryPoints[i] = localTransform.t;
            }

            var jointNum = jointIndices.Length;
            var frameJointRootSpaceTransform = new MemoryArray<AffineTransform>
                (featureVectors.jointRootSpaceT, frameIndex * jointNum, jointNum);

            for (int i = 0; i < jointNum; i++)
            {
                var jointIndex = jointIndices[i];
                var rootSpaceTransform = GetRootSpaceTransform(frameIndex, jointIndex);
                frameJointRootSpaceTransform[i] = rootSpaceTransform;
            }
        }
        
        public void Dispose()
        {
            trajectoryTimeStamps.Dispose();
            jointIndices.Dispose();
            parentIndices.Dispose();
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

        private AffineTransform GetRootSpaceTransform(int frameIndex, int jointIndex)
        {
            var transform = AffineTransform.identity;
            while (jointIndex != 0)
            {
                transform = localPoses[frameIndex * numJoints + jointIndex] * transform;
                jointIndex = parentIndices[jointIndex];
            }
            
            return transform;
        }
    }
}
