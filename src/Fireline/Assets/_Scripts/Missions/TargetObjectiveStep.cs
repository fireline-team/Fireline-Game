using System.Collections.Generic;
using UnityEngine;

namespace Fireline.Missions
{
    /// <summary>
    /// A step that completes once every one of a fixed set of targets is resolved: each fire
    /// extinguished, each piece of debris broken, and so on.
    ///
    /// Subclasses keep their own serialized target array (so field names stay friendly in the
    /// Inspector, e.g. "fires") and only describe how one target behaves. The tracking,
    /// configuration check, availability and HUD markers all live here.
    /// </summary>
    public abstract class TargetObjectiveStep<TTarget> : MissionObjectiveStep, IObjectiveMarkerSource
        where TTarget : Component
    {
        private readonly HashSet<TTarget> remaining = new HashSet<TTarget>();

        /// <summary>The targets this step needs, as set in the Inspector. May contain duplicates.</summary>
        protected abstract IReadOnlyList<TTarget> Targets { get; }
        /// <summary>Is this target dealt with (extinguished, broken...)?</summary>
        protected abstract bool IsResolved(TTarget target);
        /// <summary>Puts the target in its unresolved state when the step begins (ignite, rebuild...).</summary>
        protected abstract void Arm(TTarget target);
        /// <summary>Start calling <see cref="CheckProgress"/> when this target changes.</summary>
        protected abstract void Watch(TTarget target);
        /// <summary>Stop calling <see cref="CheckProgress"/> for this target.</summary>
        protected abstract void Unwatch(TTarget target);
        /// <summary>Shows or hides a target for missions that don't use this site.</summary>
        protected virtual void SetTargetAvailable(TTarget target, bool available) => target.gameObject.SetActive(available);

        /// <summary>Marker text before the number, e.g. "FIRE" gives "FIRE 1".</summary>
        protected abstract string MarkerLabel { get; }
        protected abstract Color MarkerColor { get; }

        public int Remaining => remaining.Count;

        public override bool IsConfigured
        {
            get
            {
                var targets = Targets;
                if (targets == null || targets.Count == 0) return false;
                for (int i = 0; i < targets.Count; i++) if (targets[i] == null) return false;
                return true;
            }
        }

        protected override void OnBegin()
        {
            remaining.Clear();
            foreach (var target in Targets)
            {
                SetTargetAvailable(target, true);
                Arm(target);
                if (remaining.Add(target)) Watch(target);
            }
            CheckProgress();
        }

        /// <summary>Call from a target's event. Completes the step once every target is resolved.</summary>
        protected void CheckProgress()
        {
            if (!IsRunning) return;
            // A destroyed/missing target is not success. Configuration must be repaired.
            foreach (var target in Targets)
                if (target != null && IsResolved(target)) remaining.Remove(target);
            if (remaining.Count == 0) CompleteStep();
        }

        protected override void OnEnd()
        {
            var targets = Targets;
            if (targets != null)
                foreach (var target in targets)
                    if (target != null) Unwatch(target);
            remaining.Clear();
        }

        public override void SetAvailable(bool available)
        {
            base.SetAvailable(available);
            var targets = Targets;
            if (targets == null) return;
            foreach (var target in targets)
                if (target != null) SetTargetAvailable(target, available);
        }

        public void CollectMarkers(List<ObjectiveMarker> markers)
        {
            if (!IsRunning) return;
            var targets = Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target != null && !IsResolved(target))
                    markers.Add(new ObjectiveMarker(target.transform.position, $"{MarkerLabel} {i + 1}", MarkerColor));
            }
        }
    }
}
