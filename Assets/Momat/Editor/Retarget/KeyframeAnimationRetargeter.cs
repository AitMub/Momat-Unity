using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Momat.Editor;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

using CurveInfo = Momat.Editor.KeyframeAnimation.CurveInfo;

namespace Momat.Editor
{
    internal static class KeyframeAnimationRetargeter
    {
        private static AvatarRetargetMap avatarRetargetMap;
        
        private static AnimationRig targetRig;

        private static int[] sourceParentIndices;
        private static AffineTransform[] sourceRigBindTransforms;
        private static AffineTransform[] sourceRigInverseBindTransforms;
        private static AffineTransform[] targetRigBindTransforms;
        private static AffineTransform[] targetRigParentInverseBindTransforms;
        
        public static KeyframeAnimation CreateRetargetAnimation(
            AnimationRig sourceRig,AnimationRig targetRig, 
            KeyframeAnimation sourceAnim, AvatarRetargetMap avatarRetargetMap)
        {
            InitRetargeter(sourceRig, targetRig, avatarRetargetMap);

            var targetAnim = new KeyframeAnimation();
            targetAnim.InitWithRigTransforms(targetRig);

            var sourceCurveInfos = sourceAnim.AnimationCurveInfos;
            for (int i = 0; i < sourceCurveInfos.Count;)
            {
                int sameJointCurveEnd = sourceAnim.FindCurveRangeEndForJoint(i);
                
                var positionCurveRange = sourceAnim.GetPositionCurveRangeIn(i, sameJointCurveEnd);
                var retargetedPosCurve = RetargetJointPositionCurve(sourceAnim, positionCurveRange.Item1);
                if (retargetedPosCurve != null)
                {
                    targetAnim.AnimationCurveInfos.AddRange(retargetedPosCurve);
                }
                
                var rotationCurveRange = sourceAnim.GetRotationCurveRangeIn(i, sameJointCurveEnd);
                var retargetedRotCurve = RetargetJointRotationCurve(sourceAnim, rotationCurveRange.Item1);
                if (retargetedRotCurve != null)
                {
                    targetAnim.AnimationCurveInfos.AddRange(retargetedRotCurve);
                }

                i = sameJointCurveEnd;
            }
            
            return targetAnim;
        }

        private static CurveInfo[] RetargetJointPositionCurve(KeyframeAnimation sourceAnim, int posBeginIndex)
        {
            var sourceCurveInfos = sourceAnim.AnimationCurveInfos;
            var curveInfoX = sourceCurveInfos[posBeginIndex];
            
            var sourceJointIndex = curveInfoX.jointIndex;
            var targetJointIndex = avatarRetargetMap.GetTargetIndexBySourceIndex(sourceJointIndex);
            if (targetJointIndex < 0)
            {
                Debug.LogWarning($"Target rig does not have mapped joint " +
                                 $"for animated joint {sourceJointIndex} in source animation. " +
                                 "Animation for this joint will not be retargeted and will be lost");
                return null;
            }
            
            // init array and elements in it
            var targetPositionCurves = 
                Enumerable.Range(1, 3).Select(i => new AnimationCurve()).ToArray();

            // Ensure that all three retargeted XYZ curves have the same number of keyframes
            // in order to calculate retargeted transform all at once
            var retargetedKeyCnt = curveInfoX.curve.Keys.Length;
            
            for (int i = 0; i < retargetedKeyCnt; i++)
            {
                var time = curveInfoX.curve.Keys[i].time;
                
                var sourceTransform = sourceAnim.JointTransformSamplers[sourceJointIndex].Evaluate(time);
                var targetTransform = ConvertToTargetTransform(sourceTransform, sourceJointIndex, targetJointIndex);

                var pos = targetTransform.t;
                for (int j = 0; j < 3; j++)
                {
                    targetPositionCurves[j].AddKey(time, pos[j]);
                }
            }

            var targetCurveInfos = new CurveInfo[3];

            for (int i = 0; i < 3; i++)
            {
                targetCurveInfos[i] = new CurveInfo
                {
                    curve = new Curve(targetPositionCurves[i], Allocator.Persistent),
                    jointIndex = targetJointIndex,
                    curveIndex = i
                };
            }

            return targetCurveInfos;
        }
        
