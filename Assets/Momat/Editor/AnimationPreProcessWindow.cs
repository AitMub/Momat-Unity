using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Object = UnityEngine.Object;


namespace Momat.Editor
{
    public class AnimationPreProcessWindow : EditorWindow
    {
        // visual elements
        private AnimationClipListView animationClipListView;
        private DropdownField animationSetSelectDropdown;
        private Label animationClipNameLabel;

        private AnimationPreProcessAsset animationPreProcessAsset;
        private ProcessingAnimationClip currAnimationClip;

        [MenuItem("Window/Momat/Animation Pre Process Window %q")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AnimationPreProcessWindow>();
            wnd.titleContent = new GUIContent("Momat AnimPreProcessWindow");
        }

        protected void OnEnable()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UxmlPath);
            VisualElement ui = visualTree.Instantiate();
            rootVisualElement.Add(ui);
            
            var animationPreProcessAssetField = rootVisualElement.Q<ObjectField>("AnimationPreProcessAsset");
            animationPreProcessAssetField.objectType = typeof(AnimationPreProcessAsset);
            animationPreProcessAssetField.RegisterValueChangedCallback(OnAnimationPreProcessAssetSelectionChanged);

            var buildButton = rootVisualElement.Q<Button>("BuildButton");
            buildButton.clickable.clicked += OnBuildButtonClicked;

            animationSetSelectDropdown = rootVisualElement.Q<DropdownField>("AnimtionSetSelectDropdown");
            animationSetSelectDropdown.RegisterValueChangedCallback(OnAnimationSetDropdownChanged);
            
            animationClipListView = rootVisualElement.Q<AnimationClipListView>("AnimationClipListView");
            animationClipListView.mainWindow = this;
            
            animationClipNameLabel = rootVisualElement.Q<Label>("AnimationClipName");
        }
        
        // callback
        void OnAnimationPreProcessAssetSelectionChanged(ChangeEvent<Object> e)
        {
            animationPreProcessAsset = e.newValue as AnimationPreProcessAsset;
            animationSetSelectDropdown.index = 0;
            animationClipNameLabel.text = "";

            if (animationPreProcessAsset == null)
            {
                animationClipListView.UpdateSource(null);
                return;
            }
            
            animationClipListView.UpdateSource(animationPreProcessAsset.motionAnimSet);
        }

        void OnAnimationSetDropdownChanged(ChangeEvent<string> evt)
        {
            if (animationPreProcessAsset == null)
            {
                return;
            }
            
            if (animationSetSelectDropdown.index == 0)
            {
                animationClipListView.UpdateSource(animationPreProcessAsset.motionAnimSet);
            }
            else if (animationSetSelectDropdown.index == 1)
            {
                animationClipListView.UpdateSource(animationPreProcessAsset.idleAnimSet);
            }
        }

        internal void OnBuildButtonClicked()
        {
            animationPreProcessAsset.BuildRuntimeData();
        }
            
        internal void UpdateCurrAnimationClip(ProcessingAnimationClip clip)
        {
            currAnimationClip = clip;

            if (clip != null)
            {
                animationClipNameLabel.text = currAnimationClip.Name;
            }
            else
            {
                animationClipNameLabel.text = "";
            }
        }

        internal bool IsEditingAsset()
        {
            return animationPreProcessAsset != null;
        }
        
        internal void AddClipsToAsset(List<AnimationClip> clips)
        {
            animationPreProcessAsset.AddClipsToAnimSet(clips, (AnimationSetEnum)animationSetSelectDropdown.index);
        }

        internal void DeleteClipInAsset(ProcessingAnimationClip clip)
        {
            animationPreProcessAsset.RemoveClipInAnimSet(clip, (AnimationSetEnum)animationSetSelectDropdown.index);
        }

        private const string k_UxmlPath = "Assets/Momat/Editor/AnimationPreProcessWindow.uxml";
    }
}
