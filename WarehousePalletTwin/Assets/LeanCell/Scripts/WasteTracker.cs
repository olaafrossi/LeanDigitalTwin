using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LeanCell
{
    /// <summary>
    /// Central waste detection engine. Detects all 7 LEAN wastes and computes
    /// normalized scores (0-100) per type. Single file with regions per waste type.
    /// </summary>
    public class WasteTracker : MonoBehaviour
    {
        public static WasteTracker Instance { get; private set; }

        [Header("Thresholds")]
        public float WaitingThreshold = 3f;       // seconds before idle counts as waste
        public float MotionMultiplier = 1.2f;      // distance > optimal * this = waste
        public float TransportMultiplier = 1.3f;   // MU path > optimal * this = waste
        public float OverProcessingMultiplier = 1.1f;

        [Header("Scores (Read-Only)")]
        [SerializeField] private float[] wasteScores = new float[7];

        [Header("Recent Events")]
        [SerializeField] private int recentEventCount;

        public float[] WasteScores => wasteScores;
        public List<WasteEvent> RecentEvents { get; private set; } = new List<WasteEvent>();
        private const int MaxRecentEvents = 50;

        // Tracking state
        private Dictionary<int, float> workerIdleStart = new Dictionary<int, float>();
        private Dictionary<int, float> workerCycleDistance = new Dictionary<int, float>();
        private float[] stationIdleTimes;
        private int totalProduced;
        private int totalConsumed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            LeanCellEvents.OnWorkerStateChanged += HandleWorkerStateChanged;
            LeanCellEvents.OnWorkerMovedDistance += HandleWorkerMoved;
            LeanCellEvents.OnProcessComplete += HandleProcessComplete;
            LeanCellEvents.OnStationIdle += HandleStationIdle;
            LeanCellEvents.OnMUCreated += HandleMUCreated;
            LeanCellEvents.OnMUCompleted += HandleMUCompleted;
            LeanCellEvents.OnMUDefective += HandleMUDefective;
        }

        void OnDisable()
        {
            LeanCellEvents.OnWorkerStateChanged -= HandleWorkerStateChanged;
            LeanCellEvents.OnWorkerMovedDistance -= HandleWorkerMoved;
            LeanCellEvents.OnProcessComplete -= HandleProcessComplete;
            LeanCellEvents.OnStationIdle -= HandleStationIdle;
            LeanCellEvents.OnMUCreated -= HandleMUCreated;
            LeanCellEvents.OnMUCompleted -= HandleMUCompleted;
            LeanCellEvents.OnMUDefective -= HandleMUDefective;
        }

        void Start()
        {
            var manager = LeanCellManager.Instance;
            if (manager != null)
                stationIdleTimes = new float[manager.Stations.Length];
            else
                stationIdleTimes = new float[4];

            StartCoroutine(PollInventory());
            StartCoroutine(PollOverproduction());
        }

        public void Reset()
        {
            for (int i = 0; i < wasteScores.Length; i++)
                wasteScores[i] = 0;
            RecentEvents.Clear();
            workerIdleStart.Clear();
            workerCycleDistance.Clear();
            totalProduced = 0;
            totalConsumed = 0;
        }

        public float GetTotalWasteScore()
        {
            float total = 0;
            for (int i = 0; i < wasteScores.Length; i++)
                total += wasteScores[i];
            return total / wasteScores.Length;
        }

        #region Waiting Detection

        private void HandleWorkerStateChanged(int workerID, WorkerState oldState, WorkerState newState)
        {
            // Worker enters idle -> start tracking
            if (newState == WorkerState.Idle)
            {
                workerIdleStart[workerID] = Time.time;
            }
            // Worker leaves idle -> check if waited too long
            else if (oldState == WorkerState.Idle && workerIdleStart.ContainsKey(workerID))
            {
                float waitTime = Time.time - workerIdleStart[workerID];
                if (waitTime > WaitingThreshold)
                {
                    var evt = new WasteEvent(WasteType.Waiting, Time.time, workerID: workerID)
                    {
                        Duration = waitTime,
                        Severity = waitTime > 10f ? WasteSeverity.High : WasteSeverity.Medium,
                        Description = $"Worker {workerID} waited {waitTime:F1}s"
                    };
                    RecordWaste(evt);
                    wasteScores[(int)WasteType.Waiting] = Mathf.Min(100f, wasteScores[(int)WasteType.Waiting] + waitTime * 2f);
                }
                workerIdleStart.Remove(workerID);
            }

            // Track distance per cycle: reset when cycle starts
            if (newState == WorkerState.WalkingToPickup)
            {
                workerCycleDistance[workerID] = 0;
            }
        }

        #endregion

        #region Motion Detection

        private void HandleWorkerMoved(int workerID, float distance)
        {
            if (!workerCycleDistance.ContainsKey(workerID))
                workerCycleDistance[workerID] = 0;
            workerCycleDistance[workerID] += distance;
        }

        // Called when worker completes a cycle (delivers to sink)
        private void CheckMotionWaste(int workerID)
        {
            if (!workerCycleDistance.ContainsKey(workerID)) return;

            float traveled = workerCycleDistance[workerID];
            // Estimate optimal distance (straight lines between stations)
            float optimal = EstimateOptimalDistance();
            float threshold = optimal * MotionMultiplier;

            if (traveled > threshold)
            {
                float excess = traveled - optimal;
                var evt = new WasteEvent(WasteType.Motion, Time.time, workerID: workerID)
                {
                    Duration = excess,
                    Severity = excess > 5f ? WasteSeverity.High : WasteSeverity.Medium,
                    Description = $"Worker {workerID} excess motion: {excess:F1}m (traveled {traveled:F1}m, optimal {optimal:F1}m)"
                };
                RecordWaste(evt);
                wasteScores[(int)WasteType.Motion] = Mathf.Min(100f, wasteScores[(int)WasteType.Motion] + excess * 5f);
            }
        }

        private float EstimateOptimalDistance()
        {
            // Sum distances between sequential station WorkPositions
            var manager = LeanCellManager.Instance;
            if (manager == null || manager.Stations.Length < 2) return 10f;

            float total = 0;
            for (int i = 0; i < manager.Stations.Length - 1; i++)
            {
                if (manager.Stations[i] != null && manager.Stations[i + 1] != null &&
                    manager.Stations[i].WorkPosition != null && manager.Stations[i + 1].WorkPosition != null)
                {
                    total += Vector3.Distance(
                        manager.Stations[i].WorkPosition.position,
                        manager.Stations[i + 1].WorkPosition.position);
                }
            }
            return Mathf.Max(total, 5f);
        }

        #endregion

        #region Over-processing Detection

        private void HandleProcessComplete(int stationIndex, realvirtual.MU mu, float actualTime)
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;

            float expected = manager.GetStationCycleTime(stationIndex);
            float threshold = expected * OverProcessingMultiplier;

            if (actualTime > threshold)
            {
                float excess = actualTime - expected;
                var evt = new WasteEvent(WasteType.OverProcessing, Time.time, stationIndex)
                {
                    Duration = excess,
                    Severity = excess > 10f ? WasteSeverity.High : WasteSeverity.Medium,
                    Description = $"Station {stationIndex} over-processed: {actualTime:F1}s (expected {expected:F1}s)"
                };
                RecordWaste(evt);
                wasteScores[(int)WasteType.OverProcessing] = Mathf.Min(100f, wasteScores[(int)WasteType.OverProcessing] + excess * 3f);
            }
        }

        #endregion

        #region Station Idle Detection

        private void HandleStationIdle(int stationIndex)
        {
            if (stationIndex >= 0 && stationIndex < stationIdleTimes.Length)
            {
                stationIdleTimes[stationIndex] = Time.time;
            }
        }

        #endregion

        #region Inventory Detection (polled)

        private IEnumerator PollInventory()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                var manager = LeanCellManager.Instance;
                if (manager == null) continue;

                int wip = manager.CurrentWIP;
                if (wip > manager.MaxWIP)
                {
                    int excess = wip - manager.MaxWIP;
                    var evt = new WasteEvent(WasteType.Inventory, Time.time)
                    {
                        Duration = excess,
                        Severity = excess > 3 ? WasteSeverity.High : WasteSeverity.Medium,
                        Description = $"WIP: {wip} (limit {manager.MaxWIP})"
                    };
                    RecordWaste(evt);
                    wasteScores[(int)WasteType.Inventory] = Mathf.Min(100f, excess * 15f);
                }
                else
                {
                    // Decay score when within limits
                    wasteScores[(int)WasteType.Inventory] = Mathf.Max(0, wasteScores[(int)WasteType.Inventory] - 2f);
                }
            }
        }

        #endregion

        #region Overproduction Detection (polled)

        private IEnumerator PollOverproduction()
        {
            while (true)
            {
                yield return new WaitForSeconds(2f);

                int surplus = totalProduced - totalConsumed;
                var manager = LeanCellManager.Instance;
                int maxWIP = manager != null ? manager.MaxWIP : 5;

                if (surplus > maxWIP)
                {
                    int excess = surplus - maxWIP;
                    var evt = new WasteEvent(WasteType.Overproduction, Time.time)
                    {
                        Severity = excess > 3 ? WasteSeverity.High : WasteSeverity.Medium,
                        Description = $"Overproduction: {surplus} produced vs {totalConsumed} consumed"
                    };
                    RecordWaste(evt);
                    wasteScores[(int)WasteType.Overproduction] = Mathf.Min(100f, excess * 20f);
                }
                else
                {
                    wasteScores[(int)WasteType.Overproduction] = Mathf.Max(0, wasteScores[(int)WasteType.Overproduction] - 3f);
                }
            }
        }

        private void HandleMUCreated(realvirtual.MU mu) => totalProduced++;
        private void HandleMUCompleted(realvirtual.MU mu)
        {
            totalConsumed++;
            // Also check motion waste for the worker who delivered
            // (motion check happens per-cycle)
        }

        #endregion

        #region Defect Detection

        private void HandleMUDefective(realvirtual.MU mu, int stationIndex)
        {
            var evt = new WasteEvent(WasteType.Defects, Time.time, stationIndex)
            {
                Severity = WasteSeverity.High,
                Description = $"Defective MU at Station {stationIndex}"
            };
            RecordWaste(evt);
            wasteScores[(int)WasteType.Defects] = Mathf.Min(100f, wasteScores[(int)WasteType.Defects] + 25f);
        }

        #endregion

        #region Event Recording

        private void RecordWaste(WasteEvent evt)
        {
            RecentEvents.Add(evt);
            if (RecentEvents.Count > MaxRecentEvents)
                RecentEvents.RemoveAt(0);

            recentEventCount = RecentEvents.Count;
            LeanCellEvents.FireWasteDetected(evt.Type, evt);
        }

        #endregion

        void Update()
        {
            // Gradual score decay for time-based wastes
            float decayRate = Time.deltaTime * 0.5f;
            wasteScores[(int)WasteType.Waiting] = Mathf.Max(0, wasteScores[(int)WasteType.Waiting] - decayRate);
            wasteScores[(int)WasteType.Motion] = Mathf.Max(0, wasteScores[(int)WasteType.Motion] - decayRate);
            wasteScores[(int)WasteType.OverProcessing] = Mathf.Max(0, wasteScores[(int)WasteType.OverProcessing] - decayRate);
            wasteScores[(int)WasteType.Defects] = Mathf.Max(0, wasteScores[(int)WasteType.Defects] - decayRate * 0.2f);
        }
    }
}
