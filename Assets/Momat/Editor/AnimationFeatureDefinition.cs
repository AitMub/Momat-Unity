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
        internal AnimationPreProcessAsset animationPreProcessAsset;
        internal string[] joints;

        public TrajectoryFeatureDefinition trajectoryFeatureDefinition;
        public PoseFeatureDefinition poseFeatureDefinition;
    }

    [CustomEditor(typeof(AnimationFeatureDefinition))]
    public class AnimationFeatureDefinitionCustomEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var animationFeatureDefinition = target as AnimationFeatureDefinition;
            
            var animationPreProcessAsset = 
                EditorGUILayout.ObjectField("Preprocess Asset", animationFeatureDefinition.animationPreProcessAsset, 
                    typeof(AnimationPreProcessAsset), false) as AnimationPreProcessAsset;
            if (animationPreProcessAsset != animationFeatureDefinition.animationPreProcessAsset)
            {
                animationFeatureDefinition.animationPreProcessAsset = animationPreProcessAsset;
                animationFeatureDefinition.poseFeatureDefinition.comparedJoint = new List<string>();
                if (animationPreProcessAsset != null)
                {
                    animationFeatureDefinition.joints = animationFeatureDefinition.animationPreProcessAsset.avatar.GetAvatarJointNames().ToArray();
                }
                else
                {
                    animationFeatureDefinition.joints = null;
                }
            }
            
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField("Trajectory Feature");
            
            EditorGUI.indentLevel++;
            SerializedProperty listProperty = serializedObject.FindProperty("trajectoryFeatureDefinition.trajectoryTimeStamps");
            EditorGUILayout.PropertyField(listProperty, true);
            EditorGUI.indentLevel--;

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Pose Feature");
            
            EditorGUI.indentLevel++;
            
            if (animationFeatureDefinition.joints != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Compared Joints");
                if (GUILayout.Button("Add Compare Joint",  GUILayout.Height(18), GUILayout.Width(150)))
                {
                    animationFeatureDefinition.poseFeatureDefinition.comparedJoint.
                        Add(animationFeatureDefinition.joints[0]);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel++;
                for (int i = 0; i < animationFeatureDefinition.poseFeatureDefinition.comparedJoint.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField($"joint{i + 1}");
                    int selectedIndex = Array.IndexOf(animationFeatureDefinition.joints,
                        animationFeatureDefinition.poseFeatureDefinition.comparedJoint[i]);
                    selectedIndex = EditorGUILayout.Popup(selectedIndex, animationFeatureDefinition.joints);

                    animationFeatureDefinition.poseFeatureDefinition.comparedJoint[i] =
                        selectedIndex >= 0 ? animationFeatureDefinition.joints[selectedIndex] : null;

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

