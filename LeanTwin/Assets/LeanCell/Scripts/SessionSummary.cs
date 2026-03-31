using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LeanCell
{
    /// <summary>
    /// Session summary overlay shown when the sim is paused after >60s of running.
    /// Displays throughput, defects, first pass yield, dominant waste, bottleneck,
    /// and a LEAN insight sentence.
    /// </summary>
    public class SessionSummary : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject SummaryPanel;
        public TextMeshProUGUI ThroughputText;
        public TextMeshProUGUI DefectsText;
        public TextMeshProUGUI FirstPassYieldText;
        public TextMeshProUGUI DominantWasteText;
        public TextMeshProUGUI BottleneckText;
        public TextMeshProUGUI InsightText;
        public Button ResumeButton;
        public Button ResetButton;

        [Header("Settings")]
        public float MinRunTimeForSummary = 60f;

        private bool wasRunning;

        void Start()
        {
            if (SummaryPanel != null)
                SummaryPanel.SetActive(false);

            if (ResumeButton != null)
                ResumeButton.onClick.AddListener(OnResume);
            if (ResetButton != null)
                ResetButton.onClick.AddListener(OnReset);
        }

        void Update()
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;

            // Detect pause transition after sufficient run time
            if (wasRunning && !manager.IsRunning)
            {
                var clock = SimulationClock.Instance;
                if (clock != null && clock.ElapsedSimTime >= MinRunTimeForSummary)
                    ShowSummary();
            }
            wasRunning = manager.IsRunning;
        }

        private void ShowSummary()
        {
            if (SummaryPanel == null) return;

            var manager = LeanCellManager.Instance;
            var tracker = WasteTracker.Instance;
            if (manager == null) return;

            SummaryPanel.SetActive(true);

            // Throughput
            if (ThroughputText != null)
                ThroughputText.text = $"Throughput: {manager.CompletedPerHour:F0} pcs/hr";

            // Defects
            if (DefectsText != null)
                DefectsText.text = $"Defects: {manager.TotalDefects} scrapped";

            // First Pass Yield
            int totalAttempted = manager.TotalCompleted + manager.TotalDefects;
            float fpy = totalAttempted > 0 ? (float)manager.TotalCompleted / totalAttempted * 100f : 100f;
            if (FirstPassYieldText != null)
                FirstPassYieldText.text = $"First Pass Yield: {fpy:F0}%";

            if (tracker != null)
            {
                // Dominant waste
                WasteType dominant = tracker.GetDominantWaste();
                if (DominantWasteText != null)
                {
                    DominantWasteText.text = $"Dominant Waste: {WasteColors.GetLabel(dominant)}";
                    DominantWasteText.color = WasteColors.GetColor(dominant);
                }

                // Bottleneck
                int bn = tracker.GetBottleneckStation();
                if (BottleneckText != null)
                    BottleneckText.text = $"Bottleneck: Station {bn + 1}";

                // LEAN Insight
                if (InsightText != null)
                    InsightText.text = GenerateInsight(dominant, bn, manager);
            }
        }

        private string GenerateInsight(WasteType dominant, int bottleneckStation, LeanCellManager manager)
        {
            float stationCycleTime = manager.GetStationCycleTime(bottleneckStation);
            float takt = manager.CurrentTaktTime;
            string stationName = $"S{bottleneckStation + 1}";

            return dominant switch
            {
                WasteType.Waiting =>
                    $"{stationName} processing time ({stationCycleTime:F0}s) exceeds takt ({takt:F0}s). " +
                    "Rebalance station work content to reduce waiting waste downstream.",

                WasteType.Overproduction =>
                    $"Robot feed rate (takt {takt:F0}s) outpaces downstream capacity. " +
                    "Increase takt time or reduce station cycle times to match demand.",

                WasteType.Inventory =>
                    $"WIP accumulating between stations. {stationName} is the constraint at {stationCycleTime:F0}s. " +
                    "Reduce batch size or balance line to establish one-piece flow.",

                WasteType.Motion =>
                    $"Workers traveling excess distance per cycle. Review station layout spacing " +
                    "and pickup point positions to minimize non-value-add movement.",

                WasteType.Defects =>
                    $"High defect rate generating scrap transport waste. Investigate root cause at " +
                    $"defect-prone stations. Consider implementing poka-yoke (error-proofing).",

                WasteType.OverProcessing =>
                    $"{stationName} actual processing exceeds standard work time ({stationCycleTime:F0}s). " +
                    "Standardize work instructions and verify station tooling.",

                WasteType.Transport =>
                    "MU travel distance exceeds optimal path. Review cell layout for " +
                    "unnecessary material movement between operations.",

                _ => "Run the simulation longer to generate actionable LEAN insights."
            };
        }

        public void Dismiss()
        {
            if (SummaryPanel != null)
                SummaryPanel.SetActive(false);
        }

        private void OnResume()
        {
            Dismiss();
            var manager = LeanCellManager.Instance;
            if (manager != null) manager.ResumeSimulation();
        }

        private void OnReset()
        {
            Dismiss();
            var manager = LeanCellManager.Instance;
            if (manager != null) manager.ResetSimulation();
        }
    }
}
