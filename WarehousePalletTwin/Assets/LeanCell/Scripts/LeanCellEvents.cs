using System;
using UnityEngine;

namespace LeanCell
{
    /// <summary>
    /// Static event bus for LeanCell simulation. All systems subscribe/unsubscribe
    /// via OnEnable/OnDisable to avoid NullReferenceException on destroyed objects.
    /// </summary>
    public static class LeanCellEvents
    {
        // MU lifecycle
        public static event Action<realvirtual.MU> OnMUCreated;
        public static event Action<realvirtual.MU> OnMUCompleted;
        public static event Action<realvirtual.MU, int> OnMUDefective; // MU, stationIndex

        // Station events
        public static event Action<int, realvirtual.MU> OnProcessStart;          // stationIndex, MU
        public static event Action<int, realvirtual.MU, float> OnProcessComplete; // stationIndex, MU, actualTime
        public static event Action<int> OnStationIdle;                            // stationIndex

        // Worker events
        public static event Action<int, WorkerState, WorkerState> OnWorkerStateChanged; // workerID, oldState, newState
        public static event Action<int, float> OnWorkerMovedDistance;                    // workerID, distanceDelta

        // Waste events
        public static event Action<WasteType, WasteEvent> OnWasteDetected;

        // Parameter changes
        public static event Action OnParametersChanged;

        // Fire methods
        public static void FireMUCreated(realvirtual.MU mu) => OnMUCreated?.Invoke(mu);
        public static void FireMUCompleted(realvirtual.MU mu) => OnMUCompleted?.Invoke(mu);
        public static void FireMUDefective(realvirtual.MU mu, int stationIndex) => OnMUDefective?.Invoke(mu, stationIndex);

        public static void FireProcessStart(int stationIndex, realvirtual.MU mu) => OnProcessStart?.Invoke(stationIndex, mu);
        public static void FireProcessComplete(int stationIndex, realvirtual.MU mu, float actualTime) => OnProcessComplete?.Invoke(stationIndex, mu, actualTime);
        public static void FireStationIdle(int stationIndex) => OnStationIdle?.Invoke(stationIndex);

        public static void FireWorkerStateChanged(int workerID, WorkerState oldState, WorkerState newState) => OnWorkerStateChanged?.Invoke(workerID, oldState, newState);
        public static void FireWorkerMovedDistance(int workerID, float distance) => OnWorkerMovedDistance?.Invoke(workerID, distance);

        public static void FireWasteDetected(WasteType type, WasteEvent evt) => OnWasteDetected?.Invoke(type, evt);

        public static void FireParametersChanged() => OnParametersChanged?.Invoke();

        /// <summary>
        /// Call on simulation reset to clear all subscribers.
        /// </summary>
        public static void ClearAll()
        {
            OnMUCreated = null;
            OnMUCompleted = null;
            OnMUDefective = null;
            OnProcessStart = null;
            OnProcessComplete = null;
            OnStationIdle = null;
            OnWorkerStateChanged = null;
            OnWorkerMovedDistance = null;
            OnWasteDetected = null;
            OnParametersChanged = null;
        }
    }
}
