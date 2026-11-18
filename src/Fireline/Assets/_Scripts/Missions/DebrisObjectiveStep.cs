using System.Collections.Generic;
using UnityEngine;

namespace Fireline.Missions
{
    /// <summary>
    /// Break every listed piece of debris, e.g. a pile blocking the door civilians are trapped
    /// behind. Debris is rebuilt when the step begins, and hidden at sites the mission didn't pick.
    /// </summary>
    public sealed class DebrisObjectiveStep : TargetObjectiveStep<Debris>
    {
        [Tooltip("Only these pieces count. Use debris placed for this objective, not general level clutter.")]
        [SerializeField] private Debris[] debris = new Debris[0];

        public IReadOnlyList<Debris> Pieces => debris;
        public override string DisplayName => "Break crates";
        protected override string ChecklistLabel => "Crate";
        protected override char ChecklistSymbol => 'D';
        public override string Instruction => $"Break the marked debris: {Remaining} remaining";

        protected override IReadOnlyList<Debris> Targets => debris;
        protected override bool IsResolved(Debris piece) => piece.IsBroken;
        protected override void Arm(Debris piece) => piece.Restore();
        protected override void Watch(Debris piece) => piece.Broken += OnBroken;
        protected override void Unwatch(Debris piece) => piece.Broken -= OnBroken;
        protected override void SetTargetAvailable(Debris piece, bool available) => piece.SetPresent(available);
        protected override string MarkerLabel => "DEBRIS";
        protected override Color MarkerColor => MinimapSymbols.Crate;

        private void OnBroken(Debris _) => CheckProgress();
    }
}
