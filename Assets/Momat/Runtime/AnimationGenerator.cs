using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Momat.Runtime
{
    public struct PlayingSegment
    {
        public PoseIdentifier playBeginPose;
        public int currFrame;

        public int AnimationID => playBeginPose.animationID;
    }
    
    public class AnimationGenerator
    {
        private enum EPlayState
        {
            eRest,
            ePlayAnim,
            eBlendIntoAnim
        }
        
        private RuntimeAnimationData runtimeAnimationData;
        
        private float blendTime;
        private int playbackFrameRate;

        private PlayingSegment currPlayingSegment;
        private PlayingSegment nextPlayingSegment;
        private Clock clock;
        private float blendBeginTime;
        private float weight;

        public float deltaTime; // temp

        private EPlayState currentState;

        private AffineTransform deltaRootTransform;
        private Transform worldTransform;
        public AffineTransform rootMotion;
        public Vector3 angularSpeed;
        
        public AnimationGenerator(RuntimeAnimationData runtimeAnimationData, Transform transform, float blendTime, int playbackFrameRate)
        {
            this.runtimeAnimationData = runtimeAnimationData;
            this.worldTransform = transform;
            this.blendTime = blendTime;
            this.playbackFrameRate = playbackFrameRate;
            
            clock = new Clock();
            
            currentState = EPlayState.eRest;
        }

        public void UpdateClock(float deltaTime)
        {
            clock.Tick(deltaTime);
            this.deltaTime = deltaTime;
        }
        
        public void UpdatePose()
        {
            var prevRootTransform = GetCurrPoseJointTransform(0);
            int prevFrame = currPlayingSegment.currFrame;
            
            if (currentState == EPlayState.eBlendIntoAnim)
            {
                bool blendFinish = Blend();
                if (blendFinish)
                {
                    BeginPlaySingleAnim();
                }
            }
            else if (currentState == EPlayState.ePlayAnim)
            {
                PlaySingleAnim();
            }
            
            
            var currRootTransform = GetCurrPoseJointTransform(0);
            int currFrame = currPlayingSegment.currFrame;
            
            if (currRootTransform != null && prevRootTransform != null)
            {
                deltaRootTransform = prevRootTransform.Value.inverse() * currRootTransform.Value;
                
                Quaternion q = deltaRootTransform.q;
                q.ToAngleAxis(out float angleInDegrees, out Vector3 axis);
                Vector3 angularDisplacement = axis * angleInDegrees * Mathf.Deg2Rad;    
                angularSpeed = angularDisplacement / deltaTime;

                rootMotion = deltaRootTransform;
                
                var world = new AffineTransform(worldTransform.position, worldTransform.rotation);
                var currRootTransformWorld = world * deltaRootTransform;
                // rootMotion = world.inverse() * currRootTransformWorld;
                
                if (currFrame == 30 && currFrame > prevFrame)
                {
                    Debug.Log($"prevT {prevRootTransform.Value.t} {prevRootTransform.Value.q}\n" +
                              $"currT {currRootTransform.Value.t} {currRootTransform.Value.q}\n" +
                              $"deltaT {deltaRootTransform.t} {deltaRootTransform.q}\n");
                    
                    Vector3 rootMotionAngles = ((Quaternion)rootMotion.q).eulerAngles;
                    Debug.Log($"world {world.q}\n" +
                              $"currWorld {currRootTransformWorld.t} {currRootTransformWorld.q}\n" +
                              $"rootMotion {rootMotion.t} {rootMotion.q}\n" +
                              $"rootAngles {rootMotionAngles}");
                }
            }
        }

        public void BeginBlendIntoPose(PoseIdentifier pose)
        {
            if (currentState == EPlayState.eBlendIntoAnim)
            {
                throw new Exception("Still in Blend");
            }
            
            if (currentState == EPlayState.eRest)
            {
                currPlayingSegment.playBeginPose = pose;
                clock = new Clock();
                currentState = EPlayState.ePlayAnim;
            }
            else
            {
                currentState = EPlayState.eBlendIntoAnim;
                nextPlayingSegment.playBeginPose = pose;
                blendBeginTime = clock.CurrentTime;
            }
        }

        public AffineTransform? GetCurrPoseJointTransform(int jointIndex)
        {
            switch (currentState)
            {
                case EPlayState.eRest:
                    return null;
                case EPlayState.ePlayAnim:
                    return runtimeAnimationData.GetTransform(new PoseIdentifier()
                        {animationID = currPlayingSegment.AnimationID, frameID = currPlayingSegment.currFrame}, jointIndex);
                case EPlayState.eBlendIntoAnim:
                    return AffineTransform.Interpolate(
                        runtimeAnimationData.GetTransform(new PoseIdentifier()
                            {animationID = currPlayingSegment.AnimationID, frameID = currPlayingSegment.currFrame}, jointIndex),
                        runtimeAnimationData.GetTransform(new PoseIdentifier()
                            {animationID = nextPlayingSegment.AnimationID, frameID = nextPlayingSegment.currFrame}, jointIndex),
                        weight);
                
                default:
                    throw new Exception("Should not reach here");
            }
        }
        
        private void BeginPlaySingleAnim()
        {
            currentState = EPlayState.ePlayAnim;

            clock.SetTime(blendTime);

            currPlayingSegment = nextPlayingSegment;
        }

        private bool Blend()
        {
            var blendedTime = clock.CurrentTime - blendBeginTime;

            currPlayingSegment.currFrame =
                (int)(clock.CurrentTime * playbackFrameRate) + currPlayingSegment.playBeginPose.frameID;
            nextPlayingSegment.currFrame =
                (int)(blendedTime * playbackFrameRate) + nextPlayingSegment.playBeginPose.frameID;

            if (blendedTime > blendTime)
            {
                weight = 1;
                return true;
            }
            else
            {
                weight = blendedTime / blendTime; // linear
                return false;
            }
        }

        private void PlaySingleAnim()
        {
            currPlayingSegment.currFrame =
                (int)(clock.CurrentTime * playbackFrameRate) + currPlayingSegment.playBeginPose.frameID;
        }
    }
}
