using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Fireline.Missions
{
    /// <summary>
    /// Scene-local rescue mission: picks a spawn and objective sites, runs each site's steps
    /// (fires, debris, anything deriving from MissionObjectiveStep), then the return to EVAC.
    /// Not a global game/session manager.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class FireRescueMission : MonoBehaviour
    {
        public enum Phase { NotStarted, Objective, Evacuate, Complete, InvalidSetup }
        [SerializeField] private MissionPoint[] spawnPoints = new MissionPoint[0];
        [SerializeField] private MissionObjectiveSite[] objectivePoints = new MissionObjectiveSite[0];
        [SerializeField, Min(1)] private int objectiveCount = 1;
        [SerializeField, Min(0.5f)] private float evacuationRadius = 3f;
        [SerializeField, Min(0)] private float evacuationHoldSeconds = 2f;
        [Tooltip("Zero chooses a fresh random mission. Nonzero is a reproducible test seed.")]
        [SerializeField] private int seed;
        [SerializeField] private UnityEvent onEvacuationReady = new UnityEvent();
        [SerializeField] private UnityEvent onMissionCompleted = new UnityEvent();
        private readonly HashSet<PlayerHealth> placedPlayers = new HashSet<PlayerHealth>();
        private readonly List<ObjectiveSequence> progress = new List<ObjectiveSequence>();
        private readonly List<MissionObjectiveSite> selectedObjectives = new List<MissionObjectiveSite>();
        public IReadOnlyList<MissionObjectiveSite> Objectives => selectedObjectives;
        public int CiviliansSaved => selectedObjectives.Sum(s => s.CiviliansSaved);
        public int CiviliansLost => selectedObjectives.Sum(s => s.CiviliansLost);
        public int CiviliansWaiting => selectedObjectives.Sum(s => s.CiviliansWaiting);
        public int CiviliansPendingRelease => selectedObjectives.Sum(s => s.CiviliansPendingRelease);
        public int CiviliansEscorting => selectedObjectives.Sum(s => s.CiviliansEscorting);
        public string CivilianSummary => $"Civilians: {CiviliansSaved} saved | {CiviliansWaiting} trapped | {CiviliansPendingRelease} emerging | {CiviliansEscorting} escorting | {CiviliansLost} lost";
        public string RescueAlert
        {
            get
            {
                var warnings = new List<string>();
                for (int i = 0; i < selectedObjectives.Count; i++)
                {
                    var site = selectedObjectives[i];
                    if (site.CiviliansInDanger)
                        warnings.Add($"HURRY! Objective {i + 1}: next civilian lost in {Mathf.CeilToInt(site.SecondsUntilLoss)}s");
                }
                return string.Join("\n", warnings);
            }
        }
        public int CompletedObjectives => progress.FindAll(p => p.IsComplete).Count;
        public MissionObjectiveStep GetCurrentStep(MissionObjectiveSite site) => progress.Find(p => p.Site == site)?.Current;
        private float evacuationTime;
        private int reportedLosses;
        private string rescueNotice;
        private float rescueNoticeUntil;
        public string RescueNotice => Time.time < rescueNoticeUntil ? rescueNotice : string.Empty;
        public Phase State { get; private set; }
        public MissionPoint Spawn { get; private set; }
        public MissionObjectiveSite Objective => progress.Find(p => !p.IsComplete)?.Site;
        public MissionObjectiveStep CurrentStep => Objective != null ? GetCurrentStep(Objective) : null;
        public bool ShowEvacuationGuidance => State == Phase.Evacuate;
        public float EvacuationRadius => evacuationRadius;
        public float EvacuationProgress => evacuationHoldSeconds <= 0 ? 1 : Mathf.Clamp01(evacuationTime / evacuationHoldSeconds);
        public string Instruction => State switch
        {
            Phase.Objective => $"Objectives completed: {CompletedObjectives}/{selectedObjectives.Count}\n" + string.Join(" | ", progress.FindAll(p => !p.IsComplete).ConvertAll(p => p.Current != null ? p.Current.Instruction : "Reach the objective")),
            Phase.Evacuate => "Escort civilians to EVAC, then return together. Protect the slower survivors!",
            Phase.Complete => CiviliansSaved > 0 ? "Mission complete - team evacuated" : "Team evacuated - no civilians saved",
            Phase.InvalidSetup => "Mission unavailable: no valid spawn/objective configuration",
            _ => "Preparing mission"
        };

        private void Start() => BeginMission();

        public bool BeginMission()
        {
            Unsubscribe();
            placedPlayers.Clear();
            evacuationTime = 0;
            reportedLosses = 0;
            rescueNotice = string.Empty;
            rescueNoticeUntil = 0;
            Spawn = null;
            progress.Clear();
            selectedObjectives.Clear();
            if (objectivePoints != null)
                foreach (var site in objectivePoints) if (site != null) site.Prepare();
            var candidates = new List<(MissionPoint spawn, List<MissionObjectiveSite> sites)>();
            int count = Mathf.Max(1, objectiveCount);
            if (spawnPoints != null && objectivePoints != null)
                foreach (var spawn in spawnPoints)
                {
                    if (spawn == null || !spawn.isActiveAndEnabled || spawn.gameObject.scene != gameObject.scene) continue;
                    var sites = new List<MissionObjectiveSite>();
                    foreach (var site in objectivePoints)
                        if (site != null && site.isActiveAndEnabled && site.gameObject.scene == gameObject.scene
                            && site.IsConfigured && MissionPoint.CanPair(spawn, site.Point) && !sites.Contains(site))
                            sites.Add(site);
                    if (sites.Count >= count) candidates.Add((spawn, sites));
                }
            if (candidates.Count == 0)
            {
                State = Phase.InvalidSetup;
                Debug.LogError("Fire rescue mission has no valid spawn/objective pairs. Check step references and exclusion circles.", this);
                return false;
            }
            var random = seed == 0 ? null : new System.Random(seed);
            int Pick(int max) => random == null ? Random.Range(0, max) : random.Next(max);
            var candidate = candidates[Pick(candidates.Count)];
            Spawn = candidate.spawn;
            for (int i = 0; i < count; i++)
            {
                int choice = Pick(candidate.sites.Count);
                var site = candidate.sites[choice];
                candidate.sites.RemoveAt(choice);
                selectedObjectives.Add(site);
                progress.Add(new ObjectiveSequence(site, OnSiteCleared));
            }
            State = Phase.Objective;
            PlaceNewPlayers();
            foreach (var entry in progress) entry.Site.BeginRescue(this);
            foreach (var entry in progress) entry.Start();
            return true;
        }

        private void OnSiteCleared(ObjectiveSequence entry)
        {
            entry.Site.ReleaseCivilians();
            rescueNotice = $"Objective {selectedObjectives.IndexOf(entry.Site) + 1} complete: {entry.Site.CiviliansPendingRelease} civilians emerging. Escort them to EVAC!";
            rescueNoticeUntil = Time.time + 6f;
            if (progress.TrueForAll(p => p.IsComplete) && State == Phase.Objective)
            {
                State = Phase.Evacuate;
                onEvacuationReady.Invoke();
            }
        }
        private void Unsubscribe()
        {
            foreach (var entry in progress) entry.Stop();
        }
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (State != Phase.Objective && State != Phase.Evacuate) return;
            PlaceNewPlayers();
            if (State == Phase.Objective)
            {
                if (!PlayerHealth.ActivePlayers.Any(p => p != null && p.gameObject.scene == gameObject.scene)) return;
                foreach (var entry in progress)
                    if (!entry.IsComplete) entry.Site.AdvanceRescue(Time.deltaTime);
                ReportCivilianLosses();
                return;
            }
            ReportCivilianLosses();
            bool any = false, together = CiviliansPendingRelease == 0 && CiviliansEscorting == 0;
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

        private void ReportCivilianLosses()
        {
            if (CiviliansLost <= reportedLosses) return;
            rescueNotice = $"{CiviliansLost - reportedLosses} civilian(s) died! Protect the remaining survivors.";
            rescueNoticeUntil = Time.time + 6f;
            reportedLosses = CiviliansLost;
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