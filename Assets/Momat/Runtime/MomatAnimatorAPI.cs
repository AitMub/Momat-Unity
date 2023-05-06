using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Random = System.Random;


namespace Momat.Runtime
{
    public partial class MomatAnimator : MonoBehaviour
    {
        public void SetFutureLocalTrajectory(RuntimeTrajectory futureTrajectory)
        {
            futureLocalTrajectory = futureTrajectory;
        }

        public bool EventTriggered()
        {
            return toPlayEventID != EventClipData.InvalidEventID;
        }

        public bool TryTriggerEvent(int eventID)
        {
            if (EventTriggered())
            {
                return false;
            }
            
            toPlayEventID = eventID;
            return true;
        }
    }
}