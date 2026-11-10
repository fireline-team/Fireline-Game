using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

namespace Fireline.Missions
{
    public sealed class FireObjectiveStep : TargetObjectiveStep<FireZone>
    {
        [Tooltip("Only these fires count. Use dedicated mission fires, not shared ambient hazards.")]
        [SerializeField] private FireZone[] fires = new FireZone[0];

        public IReadOnlyList<FireZone> Fires => fires;
        public override string Instruction => $"Extinguish marked fires: {Remaining} remaining";

        protected override IReadOnlyList<FireZone> Targets => fires;
        protected override bool IsResolved(FireZone fire) => !fire.IsBurning;
        protected override void Arm(FireZone fire) => fire.Ignite();
        protected override void Watch(FireZone fire) => fire.OnExtinguished.AddListener(CheckProgress);
        protected override void Unwatch(FireZone fire) => fire.OnExtinguished.RemoveListener(CheckProgress);
        protected override string MarkerLabel => "FIRE";
        protected override Color MarkerColor => new Color(1f, 0.65f, 0.15f);
    }
}