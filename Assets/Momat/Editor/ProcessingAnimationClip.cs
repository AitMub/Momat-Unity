using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Momat.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace Momat.Editor
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct TypeSpecificData
    {
        [FieldOffset(0)] public EventClipData eventClipData;
    }
    
    [Serializable]
    public class ProcessingAnimationClip
    {
        public AnimationClip sourceAnimClip;
        public EAnimationType animationType;
        public AvatarRetargetMap avatarRetargetMap;

        public TypeSpecificData clipData;
        
        public string Name { get => sourceAnimClip.name; }
    }
}
