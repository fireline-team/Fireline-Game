using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

/// <summary>Gameplay overlap and a simple stream preview. Particles never decide damage.</summary>
public class HoseWeapon : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damagePerSecond = 50f;
    [SerializeField, Min(0.1f)] private float range = 5f;
    [SerializeField, Min(0.01f)] private float width = 0.6f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Material streamMaterial;

    private readonly List<Collider2D> hits = new List<Collider2D>(64);
    private readonly HashSet<HordeEnemy> damaged = new HashSet<HordeEnemy>();
    private LineRenderer stream;
    private PlayerHealth health;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        GameObject visual = new GameObject("Hose stream");
        visual.transform.SetParent(transform, false);
        stream = visual.AddComponent<LineRenderer>();
        stream.sharedMaterial = streamMaterial;
        stream.positionCount = 2;
        stream.useWorldSpace = true;
        stream.startWidth = width;
        stream.endWidth = width;
        stream.startColor = stream.endColor = new Color(0.25f, 0.8f, 1f, 0.7f);
        stream.sortingOrder = 2;
        stream.enabled = false;
    }

    public void StopSpraying()
    {
        if (stream != null) stream.enabled = false;
    }

    public void Spray(Vector2 direction, float deltaTime)
    {
        if (!isActiveAndEnabled || (health != null && health.IsDead) || direction.sqrMagnitude < 0.001f)
        {
            StopSpraying();
            return;
        }
        direction.Normalize();
        Vector2 origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
        Vector2 end = origin + direction * range;
        stream.enabled = true;
        stream.SetPosition(0, new Vector3(origin.x, origin.y, transform.position.z));
        stream.SetPosition(1, new Vector3(end.x, end.y, transform.position.z));

        // Horde movement writes Transforms directly, so queries need current physics poses.
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(hitLayers);
        Physics2D.OverlapBox((origin + end) * 0.5f, new Vector2(range, width),
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, filter, hits);
        damaged.Clear();
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            HordeEnemy enemy = hit.GetComponentInParent<HordeEnemy>();
            if (enemy != null && enemy.isActiveAndEnabled && damaged.Add(enemy))
                enemy.TakeDamage(damagePerSecond * deltaTime);
        }
    }

    private void OnDisable() => StopSpraying();
}
