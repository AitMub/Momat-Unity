using System;
using System.Collections.Generic;
using Momat.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Object = UnityEngine.Object;


namespace Momat.Editor
{
    internal class EventEditElements
    {
        public AnimationPreProcessWindow mainWindow;
        
        public IntegerField eventIDField;
        public IntegerField prepareFrameField;
        public IntegerField beginFrameField;
        public IntegerField beginRecoveryFrameField;
        public IntegerField finishFrameField;

        
        public EventEditElements(AnimationPreProcessWindow window, VisualElement eventDataEditRootVE)
        {
            mainWindow = window;
            
            eventIDField = eventDataEditRootVE.Q<IntegerField>("EventID");
            prepareFrameField = eventDataEditRootVE.Q<IntegerField>("PrepareFrame");
            beginFrameField = eventDataEditRootVE.Q<IntegerField>("BeginFrame");
            beginRecoveryFrameField = eventDataEditRootVE.Q<IntegerField>("BeginRecoveryFrame");
            finishFrameField = eventDataEditRootVE.Q<IntegerField>("FinishFrame");

            eventIDField.RegisterValueChangedCallback
                (e => mainWindow.currAnimationClip.clipData.eventClipData.eventID = e.newValue);
            prepareFrameField.RegisterValueChangedCallback
                (e => mainWindow.currAnimationClip.clipData.eventClipData.prepareFrame = e.newValue);
            beginFrameField.RegisterValueChangedCallback
                (e => mainWindow.currAnimationClip.clipData.eventClipData.beginFrame = e.newValue);
            beginRecoveryFrameField.RegisterValueChangedCallback
                (e => mainWindow.currAnimationClip.clipData.eventClipData.beginRecoveryFrame = e.newValue);
            finishFrameField.RegisterValueChangedCallback
                (e => mainWindow.currAnimationClip.clipData.eventClipData.finishFrame = e.newValue);
        }

        public void Show()
        {
            mainWindow.currAnimationClip.clipData.eventClipData ??= new EventClipData();
                
            eventIDField.value = mainWindow.currAnimationClip.clipData.eventClipData.eventID;
            prepareFrameField.value = mainWindow.currAnimationClip.clipData.eventClipData.prepareFrame;
            beginFrameField.value = mainWindow.currAnimationClip.clipData.eventClipData.beginFrame;
            beginRecoveryFrameField.value = mainWindow.currAnimationClip.clipData.eventClipData.beginRecoveryFrame;
            finishFrameField.value = mainWindow.currAnimationClip.clipData.eventClipData.finishFrame;
        }
    }
    
    internal class AnimationPreProcessWindow : EditorWindow
    {
        // visual elements
        private static AnimationClipListView animationClipListView;
        private static Label animationClipNameLabel;
        private static VisualElement animationPropertyEditView;

        private static VisualElement eventDataEditView;
        private static EventEditElements eventEditElements;
        
        private static DropdownField animationTypeDropdown;
        private static string[] typeStrings;
        
        private static ObjectField retargetMapAssetField;

        private static AnimationPreProcessAsset animationPreProcessAsset;

        private static Timeline timeline;

        public ProcessingAnimationClip currAnimationClip;

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
            
            eventDataEditView = rootVisualElement.Q<VisualElement>("EventDataEditView");
            eventDataEditView.visible = false;
            eventEditElements = new EventEditElements(this, eventDataEditView);

            animationTypeDropdown = rootVisualElement.Q<DropdownField>("AnimationTypeDropdown");
            animationTypeDropdown.RegisterValueChangedCallback(OnAnimationTypeChanged);
            typeStrings = animationTypeDropdown.choices.ToArray();
            
            retargetMapAssetField = rootVisualElement.Q<ObjectField>("RetargetMap");
            retargetMapAssetField.objectType = typeof(AvatarRetargetMap);
            retargetMapAssetField.RegisterValueChangedCallback
                (e => currAnimationClip.avatarRetargetMap = e.newValue as AvatarRetargetMap);
            
            var timelineArea = rootVisualElement.Q<VisualElement>("TimelineArea");
            timeline = new Timeline(this, timelineArea);
        }
        
        private void OnGUI()
        {
            timeline.Draw();
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
            
            var tagDropdown = rootVisualElement.Q<DropdownField>("TagDropDown");
            tagDropdown.choices = animationPreProcessAsset.tags;
            
            animationClipListView.UpdateSource(animationPreProcessAsset.animSet);
        }

        void OnAnimationTypeChanged(ChangeEvent<string> e)
        {
            currAnimationClip.animationType = (EAnimationType)
                Array.IndexOf(typeStrings, e.newValue);

            UpdateAnimationTypeDataEditView();
        }

        internal void OnBuildButtonClicked()
        {
            animationPreProcessAsset.BuildRuntimeData();
            animationClipListView.UpdateSource(animationPreProcessAsset.animSet);
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

                UpdateAnimationTypeDataEditView();
            }
        }

        private void UpdateAnimationTypeDataEditView()
        {
            switch (currAnimationClip.animationType)
            {
                case EAnimationType.EMotion:
                    eventDataEditView.visible = false;
                    break;
                
                case EAnimationType.EIdle:
                    eventDataEditView.visible = false;
                    break;
                
                case EAnimationType.EEvent:
                    eventDataEditView.visible = true;
                    eventEditElements.Show();
                    break;
                
                default:
                    break;
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
