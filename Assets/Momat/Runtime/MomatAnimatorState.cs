using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        private void SetState(IMomatAnimatorState newState)
        {
            currentState?.Exit();

            newState.Enter(this);
            Debug.Log(newState.ToString());
            currentState = newState;
        }
        
        private interface IMomatAnimatorState
        {
            public void Enter(MomatAnimator momatAnimator);
            public void Update();
            public void Exit();
        }

        private class MotionState : IMomatAnimatorState
        {
            private MomatAnimator momatAnimator;
            private float enterBlendTime;

            public void Enter(MomatAnimator momatAnimator)
            {
                this.momatAnimator = momatAnimator;
                this.momatAnimator.animatorClock.SetTimeStamp();

                enterBlendTime = 0.5f;
                
                var motionBeginPose = this.momatAnimator.SearchPoseInFeatureSet
                    (momatAnimator.GetAllMotionAnimFeatureVectors());
                this.momatAnimator.BeginPlayPose(motionBeginPose, enterBlendTime, EBlendMode.BlendWithLastFrame);
            }

            public void Update()
            {
                if (momatAnimator.EventTriggered())
                {
                    momatAnimator.SetState(new EventState());
                    return;
                }
                
                if (momatAnimator.IsIdle())
                {
                    momatAnimator.SetState(new IdleState());
                    return;
                }
                
                if (CheckNeedUpdateMotionPose())
                {
                    SwitchToNextMotionPose();
                }
            }

            public void Exit()
            {
                
            }

            private bool CheckNeedUpdateMotionPose()
            {
                if (momatAnimator.animatorClock.TimeFromLastTimeStamp < enterBlendTime)
                {
                    return false;
                }
                enterBlendTime = 0f;
                
                if (momatAnimator.animatorClock.TimeFromLastTimeStamp > momatAnimator.updateInterval)
                {
                    momatAnimator.animatorClock.SetTimeStamp();
                    return true;
                }

                return false;
            }

            private void SwitchToNextMotionPose()
            {
                var nextPose = momatAnimator.SearchPoseInFeatureSet(momatAnimator.GetAllMotionAnimFeatureVectors());
                momatAnimator.BeginPlayPose(nextPose, momatAnimator.blendTime);
            }
        }
        
        private class IdleState : IMomatAnimatorState
        {
            private MomatAnimator momatAnimator;

            private PoseIdentifier idleBeginPose;
            
            public void Enter(MomatAnimator momatAnimator)
            {
                this.momatAnimator = momatAnimator;
                this.momatAnimator.animatorClock.SetTimeStamp();
                
                idleBeginPose = this.momatAnimator.SearchPoseInFeatureSet(momatAnimator.GetAllIdleAnimFeatureVectors());
                this.momatAnimator.BeginPlayPose(idleBeginPose, 1.0f, EBlendMode.BlendWithLastFrame);
            }

            public void Update()
            {
                if (momatAnimator.EventTriggered())
                {
                    momatAnimator.SetState(new EventState());
                    return;
                }
                
                if (momatAnimator.IsIdle() == false)
                {
                    momatAnimator.SetState(new MotionState());
                    return;
                }

                if (CheckNeedUpdateIdlePose())
                {
                    SwitchToNextIdlePose();
                }
            }

            public void Exit()
            {
                
            }
            
            private bool CheckNeedUpdateIdlePose()
            {
                var currAnimTotalFrame = momatAnimator.GetAnimationFrameCnt(idleBeginPose.animationID);
                
                if (momatAnimator.animatorClock.TimeFromLastTimeStamp * momatAnimator.FrameRate 
                    > currAnimTotalFrame - idleBeginPose.frameID)
                {
                    momatAnimator.animatorClock.SetTimeStamp();
                    return true;
                }

                return false;
            }

            private void SwitchToNextIdlePose()
            {
                if (momatAnimator.runtimeAnimationData.animationTypeNum[(int)EAnimationType.EIdle] == 1)
                {
                    idleBeginPose = new PoseIdentifier
                    {
                        animationID = momatAnimator.runtimeAnimationData.animationTypeOffset[(int)EAnimationType.EIdle],
                        frameID = 0
                    };
                    momatAnimator.BeginPlayPose(idleBeginPose, 1.0f, EBlendMode.BlendWithLastFrame);
                }
                else
                {
                    // not playing same idle animation continuously
                    var nextIdleAnimationID = Random.Range
                    (momatAnimator.runtimeAnimationData.animationTypeOffset[(int)EAnimationType.EIdle],
                        momatAnimator.runtimeAnimationData.animationTypeOffset[(int)EAnimationType.EIdle + 1] - 1);
                    if (nextIdleAnimationID >= idleBeginPose.animationID)
                    {
                        nextIdleAnimationID++;
                    }
                    
                    idleBeginPose = new PoseIdentifier
                    {
                        animationID = nextIdleAnimationID,
                        frameID = 0
                    };
                    momatAnimator.BeginPlayPose(idleBeginPose, 1.0f, EBlendMode.BlendWithLastFrame);
                }
            }
        }
        
        private class EventState : IMomatAnimatorState
        {
            private MomatAnimator momatAnimator;
            private PoseIdentifier eventBeginPose;
            private EventClipData eventClipData;
            
            public void Enter(MomatAnimator momatAnimator)
            {
                this.momatAnimator = momatAnimator;
                this.momatAnimator.animatorClock.SetTimeStamp();

                eventBeginPose = this.momatAnimator.SearchPoseInFeatureSet(
                        momatAnimator.GetEventBeginPhasePoseFeatureVectors(momatAnimator.toPlayEventID));
                this.momatAnimator.BeginPlayPose(eventBeginPose, momatAnimator.blendTime);

                eventClipData = momatAnimator.runtimeAnimationData.GetEventClipData(eventBeginPose.animationID);
            }

            public void Update()
            {
                if (EventFinished())
                {
                    ReturnToMotionOrIdle();
                    return;
                }

                if (EventBeginRecovery() && momatAnimator.IsIntendingToMove())
                {
                    ReturnToMotionOrIdle();
                }
            }

            public void Exit()
            {
                
            }

            private bool EventFinished()
            {
                float currFrame = momatAnimator.FrameRate * momatAnimator.animatorClock.TimeFromLastTimeStamp +
                                  eventBeginPose.frameID;

                return currFrame >= eventClipData.finishFrame;
            }

            private bool EventBeginRecovery()
            {
                float currFrame = momatAnimator.FrameRate * momatAnimator.animatorClock.TimeFromLastTimeStamp +
                                  eventBeginPose.frameID;

                return currFrame >= eventClipData.beginRecoveryFrame;
            }

            private void ReturnToMotionOrIdle()
            {
                if (momatAnimator.IsIdle())
                {
                    momatAnimator.SetState(new IdleState());
                }
                else
                {
                    momatAnimator.SetState(new MotionState());
                }

                momatAnimator.toPlayEventID = EventClipData.InvalidEventID;
            }
        }
    }
}