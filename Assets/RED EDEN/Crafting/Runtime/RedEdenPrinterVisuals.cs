using UnityEngine;

namespace RedEden.Crafting
{
    [DisallowMultipleComponent]
    public sealed class RedEdenPrinterVisuals : MonoBehaviour
    {
        [Header("Station")]
        [SerializeField] private RedEdenCraftingStation station;
        [SerializeField] private Transform visualEffectsRoot;

        [Header("Printer Motion")]
        [SerializeField] private Transform[] movingParts;
        [SerializeField] private Transform[] expandingParts;
        [SerializeField] private Vector3 travelOffset = new Vector3(0f, 0.12f, 0f);
        [SerializeField, Min(0f)] private float cycleSpeed = 1.5f;
        [SerializeField, Min(0f)] private float expansionAmount = 0.035f;
        [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Fabrication Effects")]
        [SerializeField] private ParticleSystem glowingDust;
        [SerializeField] private ParticleSystem smallStars;
        [SerializeField] private ParticleSystem swirlParticles;
        [SerializeField] private Transform silhouetteRoot;
        [SerializeField] private Renderer[] silhouetteRenderers;
        [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(0.1f)] private float completionAnimationDuration = 1.2f;

        [Header("Emission and Lighting")]
        [SerializeField] private Renderer[] emissiveRenderers;
        [SerializeField] private Color workingEmission = new Color(0.15f, 0.8f, 1f, 1f);
        [SerializeField] private Color readyEmission = new Color(0.2f, 1f, 0.55f, 1f);
        [SerializeField] private Light fabricationLight;
        [SerializeField, Min(0f)] private float workingLightIntensity = 2f;
        [SerializeField, Min(0f)] private float completionLightIntensity = 5f;
        [SerializeField] private Color idleColor = new Color(0.12f, 0.5f, 0.35f);

        private Vector3[] movingBasePositions;
        private Vector3[] expandingBaseScales;
        private Vector3 silhouetteBaseScale;
        private MaterialPropertyBlock propertyBlock;
        private RedEdenStationState previousState;
        private float completionTimeRemaining;
        private bool hasSilhouette;

        private void Awake()
        {
            if (station == null)
                station = GetComponent<RedEdenCraftingStation>();

            movingBasePositions = new Vector3[movingParts.Length];
            for (int i = 0; i < movingParts.Length; i++)
                if (CanAnimate(movingParts[i]))
                    movingBasePositions[i] = movingParts[i].localPosition;

            expandingBaseScales = new Vector3[expandingParts.Length];
            for (int i = 0; i < expandingParts.Length; i++)
                if (CanAnimate(expandingParts[i]))
                    expandingBaseScales[i] = expandingParts[i].localScale;

            hasSilhouette = CanAnimate(silhouetteRoot);
            if (hasSilhouette)
            {
                silhouetteBaseScale = silhouetteRoot.localScale;
                if (silhouetteRenderers.Length == 0)
                    silhouetteRenderers = silhouetteRoot.GetComponentsInChildren<Renderer>(true);
                SetRenderersEnabled(silhouetteRenderers, false);
            }

            if (visualEffectsRoot == null)
            {
                GameObject effects = new GameObject("Printer Visual Effects");
                effects.transform.SetParent(transform, false);
                visualEffectsRoot = effects.transform;
            }

            if (glowingDust == null)
                glowingDust = CreateParticleSystem("Glowing Dust", new Color(0.25f, 0.85f, 1f, 0.85f), 24f, 0.025f, 0.9f, false);
            if (smallStars == null)
                smallStars = CreateParticleSystem("Fabrication Stars", new Color(0.8f, 0.95f, 1f, 1f), 8f, 0.035f, 1.1f, false);
            if (swirlParticles == null)
                swirlParticles = CreateParticleSystem("Fabrication Swirl", new Color(0.25f, 0.65f, 1f, 0.75f), 16f, 0.018f, 0.8f, true);

            if (fabricationLight == null)
            {
                GameObject lightObject = new GameObject("Fabrication Glow");
                lightObject.transform.SetParent(visualEffectsRoot, false);
                lightObject.transform.localPosition = Vector3.up * 0.9f;
                fabricationLight = lightObject.AddComponent<Light>();
                fabricationLight.type = LightType.Point;
                fabricationLight.range = 3f;
            }

            propertyBlock = new MaterialPropertyBlock();
            previousState = station != null ? station.State : RedEdenStationState.Idle;
            SetEmission(Color.black);
            UpdateSilhouette(0f, false);
        }

        private void Update()
        {
            if (station == null)
                return;

            RedEdenStationState state = station.State;
            if (state == RedEdenStationState.ReadyToCollect && previousState == RedEdenStationState.Crafting)
            {
                completionTimeRemaining = Mathf.Max(0.1f, completionAnimationDuration);
                EmitCompletionBurst();
            }

            bool working = state == RedEdenStationState.Crafting;
            float cycle = working ? Mathf.PingPong(Time.time * cycleSpeed, 1f) : 0f;
            float motion = motionCurve.Evaluate(cycle);

            for (int i = 0; i < movingParts.Length; i++)
            {
                Transform part = movingParts[i];
                if (CanAnimate(part, false))
                    part.localPosition = movingBasePositions[i] + travelOffset * motion;
            }

            float expand = working ? motionCurve.Evaluate(Mathf.PingPong(Time.time * cycleSpeed * 0.7f, 1f)) : 0f;
            for (int i = 0; i < expandingParts.Length; i++)
            {
                Transform part = expandingParts[i];
                if (CanAnimate(part, false))
                    part.localScale = expandingBaseScales[i] * (1f + expansionAmount * expand);
            }

            float progress = working ? station.Progress : state == RedEdenStationState.ReadyToCollect ? 1f : 0f;
            UpdateSilhouette(progress, working || state == RedEdenStationState.ReadyToCollect);

            if (completionTimeRemaining > 0f)
                completionTimeRemaining = Mathf.Max(0f, completionTimeRemaining - Time.deltaTime);

            SetParticleActivity(working);
            UpdateEmission(state, working);
            previousState = state;
        }

        private bool CanAnimate(Transform target, bool report = true)
        {
            if (target == null || target == transform)
                return false;
            if (target.GetComponentInChildren<Collider>(true) != null)
            {
                if (report)
                    Debug.LogWarning("Printer visual animation skipped a transform containing colliders: " + target.name, target);
                return false;
            }
            return true;
        }

        private ParticleSystem CreateParticleSystem(string objectName, Color color, float emissionRate,
            float particleSize, float lifetime, bool orbit)
        {
            GameObject particleObject = new GameObject(objectName, typeof(ParticleSystem));
            particleObject.transform.SetParent(visualEffectsRoot, false);
            ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = lifetime;
            main.startSpeed = orbit ? 0.08f : 0.16f;
            main.startSize = particleSize;
            main.startColor = color;
            main.maxParticles = 160;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.55f;
            shape.radiusThickness = 0.08f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.35f, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            if (orbit)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.orbitalZ = new ParticleSystem.MinMaxCurve(90f);
            }

            return particles;
        }

