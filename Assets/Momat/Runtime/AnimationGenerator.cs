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

        public Vector3 velocity;
        public Vector3 angularVelocity;
        
        public AnimationGenerator(RuntimeAnimationData runtimeAnimationData, Transform transform, float blendTime, int playbackFrameRate)
        {
            this.runtimeAnimationData = runtimeAnimationData;
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

        private void CalculateRootMotion
            (AffineTransform prevRootTransform, AffineTransform currRootTransform)
        {
            var deltaRootTransform = prevRootTransform.inverse() * currRootTransform;
            
            Quaternion q = deltaRootTransform.q;
            q.ToAngleAxis(out float angleInDegrees, out Vector3 axis);
            Vector3 angularDisplacement = axis * angleInDegrees * Mathf.Deg2Rad;    
            
            angularVelocity = angularDisplacement / deltaTime;
            velocity = deltaRootTransform.t / deltaTime;
        }

        public AffineTransform? GetPoseJointTransformAtTime(int jointIndex)
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

            if (blendedTime > blendTime)
            {
                currPlayingSegment.currFrame =
                    (int)(clock.CurrentTime * playbackFrameRate) + currPlayingSegment.playBeginPose.frameID;
                nextPlayingSegment.currFrame =
                    (int)(blendedTime * playbackFrameRate) + nextPlayingSegment.playBeginPose.frameID;

                weight = 1;

                PoseIdentifier prevPi = new PoseIdentifier();
                prevPi.animationID = nextPlayingSegment.AnimationID;
                prevPi.frameID = nextPlayingSegment.currFrame - (int)(deltaTime * playbackFrameRate);
                var prevT = runtimeAnimationData.GetTransform(prevPi, 0);

                PoseIdentifier currPi = new PoseIdentifier();
                currPi.animationID = nextPlayingSegment.AnimationID;
                currPi.frameID = nextPlayingSegment.currFrame;
                var currT = runtimeAnimationData.GetTransform(currPi, 0);
                
                CalculateRootMotion(prevT, currT);

                return true;
            }
            else
            {
                PoseIdentifier currPlayPrevPi = new PoseIdentifier();
                currPlayPrevPi.animationID = currPlayingSegment.AnimationID;
                currPlayPrevPi.frameID = currPlayingSegment.currFrame;
                var currPlayPrevT = runtimeAnimationData.GetTransform(currPlayPrevPi, 0);
                
                PoseIdentifier nextPlayPrevPi = new PoseIdentifier();
                nextPlayPrevPi.animationID = nextPlayingSegment.AnimationID;
                nextPlayPrevPi.frameID = nextPlayingSegment.currFrame;
                var nextPlayPrevT = runtimeAnimationData.GetTransform(currPlayPrevPi, 0);

                currPlayingSegment.currFrame =
                    (int)(clock.CurrentTime * playbackFrameRate) + currPlayingSegment.playBeginPose.frameID;
                nextPlayingSegment.currFrame =
                    (int)(blendedTime * playbackFrameRate) + nextPlayingSegment.playBeginPose.frameID;

                weight = blendedTime / blendTime; // linear
                
                PoseIdentifier currPlayCurrPi = new PoseIdentifier();
                currPlayCurrPi.animationID = currPlayingSegment.AnimationID;
                currPlayCurrPi.frameID = currPlayingSegment.currFrame;
                var currPlayCurrT = runtimeAnimationData.GetTransform(currPlayPrevPi, 0);
                
                PoseIdentifier nextPlayCurrPi = new PoseIdentifier();
                nextPlayCurrPi.animationID = currPlayingSegment.AnimationID;
                nextPlayCurrPi.frameID = currPlayingSegment.currFrame;
                var nextPlayCurrT = runtimeAnimationData.GetTransform(currPlayPrevPi, 0);

                var deltaCurrRootTransform = currPlayPrevT.inverse() * currPlayCurrT;
            
                Quaternion q1 = deltaCurrRootTransform.q;

                var currVelocity = deltaCurrRootTransform.t / deltaTime;

                var deltaNextRootTransform = nextPlayPrevT.inverse() * nextPlayCurrT;
                Quaternion q2 = deltaNextRootTransform.q;

                var nextVelocity = deltaCurrRootTransform.t / deltaTime;

                var cv = (Vector3)currVelocity;
                var nv = (Vector3)nextVelocity;
                var scale = cv.magnitude * (1 - weight) + nv.magnitude * weight;
                velocity = cv * (1 - weight) + nv * weight;
                if(velocity.magnitude != 0)
                    velocity = velocity / (cv + nv).magnitude * scale;

                var q = Quaternion.Slerp(q1, q2, weight);
                q.ToAngleAxis(out float angleInDegrees, out Vector3 axis);
                Vector3 angularDisplacement = axis * angleInDegrees * Mathf.Deg2Rad; 
                angularVelocity = angularDisplacement / deltaTime;
                
                return false;
            }
        }

        private void PlaySingleAnim()
        {
            PoseIdentifier prevPi = new PoseIdentifier();
            prevPi.animationID = currPlayingSegment.AnimationID;
            prevPi.frameID = currPlayingSegment.currFrame;
            var prevT = runtimeAnimationData.GetTransform(prevPi, 0);

            currPlayingSegment.currFrame =
                (int)(clock.CurrentTime * playbackFrameRate) + currPlayingSegment.playBeginPose.frameID;
            
            PoseIdentifier currPi = new PoseIdentifier();
            currPi.animationID = currPlayingSegment.AnimationID;
            currPi.frameID = currPlayingSegment.currFrame;
            var currT = runtimeAnimationData.GetTransform(currPi, 0);

            CalculateRootMotion(prevT, currT);
        }
    }
}
