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
                targetAnim.AnimationCurveInfos.AddRange(retargetedPosCurve);
                
                var rotationCurveRange = sourceAnim.GetRotationCurveRangeIn(i, sameJointCurveEnd);
                var retargetedRotCurve = RetargetJointRotationCurve(sourceAnim, rotationCurveRange.Item1);
                targetAnim.AnimationCurveInfos.AddRange(retargetedRotCurve);

                i = sameJointCurveEnd;
            }
            
            return targetAnim;
        }

        private static CurveInfo[] RetargetJointPositionCurve(KeyframeAnimation sourceAnim, int posBeginIndex)
        {            
            var targetPositionCurves = new AnimationCurve[3];

            var sourceCurveInfos = sourceAnim.AnimationCurveInfos;
            var curveInfoX = sourceCurveInfos[posBeginIndex];

            // Ensure that all three retargeted XYZ curves have the same number of keyframes
            // in order to calculate retargeted transform all at once
            var retargetedKeyCnt = curveInfoX.curve.Keys.Length;
            
            var sourceJointIndex = curveInfoX.jointIndex;
            var targetJointIndex = GetTargetJointIndexBySourceIndex(sourceJointIndex);

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
            var targetRotationCurves = new AnimationCurve[3];

            var sourceCurveInfos = sourceAnim.AnimationCurveInfos;
            var curveInfoX = sourceCurveInfos[rotBeginIndex];

            // Ensure that all three retargeted XYZW curves have the same number of keyframes
            // in order to calculate retargeted transform all at once
            var retargetedKeyCnt = curveInfoX.curve.Keys.Length;
            
            var sourceJointIndex = curveInfoX.jointIndex;
            var targetJointIndex = GetTargetJointIndexBySourceIndex(sourceJointIndex);

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

            var targetCurveInfos = new CurveInfo[3];

            for (int i = 0; i < 4; i++)
            {
                targetCurveInfos[i] = new CurveInfo
                {
                    curve = new Curve(targetRotationCurves[i], Allocator.Persistent),
                    jointIndex = targetJointIndex,
                    curveIndex = i
                };
            }

            return targetCurveInfos;
        }

        private static AffineTransform ConvertToTargetTransform
            (AffineTransform sourceTransform, int sourceJointIndex, int targetJointIndex)
        {
            int sourceParentIndex = sourceParentIndices[sourceJointIndex];
            
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

        private static void InitRetargeter
            (AnimationRig sourceRig, AnimationRig targetRig, AvatarRetargetMap avatarRetargetMap)
        {
            KeyframeAnimationRetargeter.avatarRetargetMap = avatarRetargetMap;

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

        private static int GetTargetJointIndexBySourceIndex(int sourceIndex)
        {
            return avatarRetargetMap.sourceToTargetIndices[sourceIndex];
        }
    }
    
}
