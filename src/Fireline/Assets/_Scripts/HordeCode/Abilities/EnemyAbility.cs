using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// A behavior an enemy type can have: shooting, summoning, leaving fires... Each one is an
    /// asset (Create > Fireline > Enemy Abilities) listed on an EnemyDefinition, so a new enemy
    /// type is a new combination of assets, and a new behavior is a new subclass.
    /// HordeEnemy and HordeManager never need to change for either.
    ///
    /// The asset only holds settings and is shared by every enemy using it. Anything that
    /// changes per enemy (cooldowns, who it summoned) lives in the runtime it creates.
    /// </summary>
    public abstract class EnemyAbility : ScriptableObject
    {
        /// <summary>Makes the per-enemy state for this ability. Called once per enemy, then reused across pooled respawns.</summary>
        public abstract EnemyAbilityRuntime CreateRuntime(HordeEnemy owner);
    }

    /// <summary>One enemy's copy of an ability: its timers and bookkeeping.</summary>
    public abstract class EnemyAbilityRuntime
    {
        protected HordeEnemy Owner { get; }

        protected EnemyAbilityRuntime(HordeEnemy owner) => Owner = owner;

        /// <summary>The enemy just spawned (or respawned from the pool). Reset timers here.</summary>
        public virtual void OnSpawned() { }

        /// <summary>Called by HordeManager every frame while the enemy is alive and has finished rising.</summary>
        public abstract void Tick(float deltaTime, IHordeTargets targets);

        /// <summary>The enemy died or was recycled.</summary>
        public virtual void OnDespawned() { }

        /// <summary>The enemy's position on the ground plane.</summary>
        protected Vector2 OwnerPosition => Owner.CachedTransform.position;
    }
}