        private static CurveInfo[] RetargetJointRotationCurve(KeyframeAnimation sourceAnim, int rotBeginIndex)
        {
            var sourceCurveInfos = sourceAnim.AnimationCurveInfos;
            var curveInfoX = sourceCurveInfos[rotBeginIndex];
                        
            var sourceJointIndex = curveInfoX.jointIndex;
            var targetJointIndex = avatarRetargetMap.GetTargetIndexBySourceIndex(sourceJointIndex);
            if (targetJointIndex < 0)
            {
                Debug.LogWarning($"Target rig does not have mapped joint " +
                                 $"for animated joint {sourceJointIndex} in source animation. " +
                                 $"Animation for this joint will not be retargeted");

                return null;
            }
            
            var targetRotationCurves =                 
                Enumerable.Range(1, 4).Select(i => new AnimationCurve()).ToArray();

            // Ensure that all three retargeted XYZW curves have the same number of keyframes
            // in order to calculate retargeted transform all at once
            var retargetedKeyCnt = curveInfoX.curve.Keys.Length;

            for (int i = 0; i < retargetedKeyCnt; i++)
            {
                var time = curveInfoX.curve.Keys[i].time;
                
                var sourceTransform = sourceAnim.JointTransformSamplers[sourceJointIndex].Evaluate(time);
                var targetTransform = ConvertToTargetTransform(sourceTransform, sourceJointIndex, targetJointIndex);

                var rot = targetTransform.q;
                for (int j = 0; j < 4; j++)
                {
                    targetRotationCurves[j].AddKey(time, rot.value[j]);
                }
            }

            var targetCurveInfos = new CurveInfo[4];

            for (int i = 0; i < 4; i++)
            {
                targetCurveInfos[i] = new CurveInfo
                {
                    curve = new Curve(targetRotationCurves[i], Allocator.Persistent),
                    jointIndex = targetJointIndex,
                    curveIndex = i + 3
                };
            }

            return targetCurveInfos;
        }

        private static AffineTransform ConvertToTargetTransform
            (AffineTransform sourceTransform, int sourceJointIndex, int targetJointIndex)
        {
            int sourceParentIndex = sourceParentIndices[sourceJointIndex];
            
            if (sourceParentIndex > 0)
            {
                AffineTransform sJointWorldTransformInAnim = 
                    sourceRigBindTransforms[sourceParentIndex] * sourceTransform;
            
                AffineTransform sJointRelativeTransformWorldSpace =
                    sJointWorldTransformInAnim * sourceRigInverseBindTransforms[sourceJointIndex];

                AffineTransform tJointWorldTransformInAnim =
                    sJointRelativeTransformWorldSpace * targetRigBindTransforms[targetJointIndex];

                AffineTransform tJointLocalTransformInAnim =
                    targetRigParentInverseBindTransforms[targetJointIndex] * tJointWorldTransformInAnim;

                return tJointLocalTransformInAnim;
            }

            if (sourceParentIndex == 0)
            {
                return sourceTransform * targetRig.Joints[targetJointIndex].localTransform;
            }

            return sourceTransform; // root transform can apply to target directly
        }

        private static void InitRetargeter
            (AnimationRig sourceRig, AnimationRig targetRig, AvatarRetargetMap avatarRetargetMap)
        {
            KeyframeAnimationRetargeter.avatarRetargetMap = avatarRetargetMap;

            KeyframeAnimationRetargeter.targetRig = targetRig;

            sourceParentIndices = sourceRig.GenerateParentIndices();
            
            sourceRigBindTransforms = sourceRig.GenerateWorldMatrices();
            sourceRigInverseBindTransforms = new AffineTransform[sourceRigBindTransforms.Length];
            for (int i = 0; i < sourceRigBindTransforms.Length; i++)
            {
                sourceRigInverseBindTransforms[i] = sourceRigBindTransforms[i].inverse();
            }

            targetRigBindTransforms = targetRig.GenerateWorldMatrices();
            targetRigParentInverseBindTransforms = new AffineTransform[targetRigBindTransforms.Length];
            for (int i = 1; i < targetRigBindTransforms.Length; i++)
            {
                targetRigParentInverseBindTransforms[i] =
                    targetRigBindTransforms[targetRig.Joints[i].parentIndex].inverse();
            }
        }
    }
    
}
