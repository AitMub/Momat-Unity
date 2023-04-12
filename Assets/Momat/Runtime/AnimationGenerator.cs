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

        private EPlayState currentState;
        
        public AnimationGenerator(RuntimeAnimationData runtimeAnimationData, float blendTime, int playbackFrameRate)
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
