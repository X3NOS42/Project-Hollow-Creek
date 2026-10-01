using UnityEngine;

namespace HollowCreek.Environment
{
    /// <summary>
    /// Gentle candle flicker: varies the point light intensity around BaseIntensity
    /// with Perlin noise and gives the flame sphere a tiny scale wobble. The F1
    /// lighting menu drives BaseIntensity, so its intensity slider still works.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class CandleFlicker : MonoBehaviour
    {
        // Peak swing around the base intensity (0.15 = plus/minus 15%).
        public float flickerAmount = 0.15f;
        public float flickerSpeed = 6f;
        public Transform flame;

        /// <summary>Intensity the flicker multiplies. Set this, not light.intensity.</summary>
        public float BaseIntensity { get; set; }

        private Light candleLight;
        private float seed;
        private Vector3 flameBaseScale;

        private void Awake()
        {
            candleLight = GetComponent<Light>();
            BaseIntensity = candleLight.intensity;
            seed = Random.value * 100f;
            if (flame != null)
            {
                flameBaseScale = flame.localScale;
            }
        }

        private void Update()
        {
            // Perlin noise stays smooth over time; each candle gets its own
            // seed so a row of them never blinks in sync.
            float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
            float multiplier = 1f + (noise - 0.5f) * 2f * flickerAmount;
            candleLight.intensity = BaseIntensity * multiplier;

            if (flame != null)
            {
                flame.localScale = flameBaseScale * (1f + (noise - 0.5f) * 0.12f);
            }
        }
    }
}
