using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace Momat.Runtime
{
    [Serializable]
    public class EventClipData
    {
        public const int InvalidEventID = -1;
        public int eventID;

        public int prepareFrame;
        public int beginFrame;
        public int beginRecoveryFrame;
        public int finishFrame;
    }
    
    public partial class RuntimeAnimationData : ScriptableObject
    {
        public EventClipData[] eventClipDatas;

        public EventClipData GetEventClipData(int animationID)
        {
            return eventClipDatas[animationID - animationTypeOffset[(int)EAnimationType.EEvent]];
        }
    }
}