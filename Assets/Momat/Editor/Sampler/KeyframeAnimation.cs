using System;

using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Momat.Editor
{
    internal struct KeyframeAnimation : IDisposable
    {
        public struct CurveInfo
        {
            public Curve curve;
            public int jointIndex;
            public int curveIndex;

            public const int PositionCurveCnt = 3;
            public const int RotationCurveCnt = 4;

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
                        if (curveBinding.propertyName == "MotionT.y")
                        {
                            var zeroHeightCurve = new AnimationCurve();
                            zeroHeightCurve.AddKey(0, 0);
                            zeroHeightCurve.AddKey(anim.duration, 0);
                            anim.MapEditorCurve(jointIndex, curveBinding.propertyName, "MotionT", "MotionQ", zeroHeightCurve);
                        }
                        else if (curveBinding.propertyName is "MotionQ.x" or "MotionQ.z")
                        {
                            var motionYIndex = 
                                Array.FindIndex(curveBindings, cb => cb.propertyName == "MotionQ.y");
                            var motionYCurve = AnimationUtility.GetEditorCurve(animClip, curveBindings[motionYIndex]);

                            var zeroCurve = new AnimationCurve();
                            for (int i = 0; i < motionYCurve.length; i++)
                            {
                                zeroCurve.AddKey(motionYCurve[i].time, 0);
                            }
                            anim.MapEditorCurve(jointIndex, curveBinding.propertyName, "MotionT", "MotionQ", zeroCurve);
                        }
                        else if (curveBinding.propertyName.Contains("Motion"))
                        {
                            anim.MapEditorCurve(jointIndex, curveBinding.propertyName, "MotionT", "MotionQ", curve);
                        }
                    }
                    else
                    {
                        anim.MapEditorCurve(jointIndex, curveBinding.propertyName, "m_LocalPosition", "m_LocalRotation", curve);
                    }
                }
            }

            anim.animationCurveInfos.Sort((x, y) => x.CompareTo(y));
            anim.CounteractDupRootTransformInBody(animatedRig);
            
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

        private void CounteractDupRootTransformInBody(AnimationRig rig)
        {
            string assetPath = AssetDatabase.GetAssetPath(rig.Avatar);
            GameObject avatarRootObject = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;
            var bodyToWorldTransform = avatarRootObject.transform;
            var bodyToWorldMat = bodyToWorldTransform.localToWorldMatrix;

            var bodyCurveIndex = animationCurveInfos.FindIndex(c => c.jointIndex == rig.BodyJointIndex);
            
            var curves = 
                Enumerable.Range(1, CurveInfo.PositionCurveCnt + CurveInfo.RotationCurveCnt).Select(i => new AnimationCurve()).ToArray();

            var bodyWorldHeight = (bodyToWorldMat * rig.Joints[rig.BodyJointIndex].localTransform.GetMatrix()).GetPosition().y;

            var RootCurveCnt = CurveInfo.RotationCurveCnt + CurveInfo.PositionCurveCnt;
            
            for (int i = 0; i < AnimationCurves[bodyCurveIndex].Keys.Length; i++)
            {
                var time = AnimationCurves[bodyCurveIndex].Keys[i].time;
                var animRootTransform = jointTransformSamplers[0].Evaluate(time);
                var animRootMat = animRootTransform.GetMatrix();

                var bodyLocalTransform = jointTransformSamplers[rig.BodyJointIndex].Evaluate(time);
                
                var bodyWorldT = bodyToWorldMat * bodyLocalTransform.GetMatrix();
                var bodyWorldRelativeT = animRootMat.inverse * bodyWorldT;

                bodyLocalTransform = new AffineTransform(bodyWorldRelativeT);
                var translate = bodyLocalTransform.t;
                bodyLocalTransform.t = new float3(translate.x,
                    translate.y - bodyWorldHeight, 
                    translate.z);

                for (int j = 0; j < CurveInfo.PositionCurveCnt; j++)
                {
                    curves[j].AddKey(time, bodyLocalTransform.t[j]);
                }
                for (int j = CurveInfo.PositionCurveCnt; j < RootCurveCnt; j++)
                {
                    curves[j].AddKey(time, bodyLocalTransform.q.value[j - CurveInfo.PositionCurveCnt]);
                }
            }
            
            var curveInfos = new CurveInfo[RootCurveCnt];
            for (int i = 0; i < RootCurveCnt; i++)
            {
                curveInfos[i] = new CurveInfo
                {
                    curve = new Curve(curves[i], Allocator.Persistent),
                    jointIndex = rig.BodyJointIndex,
                    curveIndex = i
                };
                var sampler = jointTransformSamplers[rig.BodyJointIndex];
                sampler.SetCurve(i, curveInfos[i].curve);
                jointTransformSamplers[rig.BodyJointIndex] = sampler;
            }

            var toRemoveCurve = animationCurveInfos.GetRange(bodyCurveIndex, RootCurveCnt);
            animationCurveInfos.RemoveRange(bodyCurveIndex, RootCurveCnt);
            toRemoveCurve.ForEach(c => c.curve.Dispose());
            
            animationCurveInfos.InsertRange(bodyCurveIndex, curveInfos);
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
