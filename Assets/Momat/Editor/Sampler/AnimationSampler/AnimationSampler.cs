using System;

using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using static Momat.Editor.AnimationCurveBake.ThreadSafe;
using static Momat.Editor.AnimationCurveBake;

namespace Momat.Editor
{
    internal partial class AnimationSampler : IDisposable
    {
        private AnimationRig rig;
        private AnimationClip animationClip;

        private KeyframeAnimation editorAnimation;
        private KeyframeAnimation? bakedAnimation;

        private PoseSamplePostProcess poseSamplePostProcess;

        internal AnimationSampler(AnimationClip animationClip, AnimationRig rig)
        {
            this.animationClip = animationClip;
            this.rig = rig;
            
            editorAnimation = KeyframeAnimation.Create(animationClip, rig);
            bakedAnimation = null;
            
            try
            {
                poseSamplePostProcess = new PoseSamplePostProcess
                    (rig, animationClip, editorAnimation.JointTransformSamplers[0][0]);
            }
            catch (Exception e)
            {
                editorAnimation.Dispose();
                throw e;
            }
        }

        internal void RetargetAnimation(AnimationRig targetRig, AvatarRetargetMap avatarRetargetMap)
        {
            if (bakedAnimation != null)
            {
                throw new Exception("Retarget is only allowed before Range Sampler is prepared");
            }

            var oriEditorAnimation = editorAnimation;
            editorAnimation = editorAnimation.RetargetAnimation(
                this.rig, targetRig, avatarRetargetMap);
            oriEditorAnimation.Dispose();
            
            try
            {
                poseSamplePostProcess.Dispose();
                poseSamplePostProcess = new PoseSamplePostProcess
                    (targetRig, animationClip, editorAnimation.JointTransformSamplers[0][0]);
            }
            catch (Exception e)
            {
                editorAnimation.Dispose();
                throw e;
            }
            
            this.rig = targetRig;
        }

        public void Dispose()
        {
            editorAnimation.Dispose();
            bakedAnimation?.Dispose();

            poseSamplePostProcess.Dispose();
        }
        
        // 暂时注释掉, 这个好像只在Kinematica的预览功能中使用了
        /*public TransformBuffer.Memory SamplePose(float sampleTimeInSeconds)
        {
            int numJoints = editorAnimation.JointSamplers.Length;

            TransformBuffer.Memory buffer = TransformBuffer.Memory.Allocate(numJoints, Allocator.Temp);
            ref TransformBuffer pose = ref buffer.Ref;

            for (int jointIndex = 0; jointIndex < numJoints; ++jointIndex)
            {
                pose[jointIndex] = editorAnimation.JointSamplers[jointIndex].Evaluate(sampleTimeInSeconds);
            }

            poseSamplePostProcess.Apply(pose.transforms);

            return buffer;
        }*/

        public AffineTransform SampleTrajectory(float sampleTimeInSeconds)
        {
            return editorAnimation.JointTransformSamplers[0].Evaluate(sampleTimeInSeconds);
        }

        internal void AllocateBakedAnimation(float sampleRate)
        {
            if (bakedAnimation == null)
            {
                bakedAnimation = editorAnimation.AllocateCopyAtFixedSampleRate(sampleRate);
            }
        }

        internal RangeSampler PrepareRangeSampler
            (float sampleRate, SampleRange sampleRange, int destinationStartFrameIndex, NativeArray<AffineTransform> outTransforms)
        {
            AllocateBakedAnimation(sampleRate);

            if (bakedAnimation.Value.NumFrames < sampleRange.startFrameIndex + sampleRange.numFrames)
            {
                throw new ArgumentException($"Trying to sample {sampleRange.startFrameIndex + sampleRange.numFrames - 1} frame from {animationClip.name} whose last frame is {bakedAnimation.Value.NumFrames - 1} (resampled at {sampleRate} fps)", "sampleRange");
            }

            int numJoints = editorAnimation.JointTransformSamplers.Length;

            NativeArray<AffineTransform> localPoses = new NativeArray<AffineTransform>(sampleRange.numFrames * numJoints, Allocator.Persistent);

            BakeJob bakeJob = ConfigureBake(editorAnimation.AnimationCurves, sampleRate, bakedAnimation.Value.AnimationCurves, Allocator.Persistent, sampleRange);

            SampleLocalPosesJob sampleLocalPosesJob = new SampleLocalPosesJob()
            {
                jointSamplers = new MemoryArray<TransformSampler>(bakedAnimation.Value.JointTransformSamplers),
                localPoses = outTransforms,
                sampleRange = sampleRange,
                poseSamplePostProcess = poseSamplePostProcess.Clone(),
            };

            ConvertToGlobalPosesJob convertToGlobalPoseJob = new ConvertToGlobalPosesJob()
            {
                localPoses = localPoses,
                globalTransforms = new MemoryArray<AffineTransform>(outTransforms),
                numJoints = numJoints,
                sampleRange = sampleRange,
                poseSamplePostProcess = poseSamplePostProcess.Clone(),
                destinationStartFrameIndex = destinationStartFrameIndex,
            };

            return new RangeSampler()
            {
                localPoses = localPoses,
                bakeJob = bakeJob,
                sampleLocalPosesJob = sampleLocalPosesJob,
                convertToGlobalPosesJob = convertToGlobalPoseJob
            };
        }
    }
}
