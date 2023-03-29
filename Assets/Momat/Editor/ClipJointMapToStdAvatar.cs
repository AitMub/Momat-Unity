using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Momat.Editor
{
    [CreateAssetMenu(fileName = "ClipJointMapToStdAvatar", menuName = "Momat/Clip Joint Map To Std Avatar")]
    public class ClipJointMapToStdAvatar : ScriptableObject
    {
        [Serializable]
        public struct MapFromStdAvatarJointNameToClipJointName
        {
            [HideInInspector]
            public string stdAvatarJointName;
            [SerializeField]
            public string clipJointName;
        }

        public AnimationClip clip;
        public List<MapFromStdAvatarJointNameToClipJointName> map;

        private static readonly string[] StdAvatarJointNames =
        {
            "Hips", "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot",
            "Spine", "Chest", "UpperChest", "Neck", "Head", "LeftShoulder", "RightShoulder",
            "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand", "LeftToes",
            "RightToes", "LeftEye", "RightEye", "Jaw", "Left Thumb Proximal", "Left Thumb Intermediate",
            "Left Thumb Distal", "Left Index Proximal", "Left Index Intermediate", "Left Index Distal",
            "Left Middle Proximal", "Left Middle Intermediate", "Left Middle Distal", "Left Ring Proximal",
            "Left Ring Intermediate", "Left Ring Distal", "Left Little Proximal", "Left Little Intermediate",
            "Left Little Distal", "Right Thumb Proximal", "Right Thumb Intermediate", "Right Thumb Distal",
            "Right Index Proximal", "Right Index Intermediate", "Right Index Distal", "Right Middle Proximal",
            "Right Middle Intermediate", "Right Middle Distal", "Right Ring Proximal", "Right Ring Intermediate",
            "Right Ring Distal", "Right Little Proximal", "Right Little Intermediate", "Right Little Distal"
        };

        private string[] clipJointNames;

        public ClipJointMapToStdAvatar()
        {
            map = new List<MapFromStdAvatarJointNameToClipJointName>();
            foreach (var name in StdAvatarJointNames)
            {
                var stdToClip = new MapFromStdAvatarJointNameToClipJointName()
                {
                    stdAvatarJointName = name,
                    clipJointName = ""
                };
                map.Add(stdToClip);
            }
        }
        
        public string[] GetClipJointNames()
        {
            if (clip == null)
            {
                return null;
            }

            if (clipJointNames != null)
            {
                return clipJointNames;
            }

            HashSet<string> names = new HashSet<string>();
            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                var jointName = ExtractJointNameFromPath(binding.path);
                names.Add(jointName);
            }

            clipJointNames = names.ToArray();
            return names.ToArray();
        }

        public string GetJointStdNameFromPath(string jointPath)
        {
            var clipJointName = ExtractJointNameFromPath(jointPath);
            return GetJointStdName(clipJointName);
        }

        public string GetJointStdName(string clipJointName)
        {
            for (int i = 0; i < map.Count; i++)
            {
                if (map[i].clipJointName == clipJointName)
                {
                    return map[i].stdAvatarJointName;
                }
            }

            return null;
        }

        private string ExtractJointNameFromPath(string path)
        {
            string jointName = path;
            
            // remove path
            int index = path.LastIndexOf('/');
            if (index != -1)
            {
                jointName = path.Substring(index + 1);
            }
            
            // remove prefix
            index = jointName.IndexOfAny(new char[]{':'});
            if (index != -1)
            {
                jointName = jointName.Substring(index + 1);
            }
            
            return jointName;
        }
        
        public void TryAutoMap()
        {
            for (int i = 0; i < map.Count; i++)
            {
                if (map[i].clipJointName == "")
                {
                    for (int j = 0; j < clipJointNames.Length; j++)
                    {
                        if (map[i].stdAvatarJointName == clipJointNames[j])
                        {
                            var stdToClip = new MapFromStdAvatarJointNameToClipJointName()
                            {
                                stdAvatarJointName = map[i].stdAvatarJointName,
                                clipJointName = clipJointNames[j]
                            };
                            map[i] = stdToClip;
                        }
                    }
                }
            }
        }
    }
    
    [CustomEditor(typeof(ClipJointMapToStdAvatar))]
    public class ClipJointMapToStdAvatarEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Try Auto Map"))
            {
                var so = target as ClipJointMapToStdAvatar;
                so.TryAutoMap();
            }
        }
    }
    
    [CustomPropertyDrawer(typeof(string))]
    public class StringPropertyDrawer : PropertyDrawer
    {
        private string[] options = {"No Clip"};

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (options.Length == 1)
            {
                var clipJointMapToStdAvatar = (ClipJointMapToStdAvatar)property.serializedObject.targetObject;
                var names = clipJointMapToStdAvatar.GetClipJointNames();
                if (names != null)
                {
                    options = names;
                }
            }
            
            int selectedIndex = -1;
            string selectedOption = property.stringValue;

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == selectedOption)
                {
                    selectedIndex = i;
                    break;
                }
            }

            EditorGUI.BeginChangeCheck();

            selectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, options);

            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = options[selectedIndex];
            }
        }
    }
}
