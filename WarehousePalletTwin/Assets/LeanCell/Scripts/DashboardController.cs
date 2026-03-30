using UnityEngine;
using TMPro;

namespace LeanCell
{
    /// <summary>
    /// Main dashboard UI controller. Updates metrics at 2Hz.
    /// Attach to the Dashboard Canvas.
    /// </summary>
    public class DashboardController : MonoBehaviour
    {
        [Header("Metric Displays")]
        public TextMeshProUGUI ThroughputText;
        public TextMeshProUGUI CycleTimeText;
        public TextMeshProUGUI TaktTimeText;
        public TextMeshProUGUI WIPText;
        public TextMeshProUGUI WasteScoreText;
        public TextMeshProUGUI UtilizationText;

        [Header("Waste Bar Chart")]
        public RectTransform[] WasteBars; // 7 bars, one per waste type
        public float MaxBarHeight = 200f;

        [Header("Event Log")]
        public TextMeshProUGUI[] EventLogLines; // 5 lines for recent events

        [Header("Clock")]
        public TextMeshProUGUI ClockText;

        private float updateInterval = 0.5f;
        private float nextUpdateTime;

        // Smooth display values (lerp toward actual)
        private float displayThroughput;
        private float displayWasteScore;

        void Update()
        {
            if (Time.time < nextUpdateTime) return;
            nextUpdateTime = Time.time + updateInterval;

            UpdateMetrics();
            UpdateWasteBars();
            UpdateEventLog();
            UpdateClock();
        }

        private void UpdateMetrics()
        {
            var manager = LeanCellManager.Instance;
            var tracker = WasteTracker.Instance;
            var clock = SimulationClock.Instance;
            if (manager == null) return;

            // Lerp toward actual values for smooth animation
            displayThroughput = Mathf.Lerp(displayThroughput, manager.CompletedPerHour, Time.deltaTime * 3f);
            float wasteScore = tracker != null ? tracker.GetTotalWasteScore() : 0;
            displayWasteScore = Mathf.Lerp(displayWasteScore, wasteScore, Time.deltaTime * 3f);

            if (ThroughputText != null)
                ThroughputText.text = $"{displayThroughput:F0} pcs/hr";

            if (TaktTimeText != null)
                TaktTimeText.text = $"{manager.CurrentTaktTime:F0}s";

            if (WIPText != null)
                WIPText.text = $"{manager.CurrentWIP}";

            if (WasteScoreText != null)
            {
                WasteScoreText.text = $"{displayWasteScore:F0}/100";
                WasteScoreText.color = displayWasteScore < 20
                    ? WasteColors.ValueAdd
                    : displayWasteScore < 50
                        ? new Color(1f, 0.8f, 0f) // yellow
                        : WasteColors.Waiting;    // red
            }

            if (UtilizationText != null)
            {
                // Simple utilization: % of time workers are in Processing state
                int activeWorkers = 0;
                int processingWorkers = 0;
                if (manager.Workers != null)
                {
                    foreach (var w in manager.Workers)
                    {
                        if (w != null && w.gameObject.activeSelf)
                        {
                            activeWorkers++;
                            if (w.CurrentState == WorkerState.Processing)
                                processingWorkers++;
                        }
                    }
                }
                float util = activeWorkers > 0 ? (float)processingWorkers / activeWorkers * 100f : 0;
                UtilizationText.text = $"{util:F0}%";
            }
        }

        private void UpdateWasteBars()
        {
            var tracker = WasteTracker.Instance;
            if (tracker == null || WasteBars == null) return;

            for (int i = 0; i < WasteBars.Length && i < 7; i++)
            {
                if (WasteBars[i] == null) continue;
                float score = tracker.WasteScores[i];
                float height = Mathf.Lerp(0, MaxBarHeight, score / 100f);
                var size = WasteBars[i].sizeDelta;
                size.y = height;
                WasteBars[i].sizeDelta = size;
            }
        }

        private void UpdateEventLog()
        {
            var tracker = WasteTracker.Instance;
            if (tracker == null || EventLogLines == null) return;

            int eventCount = tracker.RecentEvents.Count;
            for (int i = 0; i < EventLogLines.Length; i++)
            {
                if (EventLogLines[i] == null) continue;

                int eventIndex = eventCount - 1 - i;
                if (eventIndex >= 0)
                {
                    var evt = tracker.RecentEvents[eventIndex];
                    EventLogLines[i].text = evt.Description;
                    EventLogLines[i].color = WasteColors.GetColor(evt.Type);
                    EventLogLines[i].gameObject.SetActive(true);
                }
                else
                {
                    EventLogLines[i].gameObject.SetActive(false);
                }
            }
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
