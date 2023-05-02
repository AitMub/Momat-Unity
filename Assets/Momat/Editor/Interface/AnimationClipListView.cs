using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Momat.Editor
{
    class AnimationClipListView : ListView
    {
        public new class UxmlFactory : UxmlFactory<AnimationClipListView, UxmlTraits>{ }
        public new class UxmlTraits : BindableElement.UxmlTraits { }
        
        
        internal AnimationPreProcessWindow mainWindow;

        public AnimationClipListView()
        {
            fixedItemHeight = 15;
            selectionType = SelectionType.Single;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            onSelectionChange += OnAnimationClipSelectionChanged;
            makeItem += MakeItem;
            bindItem += BindItem;
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            UnregisterCallback<DragUpdatedEvent>(OnDragUpdate);
            UnregisterCallback<DragPerformEvent>(OnDragPerform);
        }

        void OnAnimationClipSelectionChanged(IEnumerable<object> animationClips)
        {
            // only one clip could be selected
            mainWindow.UpdateCurrAnimationClip(animationClips.FirstOrDefault() as ProcessingAnimationClip);
        }

        void OnDragUpdate(DragUpdatedEvent evt)
        {
            if (!EditorApplication.isPlaying && DragAndDrop.objectReferences.Any(x => x is AnimationClip))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }
        }

        void OnDragPerform(DragPerformEvent evt)
        {
            if (EditorApplication.isPlaying || !mainWindow.IsEditingAsset())
            {
                return;
            }

            var clips = DragAndDrop.objectReferences.OfType<AnimationClip>().ToList();
            mainWindow.AddClipsToAsset(clips);
            Rebuild();
        }

        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);

            if (evt is KeyDownEvent keyDownEvt)
            {
                if (keyDownEvt.keyCode == KeyCode.Delete && !EditorApplication.isPlaying)
                {
                    DeleteSelection();
                }
            }
        }

        void DeleteSelection()
        {
            mainWindow.DeleteClipInAsset(selectedItem as ProcessingAnimationClip);
            ClearSelection();
            Rebuild();
        }
        
        internal void UpdateSource(IList source)
        {
            itemsSource = source ?? new List<ProcessingAnimationClip>();

            int itemCount = 0;
            if (itemsSource != null)
            {
                itemCount = itemsSource.OfType<ProcessingAnimationClip>().Count();
            }

            if (itemCount > 0)
            {
                selectedIndex = -1;
            }
            Rebuild();
        }

        VisualElement MakeItem()
        {
            var ve = new Label();
            
            Clickable doubleClick = new Clickable(() => OnAnimationSetItemDoubleClick(ve));
            doubleClick.activators.Clear();
            doubleClick.activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, clickCount = 2});
            ve.AddManipulator(doubleClick);

            return ve;
        }
        
        void BindItem(VisualElement ve, int i)
        {
            var clip = itemsSource[i] as ProcessingAnimationClip;
            (ve as Label).text = clip.Name;
            ve.userData = clip;
        }
        
        void OnAnimationSetItemDoubleClick(VisualElement ve)
        {
            if (ve.userData is ProcessingAnimationClip clip)
            {
                EditorGUIUtility.PingObject(clip.sourceAnimClip);
            }
        }
    }
}
