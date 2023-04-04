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

            localPose[0] = AffineTransform.identity;
            
            /*// 计算源avatar的各个joint的世界坐标
            int refNumJoints = poseSamplePostProcess.refParentIndices.Length;
            AffineTransform[] refWorldPose = new AffineTransform[refNumJoints];
            refWorldPose[0] = AffineTransform.identity;
            for (int refJointIndex = 0; refJointIndex < refNumJoints; refJointIndex++)
            {
                int refParentIndex = poseSamplePostProcess.refParentIndices[refJointIndex];
                if(refParentIndex > 0)
                
            }*/
            
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
                    

                    //Debug.Log($"jointIndex: {jointIndex} refIndex: {refIndex} refParentIndex: {refParentIndex}\n" +
                       //       $"Parent T: {poseSamplePostProcess.refRigBindMatrics[refParentIndex]} World T: {localMat.t}");
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
