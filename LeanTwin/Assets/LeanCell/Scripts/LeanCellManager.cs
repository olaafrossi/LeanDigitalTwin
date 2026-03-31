using UnityEngine;

namespace LeanCell
{
    public enum WorkerState
    {
        Idle,
        WalkingToPickup,
        PickingUp,
        WalkingToStation,
        Placing,
        Processing,
        PickingResult,
        WalkingToNextStation,
        WalkingToSink
    }

    /// <summary>
    /// Central simulation manager. Uses Invoke-based polling for realvirtual compatibility.
    /// </summary>
    public class LeanCellManager : MonoBehaviour
    {
        public static LeanCellManager Instance { get; private set; }

        [Header("Simulation Parameters")]
        public float CurrentTaktTime = 30f;
        public float CurrentDefectRate = 0f;
        public int MaxWIP = 5;
        public int ActiveWorkerCount = 3;

        [Header("References")]
        public WorkStation[] Stations;
        public WorkerController[] Workers;
        public realvirtual.Source Source;
        public realvirtual.Sink Sink;
        public SimulationClock Clock;

        [Header("Scenario")]
        public ScenarioPreset[] AvailablePresets;
        public ScenarioPreset ActivePreset;

        [Header("Metrics (Read-Only)")]
        [SerializeField] private int totalCompleted;
        [SerializeField] private int totalDefects;
        [SerializeField] private float completedPerHour;
        [SerializeField] private int currentWIP;

        public int TotalCompleted => totalCompleted;
        public int TotalDefects => totalDefects;
        public float CompletedPerHour => completedPerHour;
        public int CurrentWIP => currentWIP;

        private bool isRunning;
        private bool initialized;
        public bool IsRunning => isRunning;

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
            LeanCellEvents.OnMUCompleted += HandleMUCompleted;
            LeanCellEvents.OnMUDefective += HandleMUDefective;
            Invoke(nameof(Initialize), 0.5f);
        }

        void OnDisable()
        {
            CancelInvoke();
            LeanCellEvents.OnMUCompleted -= HandleMUCompleted;
            LeanCellEvents.OnMUDefective -= HandleMUDefective;
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            StartSimulation();

            // Start metrics polling (replaces Update)
            InvokeRepeating(nameof(PollMetrics), 1f, 0.5f);
            Debug.Log("[LeanCell] Manager: initialized, simulation started");
        }

        public void StartSimulation()
        {
            if (ActivePreset != null)
                ApplyPreset(ActivePreset);

            if (Clock != null)
                Clock.StartClock();
            isRunning = true;

            if (Source != null)
            {
                Source.Enabled = true;
                Source.AutomaticGeneration = true;
                Source.Interval = CurrentTaktTime;
            }
        }

        public void PauseSimulation()
        {
            isRunning = false;
            if (Clock != null) Clock.Pause();
            Time.timeScale = 0f;
        }

        public void ResumeSimulation()
        {
            isRunning = true;
            if (Clock != null) Clock.Resume();
            Time.timeScale = 1f;
        }

        public void ResetSimulation()
        {
            isRunning = false;
            totalCompleted = 0;
            totalDefects = 0;
            completedPerHour = 0;
            currentWIP = 0;
            if (Clock != null) Clock.StartClock();
            Time.timeScale = 1f;
        }

        public void ApplyPreset(ScenarioPreset preset)
        {
            ActivePreset = preset;
            CurrentTaktTime = preset.TaktTime;
            CurrentDefectRate = preset.DefectRate;
            MaxWIP = preset.MaxWIP;
            ActiveWorkerCount = preset.ActiveWorkerCount;

            float[] cycleTimes = {
                preset.Station1CycleTime,
                preset.Station2CycleTime,
                preset.Station3CycleTime
            };

            for (int i = 0; i < Stations.Length && i < 3; i++)
            {
                if (Stations[i] != null)
                    Stations[i].SetCycleTime(cycleTimes[i]);
            }

            for (int i = 0; i < Workers.Length; i++)
            {
                if (Workers[i] != null)
                    Workers[i].gameObject.SetActive(i < ActiveWorkerCount);
            }

            if (Source != null)
                Source.Interval = CurrentTaktTime;

            LeanCellEvents.FireParametersChanged();
        }

        /// <summary>Replaces Update() — polls WIP count and throughput.</summary>
        private void PollMetrics()
        {
            if (!isRunning) return;

            currentWIP = CountActiveWIP();

            if (Clock != null && Clock.ElapsedSimTime > 0)
                completedPerHour = totalCompleted / (Clock.ElapsedSimTime / 3600f);
        }

        private int CountActiveWIP()
        {
            int count = 0;
            foreach (var station in Stations)
            {
                if (station != null && station.HasMU)
                    count++;
            }
            foreach (var worker in Workers)
            {
                if (worker != null && worker.gameObject.activeSelf && worker.IsCarryingMU)
                    count++;
            }
            return count;
        }

        private void HandleMUCompleted(realvirtual.MU mu)
        {
            totalCompleted++;
        }

        private void HandleMUDefective(realvirtual.MU mu, int stationIndex)
        {
            totalDefects++;
        }

        public float GetStationCycleTime(int stationIndex)
        {
            if (stationIndex >= 0 && stationIndex < Stations.Length && Stations[stationIndex] != null)
                return Stations[stationIndex].CycleTime;
            return 30f;
        }
    }
}
