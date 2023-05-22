using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace Momat.Editor
{
    internal class EditorTag
    {
        public int tagID;
        public string tagName;

        public float beginTime;
        public float endTime;

        public Color color;
    }
    
    internal class Timeline
    {
        private AnimationPreProcessWindow mainWindow;
        private VisualElement timelineArea;
        private Label timeLabel;
        private Label frameLabel;
        private DropdownField tagDropdownField;
        
        private Vector2 timelineScrollPos;
        private Vector2 dataScrollPos;

        private float tickSpacing = 7.5f;
        private float timelineZoom = 1f;
        
        private float currTime;
        private int currFrame;
        private bool isDraggingCurrTimeAxis;
        private bool isDraggingTagBoundary;
        private bool isDraggingBegin;
        private int draggingTagIndex;

        private List<EditorTag> editorTags;
        
        private static Texture2D TimelineTexActive;
        private static Texture2D TimelineTexInActive;
        private static Texture2D TimelineTexEvent;
        
        public Timeline(AnimationPreProcessWindow mainWindow, VisualElement timelineArea)
        {
            this.mainWindow = mainWindow;
            this.timelineArea = timelineArea;

            timeLabel = this.timelineArea.Q<Label>("CurrTimeLabel");
            frameLabel = this.timelineArea.Q<Label>("CurrFrameLabel");
            tagDropdownField = this.timelineArea.Q<DropdownField>("TagDropDown");
            
            timelineArea.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            timelineArea.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            timelineArea.RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
            timelineArea.RegisterCallback<WheelEvent>(OnMouseWheel, TrickleDown.TrickleDown);

            var addTagButton = timelineArea.Q<Button>("AddTagButton");
            addTagButton.clickable.clicked += AddTag;

            editorTags = new List<EditorTag>();
        }

        internal void Draw()
        {
            if (mainWindow.currAnimationClip == null || mainWindow.currAnimationClip.sourceAnimClip == null)
            {
                timelineArea.visible = false;
                return;
            }
            
            AnimationClip targetClip = mainWindow.currAnimationClip.sourceAnimClip;
            timelineArea.visible = true;
            
            var areaRect = new Rect(timelineArea.layout.x, timelineArea.layout.y,
                timelineArea.layout.width, timelineArea.layout.height);
            GUILayout.BeginArea(areaRect);

            timelineScrollPos = EditorGUILayout.BeginScrollView(timelineScrollPos);
            
            EditorGUILayout.BeginHorizontal();
            float timelineWidth = targetClip.length * tickSpacing * timelineZoom * 10f;
            GUILayout.Space(Mathf.Max(timelineWidth, areaRect.width));
            EditorGUILayout.EndHorizontal();
            
            var timelineRect = new Rect(0f, 0f, areaRect.width + 1, 20f);
            GUI.DrawTexture(timelineRect, GetTimelineTexInActive());
            
            timelineRect.width = timelineWidth;
            GUI.DrawTexture(timelineRect, GetTimelineTexActive());

            Handles.BeginGUI();
            Handles.color = Color.black;
            var timelineEndPos = timelineRect.x + timelineRect.width;
            Handles.DrawLine(new Vector3(timelineRect.x, timelineRect.height),
                new Vector3(timelineEndPos, timelineRect.height));
            Handles.color = Color.grey;
            Handles.DrawLine(new Vector3(timelineEndPos, 0f),
                new Vector3(timelineEndPos, areaRect.height));
            Handles.EndGUI();

            var eventBarRect = new Rect(0f, 20f, timelineRect.width, 20f);
            GUI.DrawTexture(eventBarRect, GetTimelineTexEvent());
            
            var timeStampStyle = new GUIStyle(EditorStyles.label);
            timeStampStyle.fontSize = 10;
            timeStampStyle.normal.textColor = Color.black;
            
            Handles.color = Color.black;
            var tickRhyme = 10;
            var timeIter = 0;

            float spaceSinceLastStamp = 25f;
            float spaceSinceLastHash = 2f;

            for (float posX = 0; posX < timelineWidth; posX += tickSpacing * timelineZoom)
            {
                Handles.BeginGUI();

                if (tickRhyme == 10)
                {
                    Handles.DrawLine(new Vector3(posX, 18f, 0f), new Vector3(posX, 8f, 0f));
                    tickRhyme = 1;
                    
                    // draw next second label only when space is enough
                    if (spaceSinceLastStamp >= 25f)
                    {
                        EditorGUI.LabelField(new Rect(posX, 0f, 45f, 15f), $"{timeIter}:00", timeStampStyle);
                        spaceSinceLastStamp = 0f;
                    }

                    spaceSinceLastHash = 0f;
                    timeIter++;
                }
                else
                {
                    spaceSinceLastHash += tickSpacing * timelineZoom;
                    spaceSinceLastStamp += tickSpacing * timelineZoom;

                    if (spaceSinceLastHash >= 2f)
                    {
                        Handles.DrawLine(new Vector3(posX, 18f, 0f), new Vector3(posX, 13f, 0f));
                        spaceSinceLastHash = 0f;
                    }

                    tickRhyme++;
                }

                Handles.EndGUI();
            }

            //Draw the time tracking bar
            Handles.BeginGUI();
            Handles.color = Color.white;
            Handles.DrawLine(new Vector3(currTime * tickSpacing * timelineZoom * 10f, 0f, 0f),
                new Vector3(currTime * tickSpacing * timelineZoom * 10f, areaRect.height, 0f));
            Handles.EndGUI();
        
            DrawTags();

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            timeLabel.text = $"Time: {currTime:F2}s";
            frameLabel.text = $"Frame: {currFrame}";
            
        }

        private void DrawTags()
        {
            for (int i = 0; i < editorTags.Count; i++)
            {
                Handles.BeginGUI();
                Handles.color = editorTags[i].color;

                float x1 = editorTags[i].beginTime * tickSpacing * timelineZoom * 10f;
                float x2 = editorTags[i].endTime * tickSpacing * timelineZoom * 10f;
                
                EditorGUI.LabelField(new Rect
                    ((x1 + x2)/2 - 15f, 40f + i * 20, 35f, 15f), $"{editorTags[i].tagName}");
                
                Handles.DrawLine(new Vector3(x1, 40f + i * 20, 0f),
                    new Vector3(x2, 40f + i * 20, 0f));
                
                Handles.color = Color.white;
                Handles.DrawSolidDisc(new Vector3(x1, 40f + i * 20, 0f), Vector3.forward, 5);
                Handles.DrawSolidDisc(new Vector3(x2, 40f + i * 20, 0f), Vector3.forward, 5);
                
                Handles.EndGUI();
            }
        }

        private void AddTag()
        {
            var newTag = new EditorTag
            {
                tagID = tagDropdownField.index,
                tagName = tagDropdownField.value,
                beginTime = 0,
                endTime = mainWindow.currAnimationClip.sourceAnimClip.length,
                color = new Color(Random.Range(0,1f),Random.Range(0,1f),Random.Range(0,1f))
            };
            editorTags.Add(newTag);
        }

        private void OnMouseWheel(WheelEvent evt)
        {
            float zoomDelta = -evt.delta.y * (0.1f * timelineZoom);
            timelineZoom += zoomDelta;
            timelineZoom = Mathf.Clamp(timelineZoom, 0.0001f, float.MaxValue);
            
            mainWindow.Repaint();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            var area = new Rect(timelineArea.layout.x, timelineArea.layout.y + 20,
                timelineArea.layout.width, timelineArea.layout.height);

            if (area.Contains(evt.mousePosition))
            {
                isDraggingCurrTimeAxis = true;
            }
            
            for (int i = 0; i < editorTags.Count; i++)
            {
                float x1 = editorTags[i].beginTime * tickSpacing * timelineZoom * 10f;
                float x2 = editorTags[i].endTime * tickSpacing * timelineZoom * 10f;

                var beginDiscArea = new Rect(timelineArea.layout.x + x1 - 5, timelineArea.layout.y + 55f + i * 20,
                    10, 10);
                var endDiscArea = new Rect(timelineArea.layout.x + x2 - 5, timelineArea.layout.y + 55f + i * 20,
                    10, 10);

                if (beginDiscArea.Contains(evt.mousePosition))
                {
                    isDraggingBegin = true;
                    draggingTagIndex = i;
                    isDraggingTagBoundary = true;
                    return;
                }
                
                if (endDiscArea.Contains(evt.mousePosition))
                {
                    isDraggingBegin = false;
                    draggingTagIndex = i;
                    isDraggingTagBoundary = true;
                    return;
                }
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            isDraggingCurrTimeAxis = false;
            isDraggingTagBoundary = false;
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (isDraggingCurrTimeAxis)
            {
                currTime = (timelineScrollPos.x + evt.mousePosition.x -
                             timelineArea.layout.x) / (tickSpacing * timelineZoom * 10f);

                currTime = Mathf.Clamp
                    (currTime, 0, mainWindow.currAnimationClip.sourceAnimClip.length);
                currFrame = (int)(currTime * mainWindow.currAnimationClip.sourceAnimClip.frameRate);
                
                mainWindow.Repaint();
            }
            
            if (isDraggingTagBoundary)
            {
                 var boundaryTime = (timelineScrollPos.x + evt.mousePosition.x -
                            timelineArea.layout.x) / (tickSpacing * timelineZoom * 10f);

                 if (isDraggingBegin)
                 {
                     boundaryTime = Mathf.Clamp(boundaryTime, 0, editorTags[draggingTagIndex].endTime);
                     editorTags[draggingTagIndex].beginTime = boundaryTime;
                 }
                 else
                 {
                     boundaryTime = Mathf.Clamp(boundaryTime, editorTags[draggingTagIndex].beginTime,
                         mainWindow.currAnimationClip.sourceAnimClip.length);
                     editorTags[draggingTagIndex].endTime = boundaryTime;
                 }
                 
                 mainWindow.Repaint();
            }
        }

        private Texture2D GetTimelineTexActive()
        {
            if (TimelineTexActive == null)
                TimelineTexActive = MakeTex(1, 1, new Color(0.825f, 0.825f, 0.825f, 1f));

            return TimelineTexActive;
        }
        private Texture2D GetTimelineTexInActive()
        {
            if (TimelineTexInActive == null)
                TimelineTexInActive = MakeTex(1, 1, new Color(0.725f, 0.725f, 0.725f, 1f));

            return TimelineTexInActive;
        }
        private Texture2D GetTimelineTexEvent()
        {
            if (TimelineTexEvent == null)
                TimelineTexEvent = MakeTex(1, 1, new Color(0.4f, 0.4f, 0.4f, 1f));

            return TimelineTexEvent;
        }
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
