using UnityEngine;

namespace Fireline.Missions
{
    [RequireComponent(typeof(MissionPoint))]
    public sealed class MissionObjectiveSite : MonoBehaviour
    {
        [SerializeField] private MissionObjectiveStep[] steps = new MissionObjectiveStep[0];
        public MissionPoint Point => GetComponent<MissionPoint>();
        public MissionObjectiveStep[] Steps => steps;
        public bool IsConfigured
        {
            get
            {
                if (steps == null || steps.Length == 0) return false;
                for (int i = 0; i < steps.Length; i++)
                {
                    if (steps[i] == null || !steps[i].IsConfigured) return false;
                    for (int j = 0; j < i; j++) if (steps[i] == steps[j]) return false;
                }
                return true;
            }
        }
        public void Prepare()
        {
            foreach (var step in steps)
            {
                if (step == null) continue;
                step.ResetStep();
                if (step is FireObjectiveStep fire) fire.SetAvailable(false);
            }
        }
    }
}
