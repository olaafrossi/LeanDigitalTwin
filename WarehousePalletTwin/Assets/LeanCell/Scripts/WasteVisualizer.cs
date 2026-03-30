using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

namespace LeanCell
{
    /// <summary>
    /// Renders waste overlays: floor zone highlights, contextual labels,
    /// and the "thermal camera" desaturation effect.
    /// </summary>
    public class WasteVisualizer : MonoBehaviour
    {
        [Header("Overlay Toggle")]
        public bool OverlayActive = false;

        [Header("Thermal Camera Effect")]
        public Volume PostProcessVolume;
        public float TransitionDuration = 0.5f;
        public float DesaturationAmount = -70f; // color grading saturation when overlay is on

        [Header("Floor Zone Highlights")]
        public MeshRenderer[] StationFloorZones; // 4 quads, one per station

        [Header("Contextual Labels")]
        public TextMeshPro[] StationWasteLabels; // 4 world-space TMP, floating above stations

        [Header("Legend")]
        public GameObject LegendPanel; // TIMWOOD legend strip on dashboard

        private float currentTransition = 0f;
        private bool transitioningIn;
        private bool transitioningOut;
        private ColorAdjustments colorAdjustments;

        void Start()
        {
            // Get post-processing color adjustments
            if (PostProcessVolume != null && PostProcessVolume.profile != null)
            {
                PostProcessVolume.profile.TryGet(out colorAdjustments);
            }

            // Initialize floor zones to transparent
            if (StationFloorZones != null)
            {
                foreach (var zone in StationFloorZones)
                {
                    if (zone != null)
                    {
                        var mat = zone.material;
                        var c = mat.color;
                        c.a = 0f;
                        mat.color = c;
                    }
                }
            }

            // Hide labels initially
            if (StationWasteLabels != null)
            {
                foreach (var label in StationWasteLabels)
                {
                    if (label != null) label.gameObject.SetActive(false);
                }
            }

            if (LegendPanel != null)
                LegendPanel.SetActive(false);
        }

        void OnEnable()
        {
            LeanCellEvents.OnWasteDetected += HandleWasteDetected;
        }

        void OnDisable()
        {
            LeanCellEvents.OnWasteDetected -= HandleWasteDetected;
        }

        public void ToggleOverlay()
        {
            OverlayActive = !OverlayActive;

            if (OverlayActive)
            {
                transitioningIn = true;
                transitioningOut = false;
                if (LegendPanel != null) LegendPanel.SetActive(true);
            }
            else
            {
                transitioningOut = true;
                transitioningIn = false;
            }
        }

        void Update()
        {
            UpdateThermalTransition();

            if (OverlayActive)
                UpdateFloorZones();
        }

        private void UpdateThermalTransition()
        {
            if (transitioningIn)
            {
                currentTransition += Time.deltaTime / TransitionDuration;
                if (currentTransition >= 1f)
                {
                    currentTransition = 1f;
                    transitioningIn = false;
                }
                ApplyDesaturation(currentTransition);
            }
            else if (transitioningOut)
            {
                currentTransition -= Time.deltaTime / TransitionDuration;
                if (currentTransition <= 0f)
                {
                    currentTransition = 0f;
                    transitioningOut = false;
                    if (LegendPanel != null) LegendPanel.SetActive(false);
                    HideAllLabels();
                    ClearFloorZones();
                }
                ApplyDesaturation(currentTransition);
            }
        }

        private void ApplyDesaturation(float t)
        {
            if (colorAdjustments == null) return;
            // Lerp saturation from 0 (normal) to DesaturationAmount (desaturated)
            colorAdjustments.saturation.value = Mathf.Lerp(0f, DesaturationAmount, t);
        }

        private void UpdateFloorZones()
        {
            var tracker = WasteTracker.Instance;
            if (tracker == null) return;

            var manager = LeanCellManager.Instance;
            if (manager == null) return;

            // Color each station's floor zone based on dominant waste
            for (int i = 0; i < manager.Stations.Length && i < StationFloorZones.Length; i++)
            {
                if (StationFloorZones[i] == null || manager.Stations[i] == null) continue;

                // Determine dominant waste color for this station
                WasteType dominant = GetDominantWasteForStation(i, tracker);
                Color zoneColor = dominant == (WasteType)(-1)
                    ? WasteColors.ValueAdd
                    : WasteColors.GetColor(dominant);

                float totalWaste = tracker.GetTotalWasteScore();
                float alpha = OverlayActive ? Mathf.Lerp(0.1f, 0.5f, totalWaste / 100f) * currentTransition : 0f;
                zoneColor.a = alpha;

                StationFloorZones[i].material.color = zoneColor;
            }
        }

        private WasteType GetDominantWasteForStation(int stationIndex, WasteTracker tracker)
        {
            // Find the most recent waste event for this station
            float maxScore = 0;
            WasteType dominant = (WasteType)(-1);

            for (int i = tracker.RecentEvents.Count - 1; i >= Mathf.Max(0, tracker.RecentEvents.Count - 10); i--)
            {
                var evt = tracker.RecentEvents[i];
                if (evt.StationIndex == stationIndex || evt.StationIndex == -1)
                {
                    float score = tracker.WasteScores[(int)evt.Type];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        dominant = evt.Type;
                    }
                }
            }

            return dominant;
        }

        private void HandleWasteDetected(WasteType type, WasteEvent evt)
        {
            if (!OverlayActive) return;

            // Show contextual label at station
            if (evt.StationIndex >= 0 && evt.StationIndex < StationWasteLabels.Length)
            {
                var label = StationWasteLabels[evt.StationIndex];
                if (label != null)
                {
                    label.gameObject.SetActive(true);
                    label.text = evt.Description;
                    label.color = WasteColors.GetColor(type);
                }
            }
        }

        private void HideAllLabels()
        {
            if (StationWasteLabels == null) return;
            foreach (var label in StationWasteLabels)
            {
                if (label != null) label.gameObject.SetActive(false);
            }
        }

        private void ClearFloorZones()
        {
            if (StationFloorZones == null) return;
            foreach (var zone in StationFloorZones)
            {
                if (zone != null)
                {
                    var c = zone.material.color;
                    c.a = 0f;
                    zone.material.color = c;
                }
            }
        }
    }
}
