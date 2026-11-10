using System;
using UnityEngine;

namespace Fireline.Missions
{
    public abstract class MissionObjectiveStep : MonoBehaviour
    {
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public event Action Completed;
        public abstract bool IsConfigured { get; }
        public abstract string Instruction { get; }

        public void BeginStep()
        {
            if (IsRunning || IsComplete || !IsConfigured) return;
            IsRunning = true;
            OnBegin();
        }

        public void ResetStep()
        {
            OnEnd();
            IsRunning = false;
            IsComplete = false;
        }
        
        public virtual void SetAvailable(bool available) => ResetStep();

        protected void CompleteStep()
        {
            if (!IsRunning || IsComplete) return;
            IsRunning = false;
            IsComplete = true;
            OnEnd();
            Completed?.Invoke();
        }

        protected abstract void OnBegin();
        protected abstract void OnEnd();
        protected virtual void OnDisable() { if (IsRunning) ResetStep(); }
    }
}