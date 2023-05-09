using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace Momat.Editor
{
    internal class Timeline
    {
        private AnimationPreProcessWindow mainWindow;
        private VisualElement timelineArea;
        private Label timeLabel;
        private Label frameLabel;
        
        private Vector2 timelineScrollPos;
        private Vector2 dataScrollPos;

        private float tickSpacing = 7.5f;
        private float timelineZoom = 1f;
        private float currTime;
        private int currFrame;
        private bool isDraggingCurrTimeAxis;
        
        private static Texture2D TimelineTexActive;
        private static Texture2D TimelineTexInActive;
        private static Texture2D TimelineTexEvent;
        
        public Timeline(AnimationPreProcessWindow mainWindow, VisualElement timelineArea)
        {
            this.mainWindow = mainWindow;
            this.timelineArea = timelineArea;

            timeLabel = this.timelineArea.Q<Label>("CurrTimeLabel");
            frameLabel = this.timelineArea.Q<Label>("CurrFrameLabel");
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

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            timeLabel.text = $"Time: {currTime:F2}s";
            frameLabel.text = $"Frame: {currFrame}";
        }

        internal void OnMouseWheel(WheelEvent evt)
        {
            float zoomDelta = -evt.delta.y * (0.1f * timelineZoom);
            timelineZoom += zoomDelta;
            timelineZoom = Mathf.Clamp(timelineZoom, 0.0001f, float.MaxValue);
            
            mainWindow.Repaint();
        }

        internal void OnMouseDown(MouseDownEvent evt)
        {
            var area = new Rect(timelineArea.layout.x, timelineArea.layout.y,
                timelineArea.layout.width, 40);

            if (area.Contains(evt.mousePosition))
            {
                isDraggingCurrTimeAxis = true;
            }
        }

        internal void OnMouseUp(MouseUpEvent evt)
        {
            isDraggingCurrTimeAxis = false;
        }

        internal void OnMouseMove(MouseMoveEvent evt)
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
        }

        public Texture2D GetTimelineTexActive()
        {
            if (TimelineTexActive == null)
                TimelineTexActive = MakeTex(1, 1, new Color(0.825f, 0.825f, 0.825f, 1f));

            return TimelineTexActive;
        }
        public Texture2D GetTimelineTexInActive()
        {
            if (TimelineTexInActive == null)
                TimelineTexInActive = MakeTex(1, 1, new Color(0.725f, 0.725f, 0.725f, 1f));

            return TimelineTexInActive;
        }
        public Texture2D GetTimelineTexEvent()
        {
            if (TimelineTexEvent == null)
                TimelineTexEvent = MakeTex(1, 1, new Color(0.4f, 0.4f, 0.4f, 1f));

            return TimelineTexEvent;
        }
        public Texture2D MakeTex(int width, int height, Color col)
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
