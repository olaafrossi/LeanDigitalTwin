using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LeanCell
{
    /// <summary>
    /// Top Status Rail — glanceable simulation state at a glance.
    /// v2: sim state dot, clock, bottleneck, throughput, waste score, defect count.
    /// </summary>
    public class DashboardController : MonoBehaviour
    {
        [Header("Sim State")]
        public Image SimStateDot;
        public TextMeshProUGUI SimStateText;
        public Color RunningColor = new Color(0f, 0.78f, 0.33f);  // green
        public Color PausedColor = new Color(1f, 0.75f, 0f);       // amber
        public Color StoppedColor = new Color(0.5f, 0.5f, 0.5f);   // gray

        [Header("Metrics")]
        public TextMeshProUGUI ClockText;
        public TextMeshProUGUI BottleneckText;
        public TextMeshProUGUI ThroughputText;
        public TextMeshProUGUI WasteScoreText;
        public TextMeshProUGUI DefectCountText;
        public TextMeshProUGUI WIPText;

        private float updateInterval = 0.5f;
        private float nextUpdateTime;

        // Smooth display values
        private float displayThroughput;
        private float displayWasteScore;

        void Update()
        {
            if (Time.time < nextUpdateTime) return;
            nextUpdateTime = Time.time + updateInterval;

            UpdateSimState();
            UpdateMetrics();
            UpdateClock();
        }

        private void UpdateSimState()
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;

            bool running = manager.IsRunning;
            bool paused = Time.timeScale == 0f;

            Color dotColor;
            string stateLabel;

            if (paused)
            {
                dotColor = PausedColor;
                stateLabel = "PAUSED";
            }
            else if (running)
            {
                dotColor = RunningColor;
                stateLabel = "RUNNING";
            }
            else
            {
                dotColor = StoppedColor;
                stateLabel = "STOPPED";
            }

            if (SimStateDot != null) SimStateDot.color = dotColor;
            if (SimStateText != null) SimStateText.text = stateLabel;
        }

        private void UpdateMetrics()
        {
            var manager = LeanCellManager.Instance;
            var tracker = WasteTracker.Instance;
            if (manager == null) return;

            // Throughput
            displayThroughput = Mathf.Lerp(displayThroughput, manager.CompletedPerHour, Time.deltaTime * 3f);
            if (ThroughputText != null)
                ThroughputText.text = $"{displayThroughput:F0} pcs/hr";

            // WIP
            if (WIPText != null)
            {
                WIPText.text = $"WIP: {manager.CurrentWIP}";
                WIPText.color = manager.CurrentWIP <= manager.MaxWIP
                    ? Color.white
                    : new Color(1f, 0.3f, 0.3f);
            }

            // Waste score
            if (tracker != null)
            {
                float wasteScore = tracker.GetTotalWasteScore();
                displayWasteScore = Mathf.Lerp(displayWasteScore, wasteScore, Time.deltaTime * 3f);
                if (WasteScoreText != null)
                {
                    WasteScoreText.text = $"Waste: {displayWasteScore:F0}";
                    WasteScoreText.color = displayWasteScore < 20
                        ? WasteColors.ValueAdd
                        : displayWasteScore < 50
                            ? new Color(1f, 0.8f, 0f)
                            : WasteColors.Waiting;
                }

                // Bottleneck
                if (BottleneckText != null)
                {
                    int bn = tracker.GetBottleneckStation();
                    BottleneckText.text = $"Bottleneck: S{bn + 1}";
                }
            }

            // Defects
            if (DefectCountText != null)
                DefectCountText.text = $"Defects: {manager.TotalDefects}";
        }

        private void UpdateClock()
        {
            var clock = SimulationClock.Instance;
            if (clock == null || ClockText == null) return;

            float t = clock.ElapsedSimTime;
            int minutes = Mathf.FloorToInt(t / 60f);
            int seconds = Mathf.FloorToInt(t % 60f);
            ClockText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
