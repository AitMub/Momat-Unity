using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Momat.Runtime
{
    public class Clock
    {
        private bool paused = false;

        private float elapsedTime = 0f;
        private float timeStamp = -1f;

        public float CurrentTime => elapsedTime;
        public float TimeFromLastTimeStamp =>
            timeStamp >= 0f ? elapsedTime - timeStamp : -1f; 
        
        public void Tick(float deltaTime)
        {
            if (paused == false)
            {
                elapsedTime += deltaTime;
            }
        }

        public void SetTime(float time)
        {
            elapsedTime = time;
            timeStamp = -1;
        }

        public void SetTimeStamp()
        {
            timeStamp = CurrentTime;
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            paused = false;
        }
    }
}

