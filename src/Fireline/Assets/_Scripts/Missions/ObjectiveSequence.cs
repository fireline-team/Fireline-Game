using System;

namespace Fireline.Missions
{
    /// <summary>
    /// Runs one site's steps in order: begins a step, waits for it to complete, moves to the next,
    /// and reports when the last one is done. Knows nothing about civilians, evac or the HUD;
    /// the mission decides what finishing a site means.
    /// </summary>
    public sealed class ObjectiveSequence
    {
        private readonly Action<ObjectiveSequence> finished;
        private int index;

        public MissionObjectiveSite Site { get; }
        public MissionObjectiveStep Current { get; private set; }
        public bool IsComplete => index >= Site.Steps.Length;

        public ObjectiveSequence(MissionObjectiveSite site, Action<ObjectiveSequence> finished)
        {
            Site = site;
            this.finished = finished;
        }

        public void Start() => Advance();

        /// <summary>Stops listening to the current step without touching its state.</summary>
        public void Stop() => Detach();

        private void Advance()
        {
            Detach();
            if (IsComplete)
            {
                finished?.Invoke(this);
                return;
            }
            Current = Site.Steps[index];
            Current.Completed += OnStepCompleted;
            Current.BeginStep();
        }

        private void OnStepCompleted()
        {
            index++;
            Advance();
        }

        private void Detach()
        {
            if (Current != null) Current.Completed -= OnStepCompleted;
            Current = null;
        }
    }
}
