using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;

namespace Momat.Runtime
{
    [Serializable]
    public struct PlayingSegment
    {
        public PoseIdentifier playBeginPose;
        public float timeFromPoseBegin;

        public int AnimationID => playBeginPose.animationID;
    }

    public enum EBlendMode : byte
    {
        TwoAnimPlayingBlend = 0,
        BlendWithLastFrame = 1,
    }
    
    public partial class AnimationGenerator
    {
        private RuntimeAnimationData runtimeAnimationData;
        
        private float playbackSpeed = 1.0f;
        private float blendTime;
        private readonly float frameRate;

        private Clock clock;

        private PoseIdentifier? nextPlayPose;
        private EBlendMode currBlendMode;

        private IAnimationGeneratorState currentState;
        
        private Vector3 rootVelocity;
        private Vector3 rootAngularVelocity;

        public Vector3 RootVelocity => rootVelocity;
        public Vector3 RootAngularVelocity => rootAngularVelocity;
        
        public AnimationGenerator(RuntimeAnimationData runtimeAnimationData, float blendTime)
        {
            this.runtimeAnimationData = runtimeAnimationData;
            this.blendTime = blendTime;
            frameRate = runtimeAnimationData.frameRate;
            
            clock = new Clock();
            
            SetState(new RestState());
        }

        public void Update(float deltaTime)
        {
            deltaTime *= playbackSpeed;
            clock.Tick(deltaTime);
            currentState.Update(deltaTime);
        }

        public void SetPlaybackSpeed(float playbackSpeed)
        {
            this.playbackSpeed = playbackSpeed;
        }

        public void BeginPlayPose(PoseIdentifier pose, float toBlendTime = 0.1f, EBlendMode blendMode = EBlendMode.TwoAnimPlayingBlend)
        {
            nextPlayPose = pose;
            blendTime = toBlendTime;
            currBlendMode = blendMode;
        }

        public List<AffineTransform> GetCurrPose()
        {
            var pose = new List<AffineTransform>(runtimeAnimationData.rig.NumJoints);
            for (int i = 0; i < runtimeAnimationData.rig.NumJoints; i++)
            {
                pose.Add(GetCurrPoseJointTransform(i));
            }

            return pose;
        }
        
        public AffineTransform GetCurrPoseJointTransform(int jointIndex)
        {
            return currentState.GetCurrPoseJointTransform(jointIndex);
        }

        private static (Vector3, Vector3) CalculateRootMotion
            (AffineTransform prevRootTransform, AffineTransform currRootTransform, float deltaTime)
        {
            // can be seen as transforming currRootTransform to coord-sys of prevRootTransform
            var deltaRootTransform = prevRootTransform.inverse() * currRootTransform;

            var angularDisplacement = ConvertQuaternionMotionToEuler(deltaRootTransform.q);
            
            var velocity = deltaRootTransform.t / deltaTime;
            var angularVelocity = angularDisplacement / deltaTime;

            return (velocity, angularVelocity);
        }

        private static Vector3 ConvertQuaternionMotionToEuler(Quaternion q)
        {
            q.ToAngleAxis(out float angleInDegrees, out Vector3 axis);
            Vector3 angularDisplacement = axis * angleInDegrees * Mathf.Deg2Rad;
            return angularDisplacement;
        }

        private float GetPlayingSegmentCurrTime(PlayingSegment playingSegment)
        {
            return playingSegment.playBeginPose.frameID / frameRate + playingSegment.timeFromPoseBegin;
        }

        private static Vector3 NLerp(Vector3 v1, Vector3 v2, float weight)
        {
            var scale = v1.magnitude * (1 - weight) + v2.magnitude * weight;
            var velocity = v1 * (1 - weight) + v2 * weight;
            if (velocity.magnitude != 0)
            {
                velocity = velocity.normalized * scale;
            }

            return velocity;
        }
    }
}
