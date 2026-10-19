using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

namespace Fireline.Missions
{
    public sealed class FireObjectiveStep : MissionObjectiveStep
    {
        [Tooltip("Only these fires count. Use dedicated mission fires, not shared ambient hazards.")]
        [SerializeField] private FireZone[] fires = new FireZone[0];
        private readonly HashSet<FireZone> remaining = new HashSet<FireZone>();
        public IReadOnlyList<FireZone> Fires => fires;
        public int Remaining => remaining.Count;
        public override string Instruction => $"Extinguish marked fires: {Remaining} remaining";
        public override bool IsConfigured
        {
            get
            {
                if (fires == null || fires.Length == 0) return false;
                foreach (var fire in fires) if (fire == null) return false;
                return true;
            }
        }

        protected override void OnBegin()
        {
            remaining.Clear();
            foreach (var fire in fires)
            {
                fire.gameObject.SetActive(true);
                fire.Ignite();
                if (remaining.Add(fire)) fire.OnExtinguished.AddListener(CheckProgress);
            }
            CheckProgress();
        }

        private void CheckProgress()
        {
            if (!IsRunning) return;
            // A destroyed/missing target is not success. Configuration must be repaired.
            foreach (var fire in fires)
                if (fire != null && !fire.IsBurning) remaining.Remove(fire);
            if (remaining.Count == 0) CompleteStep();
        }

        protected override void OnEnd()
        {
            if (fires != null)
                foreach (var fire in fires)
                    if (fire != null) fire.OnExtinguished.RemoveListener(CheckProgress);
            remaining.Clear();
        }

        public void SetAvailable(bool available)
        {
            ResetStep();
            foreach (var fire in fires) if (fire != null) fire.gameObject.SetActive(available);
        }
    }
}
