using System;
using System.Collections;
using System.Collections.Generic;
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

            public void Enter(MomatAnimator momatAnimator)
            {
                this.momatAnimator = momatAnimator;
                this.momatAnimator.animatorClock.SetTimeStamp();
            }

            public void Update()
            {
                if (momatAnimator.IsIdle())
                {
                    momatAnimator.SetState(new IdleState());
                    return;
                }

                if (momatAnimator.EventTriggered())
                {
                    momatAnimator.SetState(new EventState());
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
                momatAnimator.animatorClock.SetTimeStamp();
                
                idleBeginPose = momatAnimator.SearchPoseInFeatureSet(momatAnimator.GetAllMotionIdleFeatureVectors());
                this.momatAnimator.BeginPlayPose(idleBeginPose, 1.0f, EBlendMode.BlendWithLastFrame);
            }

            public void Update()
            {
                if (momatAnimator.IsIdle() == false)
                {
                    momatAnimator.SetState(new MotionState());
                    return;
                }

                if (momatAnimator.EventTriggered())
                {
                    momatAnimator.SetState(new EventState());
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
            public void Enter(MomatAnimator momatAnimator)
            {
                this.momatAnimator = momatAnimator;
            }

            public void Update()
            {
                if (EventFinished())
                {
                    momatAnimator.SetState(new IdleState());
                }
            }

            public void Exit()
            {
                
            }

            private bool EventFinished()
            {
                return true;
            }
        }
    }
}