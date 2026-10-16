using Fireline.Shared.Infrastructure;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Runtime
{
    /// <summary>A placed environmental fire. Gameplay strength is independent of particles.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class FireZone : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float extinguishResistance = 60f;
        [SerializeField, Min(0f)] private float contactDamage = 10f;
        [SerializeField] private bool startsBurning = true;
        [Tooltip("Child containing only fire visuals; the gameplay collider stays at full size.")]
        [SerializeField] private Transform fireVisual;
        [SerializeField] private ParticleSystem extinguishSteam;
        [SerializeField] private UnityEvent onExtinguished = new UnityEvent();

        private HealthPool strength;
        private Vector3 fullVisualScale;
        //private ParticleSystem[] flames;
        private Material fireMaterial;
        private float InitalFlameHeight;
        private float[] fullEmissionRates;

        public bool IsBurning => strength != null && !strength.IsDead;
        public float RemainingStrength => strength != null ? strength.Current : 0f;
        public float Intensity => strength != null ? strength.Current / strength.Maximum : 0f;
        public float ContactDamage => IsBurning ? contactDamage : 0f;
        public UnityEvent OnExtinguished => onExtinguished;

        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
            
            strength = new HealthPool(extinguishResistance);
            if (!startsBurning) strength.TakeDamage(extinguishResistance);
            if (fireVisual != null)
            {
                fireMaterial = fireVisual.GetComponent<MeshRenderer>().materials[0];
                InitalFlameHeight = fireMaterial.GetFloat("_FlameLength");
                fullVisualScale = fireVisual.localScale;
                //flames = fireVisual.GetComponentsInChildren<ParticleSystem>(true);
                


                //dont need this with the shader
                /*fullEmissionRates = new float[flames.Length];
                for (int i = 0; i < flames.Length; i++)
                    fullEmissionRates[i] = flames[i].emission.rateOverTimeMultiplier;
            }*/

                if (extinguishSteam != null)
                    extinguishSteam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                UpdateVisuals();

            }
        }

        public void ApplyWater(float amount)
        {
            if (!isActiveAndEnabled || !IsBurning) return;
            bool extinguished = strength.TakeDamage(amount);
            UpdateVisuals();
            if (!extinguished) return;
                //if (extinguishSteam != null) extinguishSteam.Play(true);
            onExtinguished.Invoke();
        }

        /// <summary>Can be called by a future mission timer or an Inspector UnityEvent.</summary>
        public void Ignite()
        {
            if (strength == null || IsBurning) return;
            strength.Reset(extinguishResistance);
            if (extinguishSteam != null)
                extinguishSteam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (fireVisual == null) return;
            //make visual init
            fireVisual.gameObject.SetActive(IsBurning);
            SetProceduralFlameHeight();
            /*fireVisual.localScale = fullVisualScale * Mathf.Lerp(0.2f, 1f, Intensity);
            for (int i = 0; i < flames.Length; i++)
            {
                var emission = flames[i].emission;
                emission.rateOverTimeMultiplier = fullEmissionRates[i] * Intensity;
            }*/
        }

        private void SetProceduralFlameHeight()
        {
            //map the flame height to the strength of the fire
            fireMaterial.SetFloat("_FlameLength", Mathf.Lerp(0f, InitalFlameHeight, strength.Current / strength.Maximum ));
        }
    }
}
