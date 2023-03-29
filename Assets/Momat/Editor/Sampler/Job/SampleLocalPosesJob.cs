using System;

using Unity.Collections;
using static Momat.Editor.AnimationCurveBake;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Momat.Editor
{
    [BurstCompile(CompileSynchronously = true)]
    internal struct SampleLocalPosesJob : IJobParallelFor, IDisposable
    {
        [ReadOnly]
        public MemoryArray<TransformSampler> jointSamplers; // input proxy to local joint transforms from individual float curve
        public NativeArray<AffineTransform> localPoses; // output contiguous poses

        // settings
        [ReadOnly]
        public SampleRange sampleRange;

        [ReadOnly]
        public PoseSamplePostProcess poseSamplePostProcess;

        public void Execute(int index)
        {
            int frameIndex = sampleRange.startFrameIndex + index;

            int numJoints = jointSamplers.Length;

            MemoryArray<AffineTransform> localPose = new MemoryArray<AffineTransform>(localPoses, numJoints * index, numJoints);

            for (int jointIndex = 0; jointIndex < numJoints; ++jointIndex)
            {
                localPose[jointIndex] = jointSamplers[jointIndex][frameIndex];
            }

            poseSamplePostProcess.Apply(localPose);
        }

        public void Dispose()
        {
            poseSamplePostProcess.Dispose();
        }
    }
}
