using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Vector3 = System.Numerics.Vector3;

namespace Momat.Runtime
{
    public partial class AnimationGenerator
    {
        private void SetState(IAnimationGeneratorState newState)
        {
            StateContext context = new StateContext();
            if (currentState != null)
            {
                context = currentState.Exit();
            }
            
            newState.Enter(this, context);
            currentState = newState;
        }
        
        private enum EStateType
        {
            eRest, ePlaySingle, eBlendAnim,
        }
        
        private struct StateContext
        {
            public bool isPrevPoseGenerated;
            
            public PoseIdentifier prevPose;
            public float prevPosePlayedTime;

            public List<AffineTransform> prevGeneratedPose;
        }
        
        private interface IAnimationGeneratorState
        {
            public void Enter(AnimationGenerator animationGenerator, StateContext context);
            public void Update(float deltaTime);
            public StateContext Exit();
            public AffineTransform GetCurrPoseJointTransform(int jointIndex);
        }

        
        private class RestState : IAnimationGeneratorState
        {
            private AnimationGenerator animationGenerator;
            private PoseIdentifier restPose;

            public void Enter(AnimationGenerator animationGenerator, StateContext context)
            {
                this.animationGenerator = animationGenerator;
                restPose = context.prevPose; // To Do: Enter with generated pose
            }
    
            public void Update(float deltaTime)
            {
                if (animationGenerator.nextPlayPose != null)
                {
                    animationGenerator.SetState(new BlendAnimState());
                }
            }
    
            public StateContext Exit()
            {
                return new StateContext { prevPose = restPose, prevPosePlayedTime = 0 };
            }

            public AffineTransform GetCurrPoseJointTransform(int jointIndex)
            {
                return animationGenerator.runtimeAnimationData.GetPoseTransform(restPose, jointIndex);
            }
            

            public EStateType GetStateType()
            {
                return GetStaticType();
            }
            private static EStateType GetStaticType()
            {
                return EStateType.eRest;
            }
        }
        
        
        private class PlaySingleAnimState : IAnimationGeneratorState
        {
            private AnimationGenerator animationGenerator;

            private Clock clock;
            private PlayingSegment playingSegment;

            private AffineTransform prevRootTransform;

            public void Enter(AnimationGenerator animationGenerator, StateContext context)
            {
                if (context.isPrevPoseGenerated)
                {
                    throw new Exception("Can not continue playing with generated pose");
                }
                
                this.animationGenerator = animationGenerator;
                
                clock = new Clock();
                
                playingSegment.playBeginPose = context.prevPose;
                playingSegment.timeFromPoseBegin = context.prevPosePlayedTime;
                
                prevRootTransform = animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                    (playingSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(playingSegment), 0);
            }
    
            public void Update(float deltaTime)
            {
                clock.Tick(deltaTime);
                
                if (animationGenerator.nextPlayPose != null)
                {
                    animationGenerator.SetState(new BlendAnimState());
                }
                
                PlaySingleAnim();
            }
    
            public StateContext Exit()
            {
                return new StateContext 
                { prevPose = playingSegment.playBeginPose,
                    prevPosePlayedTime = playingSegment.timeFromPoseBegin };
            }

            public AffineTransform GetCurrPoseJointTransform(int jointIndex)
            {
                return animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                    (playingSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(playingSegment), jointIndex);
            }

            private void PlaySingleAnim()
            {
                playingSegment.timeFromPoseBegin += clock.DeltaTime;
                
                var currRootTransform =
                    animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                        (playingSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(playingSegment), 0);
                
                var motion = CalculateRootMotion
                    (prevRootTransform, currRootTransform, clock.DeltaTime);
                animationGenerator.rootVelocity = motion.Item1;
                animationGenerator.rootAngularVelocity = motion.Item2;
                
                prevRootTransform = currRootTransform;
            }
            
            public EStateType GetStateType()
            {
                return GetStaticType();
            }
            private static EStateType GetStaticType()
            {
                return EStateType.ePlaySingle;
            }
        }
        
        
        private class BlendAnimState : IAnimationGeneratorState
        {
            private AnimationGenerator animationGenerator;

            private Clock clock;
            private float weight;
            
            private PlayingSegment fadeOutSegment;
            private PlayingSegment fadeInSegment;

            private AffineTransform fadeOutPrevRootTransform;
            private AffineTransform fadeInPrevRootTransform;

            private List<AffineTransform> prevGeneratedPose;

