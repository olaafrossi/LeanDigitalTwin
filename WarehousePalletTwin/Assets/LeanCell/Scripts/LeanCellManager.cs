using System.Collections.Generic;
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

    public class LeanCellManager : MonoBehaviour
    {
        public static LeanCellManager Instance { get; private set; }

        [Header("Simulation Parameters")]
        public float CurrentTaktTime = 35f;
        public int CurrentBatchSize = 1;
        public float CurrentDefectRate = 0f;
        public FlowMode CurrentFlowMode = FlowMode.Push;
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
        [SerializeField] private float completedPerHour;
        [SerializeField] private int currentWIP;

        public int TotalCompleted => totalCompleted;
        public float CompletedPerHour => completedPerHour;
        public int CurrentWIP => currentWIP;

        private bool isRunning;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            StartSimulation();
        }

        void OnEnable()
        {
            LeanCellEvents.OnMUCompleted += HandleMUCompleted;
        }

        void OnDisable()
        {
            LeanCellEvents.OnMUCompleted -= HandleMUCompleted;
        }

        public void StartSimulation()
        {
            if (ActivePreset != null)
                ApplyPreset(ActivePreset);

            Clock.StartClock();
            isRunning = true;

            if (Source != null)
                Source.Enabled = true;
        }

        public void PauseSimulation()
        {
            isRunning = false;
            Clock.Pause();
            Time.timeScale = 0f;
        }

        public void ResumeSimulation()
        {
            isRunning = true;
            Clock.Resume();
            Time.timeScale = 1f;
        }

        public void ResetSimulation()
        {
            isRunning = false;
            totalCompleted = 0;
            completedPerHour = 0;
            currentWIP = 0;
            Clock.StartClock();
            Time.timeScale = 1f;
        }

        public void ApplyPreset(ScenarioPreset preset)
        {
            ActivePreset = preset;
            CurrentTaktTime = preset.TaktTime;
            CurrentBatchSize = preset.BatchSize;
            CurrentDefectRate = preset.DefectRate;
            CurrentFlowMode = preset.FlowMode;
            MaxWIP = preset.MaxWIP;
            ActiveWorkerCount = preset.ActiveWorkerCount;

            // Apply per-station cycle times
            float[] cycleTimes = {
                preset.Station1CycleTime,
                preset.Station2CycleTime,
                preset.Station3CycleTime,
                preset.Station4CycleTime
            };

            for (int i = 0; i < Stations.Length && i < 4; i++)
            {
                if (Stations[i] != null)
                    Stations[i].SetCycleTime(cycleTimes[i]);
            }

            // Activate/deactivate workers
            for (int i = 0; i < Workers.Length; i++)
            {
                if (Workers[i] != null)
                    Workers[i].gameObject.SetActive(i < ActiveWorkerCount);
            }

            // Update source interval based on takt
            if (Source != null)
                Source.Interval = CurrentTaktTime;

            LeanCellEvents.FireParametersChanged();
        }

        void Update()
        {
            if (!isRunning) return;

            // Count current WIP
            currentWIP = CountActiveWIP();

            // Compute throughput
            if (Clock.ElapsedSimTime > 0)
                completedPerHour = totalCompleted / (Clock.ElapsedSimTime / 3600f);
        }

        private int CountActiveWIP()
        {
            int count = 0;
            // Count MUs at stations
            foreach (var station in Stations)
            {
                if (station != null && station.HasMU)
                    count++;
            }
            // Count MUs being carried by workers
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

        public float GetStationCycleTime(int stationIndex)
        {
            if (stationIndex >= 0 && stationIndex < Stations.Length && Stations[stationIndex] != null)
                return Stations[stationIndex].CycleTime;
            return 30f;
        }
    }
}
