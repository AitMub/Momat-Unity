using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;

namespace Momat.Runtime
{
    public struct PlayingSegment
    {
        public PoseIdentifier playBeginPose;
        public float timeFromPoseBegin;

        public int AnimationID => playBeginPose.animationID;
    }
    
    public partial class AnimationGenerator
    {
        private RuntimeAnimationData runtimeAnimationData;
        
        private float blendTime;
        private float playbackSpeed = 1.0f;

        private Clock clock;

        private PoseIdentifier? nextPlayPose;

        private IAnimationGeneratorState currentState;
        
        private Vector3 rootVelocity;
        private Vector3 rootAngularVelocity;

        public Vector3 RootVelocity => rootVelocity;
        public Vector3 RootAngularVelocity => rootAngularVelocity;
        
        public AnimationGenerator(RuntimeAnimationData runtimeAnimationData, float blendTime, float playbackSpeed)
        {
            this.runtimeAnimationData = runtimeAnimationData;
            this.blendTime = blendTime;
            this.playbackSpeed = playbackSpeed;
            
            clock = new Clock();

            StateContext context = new StateContext();
            currentState = new RestState();
            currentState.Enter(this, context);
        }

        public void Update(float deltaTime)
        {
            deltaTime *= playbackSpeed;
            clock.Tick(deltaTime);
            currentState.Update(deltaTime);
        }

        public void BeginPlayPose(PoseIdentifier pose)
        {
            nextPlayPose = pose;
        }
        
        public AffineTransform GetPoseJointTransformAtTime(int jointIndex)
        {
            return currentState.GetCurrPoseJointTransform(jointIndex);
        }

        private void SetState(IAnimationGeneratorState newState)
        {
            var context = currentState.Exit();
            newState.Enter(this, context);
            currentState = newState;
        }

        private static (Vector3, Vector3) CalculateRootMotion
            (AffineTransform prevRootTransform, AffineTransform currRootTransform, float deltaTime)
        {
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

        private static Vector3 NLerp(Vector3 v1, Vector3 v2, float weight)
        {
            var scale = v1.magnitude * (1 - weight) + v2.magnitude * weight;
            var velocity = v1 * (1 - weight) + v2 * weight;
            if (velocity.magnitude != 0)
            {
                velocity = velocity / (v1 + v2).magnitude * scale;
            }

            return velocity;
        }
    }
}
