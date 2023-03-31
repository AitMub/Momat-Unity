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
        
        [ReadOnly]
        public NativeArray<JointIndexToQ> jointIndexToQs;

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
                if (jointIndexToQs[jointIndex].jointIndex != -1)
                {
                    Quaternion q = jointSamplers[jointIndex][frameIndex].q *
                                   Quaternion.Inverse(jointIndexToQs[jointIndex].refAvatarQ) * jointIndexToQs[jointIndex].avatarQ;
                    var affineT = new AffineTransform(jointSamplers[jointIndex][frameIndex].t, q);
                    localPose[jointIndex] = affineT;
                }
                else
                {
                    localPose[jointIndex] = jointSamplers[jointIndex][frameIndex];
                }
            }

            poseSamplePostProcess.Apply(localPose);
        }

        public void Dispose()
        {
            poseSamplePostProcess.Dispose();
        }
    }
}
