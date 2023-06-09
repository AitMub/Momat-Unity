using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Momat.Editor
{
    [CreateAssetMenu(fileName = "AnimationFeatureDefinition", menuName = "Momat/Animation Feature Definition")]
    public class AnimationFeatureDefinition : ScriptableObject
    {
        [SerializeField]
        internal Avatar avatar;

        public TrajectoryFeatureDefinition trajectoryFeatureDefinition;
        public PoseFeatureDefinition poseFeatureDefinition;
    }

    [CustomEditor(typeof(AnimationFeatureDefinition))]
    public class AnimationFeatureDefinitionCustomEditor : UnityEditor.Editor
    {
        internal string[] joints;

        private SerializedProperty avatarProperty;
        private SerializedProperty timeStampsListProperty;

        private void OnEnable()
        {
            var animationFeatureDefinition = target as AnimationFeatureDefinition;
            joints = animationFeatureDefinition.avatar.GetAvatarJointNames().ToArray();

            avatarProperty = serializedObject.FindProperty("avatar");
            timeStampsListProperty = serializedObject.FindProperty("trajectoryFeatureDefinition.trajectoryTimeStamps");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var animationFeatureDefinition = target as AnimationFeatureDefinition;
            
            var avatar = 
                EditorGUILayout.ObjectField("Avatar", avatarProperty.objectReferenceValue, 
                    typeof(Avatar), false) as Avatar;
            if (avatar != avatarProperty.objectReferenceValue)
            {
                avatarProperty.objectReferenceValue = avatar;
                
                animationFeatureDefinition.avatar = avatar;
                animationFeatureDefinition.poseFeatureDefinition.comparedJoint = new List<string>();
                joints = avatar != null ? avatar.GetAvatarJointNames().ToArray() : null;
            }
            
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField("Trajectory Feature");
            
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(timeStampsListProperty, true);
            EditorGUI.indentLevel--;

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Pose Feature");
            
            EditorGUI.indentLevel++;
            
            if (joints != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Compared Joints");
                if (GUILayout.Button("Add Compare Joint",  GUILayout.Height(18), GUILayout.Width(150)))
                {
                    animationFeatureDefinition.poseFeatureDefinition.comparedJoint.
                        Add(joints[0]);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel++;
                for (int i = 0; i < animationFeatureDefinition.poseFeatureDefinition.comparedJoint.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField($"joint{i + 1}");
                    int selectedIndex = Array.IndexOf(joints,
                        animationFeatureDefinition.poseFeatureDefinition.comparedJoint[i]);
                    selectedIndex = EditorGUILayout.Popup(selectedIndex, joints);

                    animationFeatureDefinition.poseFeatureDefinition.comparedJoint[i] =
                        selectedIndex >= 0 ? joints[selectedIndex] : null;

                    if (GUILayout.Button("-", GUILayout.Height(18), GUILayout.Width(20)))
                    {
                        animationFeatureDefinition.poseFeatureDefinition.comparedJoint.RemoveAt(i);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;

                GUILayout.Space(5);
            }

            animationFeatureDefinition.poseFeatureDefinition.comparedPastFuturePoseNum = 
            EditorGUILayout.IntField("Compared Past Future Pose Num",
                animationFeatureDefinition.poseFeatureDefinition.comparedPastFuturePoseNum);
            
            animationFeatureDefinition.poseFeatureDefinition.comparePosition = 
                EditorGUILayout.Toggle("Compare Position", 
                animationFeatureDefinition.poseFeatureDefinition.comparePosition);
            
            animationFeatureDefinition.poseFeatureDefinition.compareVelocity =
            EditorGUILayout.Toggle("Compare Velocity", 
                animationFeatureDefinition.poseFeatureDefinition.compareVelocity);
            
            EditorGUI.indentLevel--;
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}

