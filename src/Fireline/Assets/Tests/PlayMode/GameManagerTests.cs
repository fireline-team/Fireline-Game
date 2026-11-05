#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Fireline.Missions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GameManagerTests
{
    private GameManager manager;

    [UnitySetUp]
    public IEnumerator OpenMenu()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameManager.MenuScene,
            new LoadSceneParameters(LoadSceneMode.Single));
        manager = GameManager.Instance;
        Assert.That(manager, Is.Not.Null);
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.MainMenu));
    }

    private IEnumerator StartMission()
    {
        manager.OpenMission();
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.MissionBriefing));
        manager.StartMission();
        for (int i = 0; i < 600 && manager.State == GameManager.SessionState.Loading; i++) yield return null;
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.Playing));
    }

    [UnityTest]
    public IEnumerator PauseWipeResultsAndReplay()
    {
        yield return StartMission();
        yield return null;
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.Playing), "No joined players is not defeat.");
        HitStop.Freeze(0.01f);
        manager.TogglePause();
        yield return new WaitForSecondsRealtime(0.05f);
        Assert.That(Time.timeScale, Is.Zero, "Hit-stop must not resume a paused game.");
        Assert.That(GameManager.BlocksGameplay, Is.True);
        manager.TogglePause();
        Assert.That(Time.timeScale, Is.EqualTo(1));
        var first = new GameObject("First firefighter").AddComponent<PlayerHealth>();
        var second = new GameObject("Second firefighter").AddComponent<PlayerHealth>();
        first.TakeDamage(10000);
        yield return null;
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.Playing));
        second.TakeDamage(10000);
        yield return null;
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.Results));
        Assert.That(manager.Result, Is.EqualTo(GameManager.Outcome.TeamLost));
        Assert.That(first.CanResetScene, Is.False);
        Assert.That(Time.timeScale, Is.Zero);
        manager.ReturnToMainMenu();
        for (int i = 0; i < 600 && manager.State == GameManager.SessionState.Loading; i++) yield return null;
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.MainMenu));
        Assert.That(Time.timeScale, Is.EqualTo(1));
        yield return StartMission();
        Assert.That(PlayerHealth.ActivePlayers.Count, Is.Zero);
        Assert.That(manager.CompletedObjectives, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ObjectivesRequireExtractionBeforeResults()
    {
        yield return StartMission();
        var mission = Object.FindAnyObjectByType<FireRescueMission>();
        var player = new GameObject("Extracting firefighter").AddComponent<PlayerHealth>();
        player.GetComponent<Rigidbody2D>().gravityScale = 0;
        yield return null;
        player.transform.position = mission.Spawn.transform.position + Vector3.right * 10;
        foreach (var site in mission.Objectives)
            foreach (var fire in ((FireObjectiveStep)mission.GetCurrentStep(site)).Fires.ToArray()) fire.ApplyWater(10000);
        yield return null;
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.Playing));
        // Resolve the escort using the real civilian update and evacuation zone.
        float releaseDeadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < releaseDeadline && (mission.CiviliansPendingRelease > 0 || mission.CiviliansEscorting > 0))
        {
            foreach (var site in mission.Objectives)
                foreach (var civilian in site.Civilians)
                    if (civilian != null) civilian.transform.position = mission.Spawn.transform.position;
            yield return null;
        }
        Assert.That(mission.CiviliansPendingRelease + mission.CiviliansEscorting, Is.Zero);
        player.transform.position = mission.Spawn.transform.position;
        float extractionDeadline = Time.realtimeSinceStartup + 10f;
        while (manager.State == GameManager.SessionState.Playing && Time.realtimeSinceStartup < extractionDeadline)
            yield return null;
        Assert.That(manager.State, Is.EqualTo(GameManager.SessionState.Results));
        Assert.That(manager.Result, Is.EqualTo(GameManager.Outcome.Extracted));
        Assert.That(manager.CompletedObjectives, Is.EqualTo(manager.TotalObjectives));
        Assert.That(manager.CiviliansSaved, Is.EqualTo(3));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (manager != null) Object.Destroy(manager.gameObject);
        yield return null;
        var old = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("After game manager test"));
        yield return SceneManager.UnloadSceneAsync(old);
        Time.timeScale = 1;
    }
}
#endif
