using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

/// <summary>
/// To make a new nozzle: inherit from this, then override OnSpray.
/// </summary>
public abstract class HoseWeapon : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damagePerSecond = 50f;
    [SerializeField, Min(0f)] private float extinguishPerSecond = 30f;
    [SerializeField, Min(0.1f)] private float range = 5f;
    [Tooltip("How wide the stream is, across the aim direction.")]
    [SerializeField, Min(0.01f)] private float width = 0.6f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private Transform muzzle;

    [Header("Stream Visual")]
    [SerializeField] private Material streamMaterial;
    [SerializeField] private Color streamColor = new Color(0.25f, 0.8f, 1f, 0.7f);

    private readonly List<Collider2D> _colliderHits = new List<Collider2D>(64);
    private readonly HashSet<HordeEnemy> _seen = new HashSet<HordeEnemy>();
    private readonly List<HordeEnemy> _enemyHits = new List<HordeEnemy>(64);
    private LineRenderer _stream;
    private PlayerHealth _health;

    private readonly List<Collider2D> hits = new List<Collider2D>(64);
    private readonly HashSet<HordeEnemy> damaged = new HashSet<HordeEnemy>();
    private readonly HashSet<FireZone> watered = new HashSet<FireZone>();
    private LineRenderer stream;
    private PlayerHealth health;

    protected Vector2 AimDirection { get; private set; } = Vector2.right;
    protected Vector2 Origin { get; private set; }
    protected Vector2 End { get; private set; }

    protected virtual void Awake()
    {
        _health = GetComponent<PlayerHealth>();

        GameObject visual = new GameObject("Hose stream");
        visual.transform.SetParent(transform, false);
        _stream = visual.AddComponent<LineRenderer>();
        _stream.sharedMaterial = streamMaterial;
        _stream.positionCount = 2;
        _stream.useWorldSpace = true;
        _stream.startColor = _stream.endColor = streamColor;
        _stream.sortingOrder = 2;
        _stream.enabled = false;
    }

    protected virtual void OnDisable() => StopSpraying();
    
    public void Spray(Vector2 direction, float deltaTime)
    {
        if (!isActiveAndEnabled || (_health != null && _health.IsDead) || direction.sqrMagnitude < 0.001f)
        {
            StopSpraying();
            return;
        }

        AimDirection = direction.normalized;
        Origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
        End = Origin + AimDirection * range;

        OnSpray(deltaTime);
        DrawStream(ShouldShowStream());
    }
    
    public void StopSpraying()
    {
        if (_stream != null) _stream.enabled = false;
        OnStopSpraying();
    }
    
    protected abstract void OnSpray(float deltaTime);
    
    protected virtual void OnStopSpraying() { }
    
    protected virtual bool ShouldShowStream() => true;
    
    protected void SetHitboxSize(float newRange, float newWidth)
    {
        range = newRange;
        width = newWidth;
    }
    
    protected List<HordeEnemy> FindEnemiesInHitbox()
    {
        _enemyHits.Clear();
        _seen.Clear();
        
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(hitLayers);
        Physics2D.OverlapBox((origin + end) * 0.5f, new Vector2(range, width),
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, filter, hits);
        damaged.Clear();
        watered.Clear();
        foreach (Collider2D hit in hits)
        {
            Collider2D hit = _colliderHits[i];
            if (hit == null) continue;
            HordeEnemy enemy = hit.GetComponentInParent<HordeEnemy>();
            if (enemy != null && enemy.isActiveAndEnabled && damaged.Add(enemy))
                enemy.TakeDamage(damagePerSecond * deltaTime);
            FireZone fire = hit.GetComponentInParent<FireZone>();
            if (fire != null && fire.isActiveAndEnabled && watered.Add(fire))
                fire.ApplyWater(extinguishPerSecond * deltaTime);
        }
        return _enemyHits;
    }

    private void DrawStream(bool visible)
    {
        _stream.enabled = visible;
        if (!visible) return;

        float z = transform.position.z;
        _stream.startWidth = _stream.endWidth = width;
        _stream.SetPosition(0, new Vector3(Origin.x, Origin.y, z));
        _stream.SetPosition(1, new Vector3(End.x, End.y, z));
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        Vector2 dir = Application.isPlaying ? AimDirection : (Vector2)transform.right;
        Vector2 start = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
        Vector2 center = start + dir * (range * 0.5f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(range, width, 0f));
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}