        private void UpdateSilhouette(float progress, bool visible)
        {
            if (!hasSilhouette)
                return;

            if (visible && silhouetteRenderers.Length > 0)
            {
                SetRenderersEnabled(silhouetteRenderers, true);
                float reveal = revealCurve.Evaluate(Mathf.Clamp01(progress));
                silhouetteRoot.localScale = silhouetteBaseScale * Mathf.Lerp(0.04f, 1f, reveal);
                Color glow = Color.Lerp(workingEmission, readyEmission, reveal);
                SetRendererEmission(silhouetteRenderers, glow * (0.25f + reveal));
            }
            else
            {
                silhouetteRoot.localScale = Vector3.zero;
                SetRenderersEnabled(silhouetteRenderers, false);
            }
        }

        private void UpdateEmission(RedEdenStationState state, bool working)
        {
            float completionPulse = completionTimeRemaining > 0f
                ? Mathf.Sin((1f - completionTimeRemaining / Mathf.Max(0.1f, completionAnimationDuration)) * Mathf.PI)
                : 0f;
            Color emission = state == RedEdenStationState.ReadyToCollect
                ? readyEmission
                : working ? workingEmission * (0.55f + 0.45f * Mathf.PingPong(Time.time * 2f, 1f)) : Color.black;
            SetEmission(emission * (1f + completionPulse));

            if (fabricationLight != null)
            {
                fabricationLight.color = state == RedEdenStationState.ReadyToCollect ? readyEmission : workingEmission;
                fabricationLight.intensity = state == RedEdenStationState.ReadyToCollect
                    ? idleColor.grayscale + completionLightIntensity * completionPulse
                    : working ? workingLightIntensity * (0.75f + 0.25f * Mathf.Sin(Time.time * 3f)) : 0f;
            }
        }

        private void SetEmission(Color color)
        {
            SetRendererEmission(emissiveRenderers, color);
        }

        private void SetRendererEmission(Renderer[] renderers, Color color)
        {
            if (renderers == null)
                return;
            foreach (Renderer target in renderers)
            {
                if (target == null)
                    continue;
                target.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_EmissionColor", color);
                target.SetPropertyBlock(propertyBlock);
            }
        }

        private void SetParticleActivity(bool active)
        {
            SetParticleActivity(glowingDust, active);
            SetParticleActivity(smallStars, active);
            SetParticleActivity(swirlParticles, active);
        }

        private static void SetParticleActivity(ParticleSystem particles, bool active)
        {
            if (particles == null)
                return;
            if (active && !particles.isPlaying)
                particles.Play();
            else if (!active && particles.isPlaying)
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        private void EmitCompletionBurst()
        {
            if (smallStars != null)
                smallStars.Emit(28);
            if (glowingDust != null)
                glowingDust.Emit(40);
            if (swirlParticles != null)
                swirlParticles.Emit(24);
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
        {
            if (renderers == null)
                return;
            foreach (Renderer target in renderers)
                if (target != null)
                    target.enabled = enabled;
        }

        private void OnDisable()
        {
            if (movingParts != null)
                for (int i = 0; i < movingParts.Length; i++)
                    if (movingParts[i] != null && movingParts[i] != transform && movingBasePositions != null && i < movingBasePositions.Length)
                        movingParts[i].localPosition = movingBasePositions[i];
            if (expandingParts != null)
                for (int i = 0; i < expandingParts.Length; i++)
                    if (expandingParts[i] != null && expandingParts[i] != transform && expandingBaseScales != null && i < expandingBaseScales.Length)
                        expandingParts[i].localScale = expandingBaseScales[i];
        }
    }
}
