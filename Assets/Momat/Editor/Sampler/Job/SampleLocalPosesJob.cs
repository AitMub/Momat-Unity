using System;

using Unity.Collections;
using static Momat.Editor.AnimationCurveBake;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Plastic.Antlr3.Runtime.Debug;
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

            localPose[0] = AffineTransform.identity;

            for (int jointIndex = 0; jointIndex < numJoints; ++jointIndex)
            {
                if (jointIndexToQs[jointIndex].refJointIndex != -1)
                {
                    localPose[jointIndex] = jointSamplers[jointIndex][frameIndex];
                    
                    int refIndex = jointIndexToQs[jointIndex].refJointIndex;

                    int refParentIndex = poseSamplePostProcess.refParentIndices[refIndex];
                    if (refParentIndex >= 0)
                    {
                        localPose[jointIndex] = poseSamplePostProcess.refRigBindMatrics[refParentIndex] * localPose[jointIndex];
                    }
                    
                    var localMat = localPose[jointIndex] * poseSamplePostProcess.refRigInverseBindMatrices[refIndex];
                    localMat = poseSamplePostProcess.targetRigInverseParentBindMatrices[jointIndex]  * localMat *
                               poseSamplePostProcess.targetRigBindMatrices[jointIndex];

                    localPose[jointIndex] = localMat;
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
