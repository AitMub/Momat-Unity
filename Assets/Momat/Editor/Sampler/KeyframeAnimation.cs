using System;

using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Momat.Editor
{
    internal struct KeyframeAnimation : IDisposable
    {
        public struct CurveInfo
        {
            public Curve curve;
            public int jointIndex;
            public int curveIndex;

            public int CompareTo(CurveInfo otherCurve)
            {
                int result = jointIndex.CompareTo(otherCurve.jointIndex);
                if (result == 0)
                {
                    result = curveIndex.CompareTo(otherCurve.curveIndex);
                }

                return result;
            }
        }
        
        private List<CurveInfo> animationCurveInfos;
        private NativeArray<TransformSampler> jointTransformSamplers;
        private float duration;
        private int numFrames;

        public Curve[] AnimationCurves => animationCurveInfos.Select(c => c.curve).ToArray();
        public List<CurveInfo> AnimationCurveInfos => animationCurveInfos;
        public NativeArray<TransformSampler> JointTransformSamplers => jointTransformSamplers;
        public int NumFrames => numFrames;

        public static KeyframeAnimation Create(AnimationClip animClip, AnimationRig animatedRig)
        {
            KeyframeAnimation anim = new KeyframeAnimation();

            anim.InitWithRigTransforms(animatedRig);
            anim.duration = animClip.length;
            anim.numFrames = 0;

            var curveBindings = AnimationUtility.GetCurveBindings(animClip);

            foreach (var curveBinding in curveBindings)
            {
                int jointIndex = animatedRig.GetJointIndexFromPath(curveBinding.path);

                if (jointIndex >= 0)
                {
                    var curve = AnimationUtility.GetEditorCurve(animClip, curveBinding);
                    
                    if (jointIndex == 0 && animClip.hasMotionCurves)
                    {
                        if (curveBinding.propertyName.Contains("Motion"))
                        {
                            anim.MapEditorCurve(jointIndex, curveBinding.propertyName, "MotionT", "MotionQ", curve);
                        }
                    }
                    // 暂时忽略root curve
                    /*if (jointIndex == 0 && animClip.hasRootCurves)
                    {
                        if (curveBinding.propertyName.Contains("Root"))
                        {
                            anim.MapEditorCurve(jointIndex, curveBinding.propertyName, "RootT", "RootQ", curve);
                        }
                    }*/
                    else
                    {
                        anim.MapEditorCurve(jointIndex, curveBinding.propertyName, "m_LocalPosition", "m_LocalRotation", curve);
                    }
                }
            }

            anim.animationCurveInfos.Sort((x, y) => x.CompareTo(y));

            return anim;
        }

        public KeyframeAnimation RetargetAnimation
            (AnimationRig sourceRig, AnimationRig targetRig, AvatarRetargetMap avatarRetargetMap)
        {
            var targetAnim = KeyframeAnimationRetargeter.CreateRetargetAnimation
                (sourceRig, targetRig, this, avatarRetargetMap);

            targetAnim.duration = duration;
            targetAnim.numFrames = numFrames;
            
            targetAnim.animationCurveInfos.Sort((x, y) => x.CompareTo(y));
            int jointIndex = 0, allCurveIndex = 0;
            while (jointIndex < targetRig.NumJoints)
            {
                while (allCurveIndex < targetAnim.animationCurveInfos.Count &&
                       targetAnim.animationCurveInfos[allCurveIndex].jointIndex == jointIndex)
                {
                    var sampler = targetAnim.jointTransformSamplers[jointIndex];
                    sampler.SetCurve(targetAnim.animationCurveInfos[allCurveIndex].curveIndex,
                        targetAnim.animationCurveInfos[allCurveIndex].curve);
                    targetAnim.jointTransformSamplers[jointIndex] = sampler;
                    
                    allCurveIndex++;
                }

                jointIndex++;
            }

            return targetAnim;
        }
        
        public AffineTransform SampleLocalJoint(int jointIndex, float sampleTimeInSeconds)
        {
            return jointTransformSamplers[jointIndex].Evaluate(sampleTimeInSeconds);
        }

        public KeyframeAnimation AllocateCopyAtFixedSampleRate(float sampleRate)
        {
            int numJoints = jointTransformSamplers.Length;

            KeyframeAnimation anim = new KeyframeAnimation();
            anim.animationCurveInfos = new List<CurveInfo>(animationCurveInfos.Count);
            anim.jointTransformSamplers = new NativeArray<TransformSampler>(numJoints, Allocator.Persistent);
            anim.numFrames = (int)math.ceil(sampleRate * duration);

            for (int jointIndex = 0; jointIndex < numJoints; ++jointIndex)
            {
                TransformSampler sourceSampler = jointTransformSamplers[jointIndex];
                TransformSampler destinationSampler = TransformSampler.CreateEmpty(sourceSampler.DefaultTransform);

                for (int curveIndex = 0; curveIndex < TransformSampler.NumCurves; ++curveIndex)
                {
                    if (sourceSampler.GetCurveProxy(curveIndex).HasCurve)
                    {
                        Curve curve = new Curve(anim.numFrames, Allocator.Persistent); // fixed framerate curve
                        anim.animationCurveInfos.Add(new CurveInfo()
                        {
                            curve = curve,
                            jointIndex = jointIndex,
                            curveIndex = curveIndex,
                        });
                        destinationSampler.SetCurve(curveIndex, curve);
                    }
                }

                anim.jointTransformSamplers[jointIndex] = destinationSampler;
            }

            return anim;
        }

        public void Dispose()
        {
            foreach (CurveInfo curveInfo in animationCurveInfos)
            {
                curveInfo.curve.Dispose();
            }

            jointTransformSamplers.Dispose();
        }

        public void InitWithRigTransforms(AnimationRig targetRig)
        {
            animationCurveInfos = new List<CurveInfo>();
            jointTransformSamplers = new NativeArray<TransformSampler>(targetRig.NumJoints, Allocator.Persistent);

            for (int i = 0; i < targetRig.NumJoints; ++i)
            {
                jointTransformSamplers[i] = TransformSampler.CreateEmpty(targetRig.Joints[i].localTransform);
            }
        }

        public int FindCurveRangeEndForJoint(int rangeBegin)
        {
            int rangeEnd = rangeBegin + 1;
            while (rangeEnd < animationCurveInfos.Count)
            {
                if (animationCurveInfos[rangeEnd].jointIndex != 
                    animationCurveInfos[rangeEnd - 1].jointIndex)
                {
                    break;
                }

                rangeEnd++;
            }

            return rangeEnd;
        }

        public (int,int) GetPositionCurveRangeIn(int rangeBegin, int rangeEnd)
        {
            int rangeEndForPosition = -1;
            for (int i = rangeBegin; i < rangeEnd; i++)
            {
                if (animationCurveInfos[i].curveIndex >= 0 &&
                    animationCurveInfos[i].curveIndex <= 2)
                {
                    rangeEndForPosition = i;
                }
            }

            if (rangeEndForPosition != -1)
            {
                return (rangeBegin, rangeEndForPosition + 1);
            }
            else
            {                
                return (-1, -1);
            }
        }
        
        public (int,int) GetRotationCurveRangeIn(int rangeBegin, int rangeEnd)
        {
            var range = GetPositionCurveRangeIn(rangeBegin, rangeEnd);
            if (range.Item2 != -1)
            {
                return (range.Item2, rangeEnd);
            }
            else
            {
                return (-1, -1);
            }
        }

        private void MapEditorCurve
            (int jointIndex, string curveName, 
                string posCurvePrefix, string rotCurvePrefix, AnimationCurve editorCurve)
        {
            int curveIndex;
            TransformSampler sampler = jointTransformSamplers[jointIndex];
            Curve? curve = sampler.MapEditorCurve
                (curveName, posCurvePrefix, rotCurvePrefix, editorCurve, out curveIndex);
            jointTransformSamplers[jointIndex] = sampler;

            if (curve.HasValue)
            {
                animationCurveInfos.Add(new CurveInfo()
                {
                    curve = curve.Value,
                    jointIndex = jointIndex,
                    curveIndex = curveIndex
                });
            }
        }
    }
}