            public void Enter(AnimationGenerator animationGenerator, StateContext context)
            {
                this.animationGenerator = animationGenerator;

                clock = new Clock();

                if (context.isPrevPoseGenerated)
                {
                    prevGeneratedPose = this.animationGenerator.GetCurrPose();
                }
                else
                {
                    fadeOutSegment.playBeginPose = context.prevPose;
                    fadeOutSegment.timeFromPoseBegin = context.prevPosePlayedTime;
                    fadeOutPrevRootTransform = animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                        (fadeOutSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(fadeOutSegment), 0);
                }

                fadeInSegment.playBeginPose = animationGenerator.nextPlayPose.Value;
                fadeInSegment.timeFromPoseBegin = 0;
                fadeInPrevRootTransform = animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                    (fadeInSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(fadeInSegment), 0);
                
                animationGenerator.nextPlayPose = null;
            }
    
            public void Update(float deltaTime)
            {
                clock.Tick(deltaTime);
                
                if (BlendFinish())
                {
                    animationGenerator.SetState(new PlaySingleAnimState());
                }
                else if (animationGenerator.nextPlayPose != null)
                {
                    animationGenerator.SetState(new BlendAnimState());
                }
                else
                {
                    Blend();
                }
            }
    
            public StateContext Exit()
            {
                if (BlendFinish())
                {
                    return new StateContext 
                    { 
                        isPrevPoseGenerated = false,
                        prevPose = fadeInSegment.playBeginPose,
                        prevPosePlayedTime = fadeInSegment.timeFromPoseBegin 
                    };
                }
                else
                {
                    return new StateContext
                    {
                        isPrevPoseGenerated = true,
                        prevPose = fadeInSegment.playBeginPose,
                        prevPosePlayedTime = fadeInSegment.timeFromPoseBegin
                    };
                }
            }
            
            public AffineTransform GetCurrPoseJointTransform(int jointIndex)
            {
                var fadeInTransform = animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                    (fadeInSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(fadeInSegment), jointIndex);
                
                if (prevGeneratedPose != null)
                {
                    return AffineTransform.Interpolate(prevGeneratedPose[jointIndex], fadeInTransform, weight);
                }
                else
                {
                    var fadeOutTransform = animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                        (fadeOutSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(fadeOutSegment), jointIndex);
                    return AffineTransform.Interpolate(fadeOutTransform, fadeInTransform, weight);
                }
            }
            
            private bool BlendFinish()
            {
                return clock.CurrentTime > animationGenerator.blendTime;
            }

            private void Blend()
            {
                fadeInSegment.timeFromPoseBegin += clock.DeltaTime;
                weight = clock.CurrentTime / animationGenerator.blendTime;
                
                var fadeInCurrRootTransform =
                    animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                        (fadeInSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(fadeInSegment), 0);
                
                var deltaFadeInRootTransform = fadeInPrevRootTransform.inverse() * fadeInCurrRootTransform;
                
                fadeInPrevRootTransform = fadeInCurrRootTransform;


                var deltaFadeOutRootTransform = new AffineTransform();
                if (animationGenerator.currBlendMode == EBlendMode.TwoAnimPlayingBlend && prevGeneratedPose == null)
                {
                    fadeOutSegment.timeFromPoseBegin += clock.DeltaTime;
                    
                    var fadeOutCurrRootTransform =
                        animationGenerator.runtimeAnimationData.GetAnimationTransformAtTime
                            (fadeOutSegment.AnimationID, animationGenerator.GetPlayingSegmentCurrTime(fadeOutSegment), 0);

                    deltaFadeOutRootTransform = fadeOutPrevRootTransform.inverse() * fadeOutCurrRootTransform;
                    
                    fadeOutPrevRootTransform = fadeOutCurrRootTransform;
                }
                
                var deltaT = NLerp
                    (deltaFadeOutRootTransform.t, deltaFadeInRootTransform.t, weight);
                animationGenerator.rootVelocity = deltaT / clock.DeltaTime;
                
                var deltaQ = Quaternion.Slerp
                    (deltaFadeOutRootTransform.q, deltaFadeInRootTransform.q, weight);
                animationGenerator.rootAngularVelocity = ConvertQuaternionMotionToEuler(deltaQ) / clock.DeltaTime;
            }

            public EStateType GetStateType()
            {
                return GetStaticType();
            }
            private static EStateType GetStaticType()
            {
                return EStateType.eBlendAnim;
            }
        }
    }
}
