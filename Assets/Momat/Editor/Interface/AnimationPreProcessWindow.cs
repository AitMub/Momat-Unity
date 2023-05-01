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
        private Label animationClipNameLabel;
        private VisualElement animationPropertyEditView;
        
        private DropdownField animationTypeDropdown;
        private string[] typeStrings;
        
        private ObjectField retargetMapAssetField;

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

            var saveButton = rootVisualElement.Q<Button>("SaveButton");
            saveButton.clickable.clicked += SaveAsset;
            
            var buildButton = rootVisualElement.Q<Button>("BuildButton");
            buildButton.clickable.clicked += OnBuildButtonClicked;

            animationClipListView = rootVisualElement.Q<AnimationClipListView>("AnimationClipListView");
            animationClipListView.mainWindow = this;
            
            animationClipNameLabel = rootVisualElement.Q<Label>("AnimationClipName");

            animationPropertyEditView = rootVisualElement.Q<VisualElement>("AnimationPropertyEditView");
            animationPropertyEditView.visible = false;

            animationTypeDropdown = rootVisualElement.Q<DropdownField>("AnimationTypeDropdown");
            animationTypeDropdown.RegisterValueChangedCallback(OnAnimationTypeChanged);
            typeStrings = animationTypeDropdown.choices.ToArray();
            
            retargetMapAssetField = rootVisualElement.Q<ObjectField>("RetargetMap");
            retargetMapAssetField.objectType = typeof(AvatarRetargetMap);
            retargetMapAssetField.RegisterValueChangedCallback(OnRetargetMapChanged);
        }
        
        // callback
        void OnAnimationPreProcessAssetSelectionChanged(ChangeEvent<Object> e)
        {
            animationPreProcessAsset = e.newValue as AnimationPreProcessAsset;
            animationClipNameLabel.text = "";

            if (animationPreProcessAsset == null)
            {
                animationClipListView.UpdateSource(null);
                return;
            }
            
            animationClipListView.UpdateSource(animationPreProcessAsset.animSet);
        }

        void OnAnimationTypeChanged(ChangeEvent<string> e)
        {
            currAnimationClip.animationType = (AnimationTypeEnum)
                Array.IndexOf(typeStrings, e.newValue);
        }

        void OnRetargetMapChanged(ChangeEvent<Object> e)
        {
            currAnimationClip.avatarRetargetMap = e.newValue as AvatarRetargetMap;
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
            
            UpdateAnimationPropertyEditView();
        }

        private void UpdateAnimationPropertyEditView()
        {
            if (currAnimationClip == null)
            {
                animationPropertyEditView.visible = false;
            }
            else
            {
                animationPropertyEditView.visible = true;
                animationTypeDropdown.value = typeStrings[(int)currAnimationClip.animationType];
                retargetMapAssetField.value = currAnimationClip.avatarRetargetMap;
            }
        }

        private void SaveAsset()
        {
            var animationPreProcessAssetField = rootVisualElement.Q<ObjectField>("AnimationPreProcessAsset");
            EditorUtility.SetDirty(animationPreProcessAssetField.value);
            AssetDatabase.SaveAssetIfDirty(animationPreProcessAssetField.value);
        }

        internal bool IsEditingAsset()
        {
            return animationPreProcessAsset != null;
        }
        
        internal void AddClipsToAsset(List<AnimationClip> clips)
        {
            animationPreProcessAsset.AddClipsToAnimSet(clips);
        }

        internal void DeleteClipInAsset(ProcessingAnimationClip clip)
        {
            animationPreProcessAsset.RemoveClipInAnimSet(clip);
        }

        private const string k_UxmlPath = "Assets/Momat/Editor/Interface/AnimationPreProcessWindow.uxml";
    }
}
