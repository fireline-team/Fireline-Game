using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Fireline.Missions
{
    /// <summary>Scene-local fire objective and return loop, not a global game/session manager.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class FireRescueMission : MonoBehaviour
    {
        public enum Phase { NotStarted, Objective, Evacuate, Complete, InvalidSetup }
        [SerializeField] private MissionPoint[] spawnPoints = new MissionPoint[0];
        [SerializeField] private MissionObjectiveSite[] objectivePoints = new MissionObjectiveSite[0];
        [SerializeField, Min(0.5f)] private float evacuationRadius = 3f;
        [SerializeField, Min(0)] private float evacuationHoldSeconds = 2f;
        [Tooltip("Zero chooses a fresh random mission. Nonzero is a reproducible test seed.")]
        [SerializeField] private int seed;
        [SerializeField] private UnityEvent onEvacuationReady = new UnityEvent();
        [SerializeField] private UnityEvent onMissionCompleted = new UnityEvent();
        private readonly HashSet<PlayerHealth> placedPlayers = new HashSet<PlayerHealth>();
        private MissionObjectiveStep currentStep;
        private int stepIndex;
        private float evacuationTime;
        public Phase State { get; private set; }
        public MissionPoint Spawn { get; private set; }
        public MissionObjectiveSite Objective { get; private set; }
        public MissionObjectiveStep CurrentStep => currentStep;
        public float EvacuationRadius => evacuationRadius;
        public float EvacuationProgress => evacuationHoldSeconds <= 0 ? 1 : Mathf.Clamp01(evacuationTime / evacuationHoldSeconds);
        public string Instruction => State switch
        {
            Phase.Objective => currentStep != null ? currentStep.Instruction : "Reach the objective",
            Phase.Evacuate => "Return together to EVAC. Revive downed teammates before leaving.",
            Phase.Complete => "Mission complete - team evacuated",
            Phase.InvalidSetup => "Mission unavailable: no valid spawn/objective configuration",
            _ => "Preparing mission"
        };

        private void Start() => BeginMission();

        public bool BeginMission()
        {
            Unsubscribe();
            placedPlayers.Clear();
            evacuationTime = 0;
            Spawn = null;
            Objective = null;
            if (objectivePoints != null)
                foreach (var site in objectivePoints) if (site != null) site.Prepare();
            var pairs = new List<(MissionPoint spawn, MissionObjectiveSite site)>();
            if (spawnPoints != null && objectivePoints != null)
                foreach (var spawn in spawnPoints)
                    foreach (var site in objectivePoints)
                        if (spawn != null && spawn.isActiveAndEnabled && site != null && site.isActiveAndEnabled
                            && spawn.gameObject.scene == gameObject.scene && site.gameObject.scene == gameObject.scene
                            && site.IsConfigured && MissionPoint.CanPair(spawn, site.Point))
                            pairs.Add((spawn, site));
            if (pairs.Count == 0)
            {
                State = Phase.InvalidSetup;
                Debug.LogError("Fire rescue mission has no valid spawn/objective pairs. Check step references and exclusion circles.", this);
                return false;
            }
            int choice = seed == 0 ? Random.Range(0, pairs.Count) : new System.Random(seed).Next(pairs.Count);
            Spawn = pairs[choice].spawn;
            Objective = pairs[choice].site;
            State = Phase.Objective;
            stepIndex = 0;
            PlaceNewPlayers();
            BeginNextStep();
            return true;
        }

        private void BeginNextStep()
        {
            Unsubscribe();
            if (stepIndex >= Objective.Steps.Length)
            {
                State = Phase.Evacuate;
                onEvacuationReady.Invoke();
                return;
            }
            currentStep = Objective.Steps[stepIndex];
            currentStep.Completed += OnStepCompleted;
            currentStep.BeginStep();
        }
        private void OnStepCompleted() { stepIndex++; BeginNextStep(); }
        private void Unsubscribe()
        {
            if (currentStep != null) currentStep.Completed -= OnStepCompleted;
            currentStep = null;
        }
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (State != Phase.Objective && State != Phase.Evacuate) return;
            PlaceNewPlayers();
            if (State != Phase.Evacuate) return;
            bool any = false, together = true;
            foreach (var player in PlayerHealth.ActivePlayers)
            {
                if (player == null || player.gameObject.scene != gameObject.scene) continue;
                any = true;
                if (player.IsDowned || Vector2.Distance(player.transform.position, Spawn.transform.position) > evacuationRadius)
                    together = false;
            }
            evacuationTime = any && together ? evacuationTime + Time.deltaTime : 0;
            if (any && together && evacuationTime >= evacuationHoldSeconds)
            {
                State = Phase.Complete;
                onMissionCompleted.Invoke();
            }
        }

        private void PlaceNewPlayers()
        {
            foreach (var player in PlayerHealth.ActivePlayers)
            {
                if (player == null || player.gameObject.scene != gameObject.scene || !placedPlayers.Add(player)) continue;
                int index = player.GetComponent<PlayerInput>().playerIndex;
                float angle = Mathf.Max(0, index) * Mathf.PI * 0.5f;
                Vector3 position = Spawn.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.8f;
                player.transform.position = position;
                var body = player.GetComponent<Rigidbody2D>();
                body.position = position;
                body.linearVelocity = Vector2.zero;
                // Existing rules will connect fresh players after placement. Refresh an existing connection on restart.
                var connection = player.GetComponent<PlayerHoseConnection>();
                if (connection != null)
                {
                    connection.Detach();
                    connection.Attach(HoseRopeSource.FindNearest(player.gameObject));
                }
            }
        }
    }
}
