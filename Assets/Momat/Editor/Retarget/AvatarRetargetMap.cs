using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Codice.CM.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Momat.Editor
{
    [CreateAssetMenu(fileName = "AvatarRetargetMap", menuName = "Momat/Avatar Retarget Map")]
    public class AvatarRetargetMap : ScriptableObject
    {
        public Avatar sourceAvatar;
        public Avatar targetAvatar;

        public string[] sourceJoints;
        public string[] targetJoints;
        public int[] sourceToTargetIndices;
        
        public void OnAvatarChanged()
        {
            UpdateSource();
            UpdateTarget();
        }

        public void AutoMap()
        {
            if (sourceAvatar.isHuman && targetAvatar.isHuman)
            {
                MapThroughStdMiddleAvatar(); 
            }
            else
            {
                MapByFindingClosestName();
            }
        }

        public string FindTargetNameBySourceName(string name)
        {
            for (int i = 0; i < sourceJoints.Length; i++)
            {
                if (sourceJoints[i] == name)
                {
                    return targetJoints[sourceToTargetIndices[i]];
                }
            }

            return null;
        }

        public string FindTargetNameBySourceIndex(int index)
        {
            if (sourceToTargetIndices[index] > 0)
            {
                return targetJoints[sourceToTargetIndices[index]];
            }

            return null;
        }

        private void UpdateSource()
        {
            if (sourceAvatar != null)
            {
                sourceJoints = sourceAvatar.GetAvatarJointNames().ToArray();
                sourceToTargetIndices = Enumerable.Repeat(-1, sourceJoints.Length).ToArray();
            }
            else
            {
                sourceJoints = null;
                sourceToTargetIndices = null;
            }
        }

        private void UpdateTarget()
        {
            if (targetAvatar != null)
            {
                var targetJointList = targetAvatar.GetAvatarJointNames();
                targetJointList.Insert(0, "None"); // Add an empty element for unmapped joint
                targetJoints = targetJointList.ToArray();
            }
            else
            {
                targetJoints = null;
            }
        }

        private void MapThroughStdMiddleAvatar()
        {
            var sourceMapToStdAvatar = sourceAvatar.humanDescription.human;
            var targetMapToStdAvatar = targetAvatar.humanDescription.human;

            for (int i = 0; i < sourceMapToStdAvatar.Length; i++)
            {
                for (int j = 0; j < targetMapToStdAvatar.Length; j++)
                {
                    if (sourceMapToStdAvatar[i].humanName == targetMapToStdAvatar[j].humanName)
                    {
                        var sourceJointIndex = Array.IndexOf(sourceJoints, sourceMapToStdAvatar[i].boneName);
                        var targetJointIndex = Array.IndexOf(targetJoints, targetMapToStdAvatar[j].boneName);
                        sourceToTargetIndices[sourceJointIndex] = targetJointIndex;
                    }
                }
            }
        }

        private void MapByFindingClosestName()
        {
            for (int i = 0; i < sourceJoints.Length; i++)
            {
                sourceToTargetIndices[i] = FindClosestJointByName(sourceJoints[i], targetJoints);
            }
        }

        private static int FindClosestJointByName(string s, string[] targets)
        {
            int maxSimilarity = int.MinValue;
            int index = -1;
            
            for(int i = 0; i < targets.Length; i++)
            {
                int similarity = ComputeJointNameSimilarity(s, targets[i]);

                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    index = i;
                }
            }

            return index;
        }
        
        private static int ComputeJointNameSimilarity(string s, string t)
        {
            return Utility.LongestCommonSubstrLength(StandardizeJointName(s), 
                StandardizeJointName(t));
        }
        
        private static string StandardizeJointName(string name)
        {
            string[] fingerJoints = { "proximal", "intermediate", "distal" };

            string[] words = Regex.Split(name, @"(?=[A-Z0-9])");
            string result = new string("");
            
            foreach (var oriWord in words)
            {
                var word = oriWord.ToLower();
                if (word == "l")
                {
                    result = "left" + result; // Add as prefix
                }
                else if (word == "r")
                {
                    result = "right" + result;
                }
                else if (word == "1" || word == "2" || word == "3")
                {
                    result += fingerJoints[int.Parse(word) - 1];
                }
                else if (word == "A" || word == "B" || word == "C")
                {
                    result += fingerJoints[(int)word[0] - 64];
                }
                else
                {
                    result += word;
                }
            }

            return result;
        }
    }


    [CustomEditor(typeof(AvatarRetargetMap))]
    public class AvatarRetargetMapCustomEditor : UnityEditor.Editor
    {
        private SerializedProperty sourceAvatarProperty;
        private SerializedProperty targetAvatarProperty;

        public void OnEnable()
        {
            sourceAvatarProperty = serializedObject.FindProperty("sourceAvatar");
            targetAvatarProperty = serializedObject.FindProperty("targetAvatar");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var avatarRetargetMap = target as AvatarRetargetMap;
            
            bool avatarChanged = false;

            
            var newSourceAvatar = EditorGUILayout.ObjectField("Source Avatar", 
                sourceAvatarProperty.objectReferenceValue, typeof(Avatar), false);
            if (newSourceAvatar != sourceAvatarProperty.objectReferenceValue)
            {
                sourceAvatarProperty.objectReferenceValue = newSourceAvatar;
                avatarChanged = true;
            }
            if (newSourceAvatar != null)
            {
                // readonly
                GUI.enabled = false;
                EditorGUILayout.Toggle("Is Humanoid", ((Avatar)sourceAvatarProperty.objectReferenceValue).isHuman);
                GUI.enabled = true;
            }
            
            var newTargetAvatar = EditorGUILayout.ObjectField("Target Avatar", 
                targetAvatarProperty.objectReferenceValue, typeof(Avatar), false);
            if (newTargetAvatar != targetAvatarProperty.objectReferenceValue)
            {
                targetAvatarProperty.objectReferenceValue = newTargetAvatar;
                avatarChanged = true;
            }
            if (newTargetAvatar != null)
            {
                // readonly
                GUI.enabled = false;
                EditorGUILayout.Toggle("Is Humanoid", ((Avatar)targetAvatarProperty.objectReferenceValue).isHuman);
                GUI.enabled = true;
            }
            

            if (avatarRetargetMap.sourceAvatar != null && avatarRetargetMap.targetAvatar != null)
            {
                GUILayout.Space(20);
                if (GUILayout.Button("Auto Map"))
                {
                    avatarRetargetMap.AutoMap();
                }
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Source Joints:");
                EditorGUILayout.LabelField("Target Joints:");
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < avatarRetargetMap.sourceJoints.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                
                    EditorGUILayout.LabelField(avatarRetargetMap.sourceJoints[i]);
                    int selectedIndex = EditorGUILayout.Popup(avatarRetargetMap.sourceToTargetIndices[i], 
                        avatarRetargetMap.targetJoints);
                    avatarRetargetMap.sourceToTargetIndices[i] = selectedIndex;
                
                    EditorGUILayout.EndHorizontal();
                }
            }

            serializedObject.ApplyModifiedProperties();
            
            if (avatarChanged)
            {
                avatarRetargetMap.OnAvatarChanged();
            }
        }
    }
    
}