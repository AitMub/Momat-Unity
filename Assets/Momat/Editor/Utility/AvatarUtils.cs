using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Momat.Editor
{
    internal static class AvatarExtension
    {
        public static List<AnimationRig.Joint> GetAvatarJoints(this Avatar avatar)
        {
            var jointList = avatar.GetAvatarJointsInRestPose();
            jointList = avatar.ForceTPoseInAvatarConfig(jointList);
            return jointList;
        }
        
        public static List<string> GetAvatarJointNames(this Avatar avatar)
        {
            if (avatar.isHuman)
            {
                // exclude root node
                var jointNames = new List<string>(avatar.humanDescription.skeleton.Length - 1); 
                for (int i = 1; i <= avatar.humanDescription.skeleton.Length - 1; i++)
                {
                    jointNames.Add(avatar.humanDescription.skeleton[i].name);
                }
                return jointNames;
            }
            else
            {
                var jointList = avatar.GetAvatarJointsInRestPose();
                List<string> names = jointList.Select(j => j.name).ToList();
                return names;
            }
        }

        private static List<AnimationRig.Joint> ForceTPoseInAvatarConfig(this Avatar avatar,
            List<AnimationRig.Joint> jointList)
        {
            if (avatar.isHuman == false)
            {
                Debug.LogWarning($"Avatar {avatar.name} is not human avatar and " +
                                 $"does not have T-Pose set in Avatar config");
            }
            
            var skeletonBone = avatar.humanDescription.skeleton;
            
            for (int i = 0; i < jointList.Count; i++)
            {
                foreach (var bone in skeletonBone)
                {
                    if (bone.name == jointList[i].name)
                    {
                        jointList[i] = new AnimationRig.Joint()
                        {
                            name = jointList[i].name,
                            parentIndex = jointList[i].parentIndex,
                            localTransform = AffineTransform.Create(bone.position, bone.rotation)
                        };
                    }
                }
            }

            return jointList;
        }

        private static List<AnimationRig.Joint> GetAvatarJointsInRestPose(this Avatar avatar)
        {
            string assetPath = AssetDatabase.GetAssetPath(avatar);
            GameObject avatarRootObject = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;
            if (avatarRootObject == null)
            {
                throw new("Avatar Asset Not Found Exception");
            }
            
            List<AnimationRig.Joint> jointList = new List<AnimationRig.Joint>();
            jointList.Add(new AnimationRig.Joint()
            {
                name = avatarRootObject.transform.name,
                parentIndex = -1,
                localTransform = AffineTransform.Create(avatarRootObject.transform.localPosition, avatarRootObject.transform.localRotation),
            });
            
            foreach (Transform child in avatarRootObject.transform)
            {
                AnimationRig.CollectJointsRecursive(jointList, child, 0);
            }

            return jointList;
        }
    }
}
