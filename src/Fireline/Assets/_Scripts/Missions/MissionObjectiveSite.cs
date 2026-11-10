using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

namespace Fireline.Missions
{
    [RequireComponent(typeof(MissionPoint))]
    public sealed class MissionObjectiveSite : MonoBehaviour
    {
        [SerializeField] private MissionObjectiveStep[] steps = new MissionObjectiveStep[0];
        [Header("Civilian rescue")]
        [Tooltip("Civilians released when every step at this building is complete.")]
        [SerializeField, Min(1)] private int civilianCount = 3;
        [Tooltip("Seconds with players in the mission until this building loses its first civilian.")]
        [SerializeField, Min(1)] private float firstLossSeconds = 120f;
        [Tooltip("Seconds between subsequent civilian losses at this building.")]
        [SerializeField, Min(1)] private float lossIntervalSeconds = 30f;
        [Tooltip("Warn the team this many seconds before each loss.")]
        [SerializeField, Min(0)] private float warningSeconds = 20f;
        [Tooltip("Seconds between survivors emerging after the objective is cleared.")]
        [SerializeField, Min(0.1f)] private float releaseIntervalSeconds = 1f;
        [Tooltip("Player-like body art for the smaller civilian visual.")]
        [SerializeField] private Sprite civilianSprite;
        private readonly List<CivilianEscort> civilians = new List<CivilianEscort>();
        private FireRescueMission mission;
        private float releaseTimer;
        private bool releasing;
        public IReadOnlyList<CivilianEscort> Civilians => civilians;
        public int CiviliansPendingRelease { get; private set; }
        public int CiviliansEscorting { get; private set; }
        private float rescueElapsed;
        private bool rescueActive;
        public int CiviliansWaiting { get; private set; }
        public int CiviliansSaved { get; private set; }
        public int CiviliansLost { get; private set; }
        public float SecondsUntilLoss => rescueActive && CiviliansWaiting > 0
            ? Mathf.Max(0, Mathf.Max(1, firstLossSeconds) + CiviliansLost * Mathf.Max(1, lossIntervalSeconds) - rescueElapsed) : 0;
        public bool CiviliansInDanger => rescueActive && CiviliansWaiting > 0 && SecondsUntilLoss <= warningSeconds;

        public void BeginRescue(FireRescueMission owner = null)
        {
            ClearCivilians();
            mission = owner;
            rescueElapsed = 0;
            rescueActive = true;
            CiviliansWaiting = Mathf.Max(1, civilianCount);
            CiviliansSaved = CiviliansLost = 0;
        }

        // Driven by the mission so inactive candidates and completed missions never accrue losses.
        public void AdvanceRescue(float seconds)
        {
            if (!rescueActive || CiviliansWaiting == 0 || seconds <= 0) return;
            rescueElapsed += seconds;
            while (CiviliansWaiting > 0 && SecondsUntilLoss <= 0)
            {
                CiviliansWaiting--;
                CiviliansLost++;
            }
        }

        public void ReleaseCivilians()
        {
            if (!rescueActive) return;
            rescueActive = false;
            CiviliansPendingRelease = CiviliansWaiting;
            CiviliansWaiting = 0;
            releasing = true;
            releaseTimer = 0;
        }

        private void Update()
        {
            if (!releasing || mission == null || !mission.isActiveAndEnabled) return;
            if (CiviliansPendingRelease <= 0) { releasing = false; return; }
            releaseTimer -= Time.deltaTime;
            if (releaseTimer > 0) return;
            Vector3 exit = transform.position + Vector3.down * 1.5f;
            Vector3 position = exit;
            bool space = CivilianEscort.CanSpawnAt(mission, position);
            // Keep emerging civilians apart even when the group is waiting at the exit.
            for (int ring = 1; !space && ring <= 3; ring++)
                for (int slot = 0; !space && slot < 8; slot++)
                {
                    float angle = slot * Mathf.PI / 4;
                    position = exit + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0)
                        * (ring * (CivilianEscort.MinimumSpacing + 0.1f));
                    // Do not spawn on the other side of a wall beside the exit.
                    space = !Physics2D.Linecast(exit, position, LayerMask.GetMask(Structure.ObstacleLayer))
                        && CivilianEscort.CanSpawnAt(mission, position);
                }
            if (!space) { releaseTimer = 0.1f; return; }
            // At most one per frame: a hitch must not spawn the whole crowd at once.
            releaseTimer = Mathf.Max(0.1f, releaseIntervalSeconds);
            var go = new GameObject($"Civilian {civilians.Count + 1}");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var civilian = go.AddComponent<CivilianEscort>();
            civilian.Initialize(this, mission, civilianSprite);
            civilians.Add(civilian);
            CiviliansPendingRelease--;
            CiviliansEscorting++;
        }

        internal void ResolveCivilian(bool saved)
        {
            CiviliansEscorting--;
            if (saved) CiviliansSaved++;
            else CiviliansLost++;
        }

        private void ClearCivilians()
        {
            releasing = false;
            CiviliansPendingRelease = CiviliansEscorting = 0;
            foreach (var civilian in civilians)
                if (civilian != null) { civilian.gameObject.SetActive(false); Destroy(civilian.gameObject); }
            civilians.Clear();
        }

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
            rescueActive = false;
            rescueElapsed = 0;
            CiviliansWaiting = CiviliansSaved = CiviliansLost = 0;
            ClearCivilians();
            foreach (var step in steps)
            {
                if (step != null) step.SetAvailable(false);
            }
        }
    }
}