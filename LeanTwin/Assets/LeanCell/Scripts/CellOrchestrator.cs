using UnityEngine;
using System.Collections.Generic;

namespace LeanCell
{
    public enum MUFlowState
    {
        RobotPickup,
        ConveyorTransit,
        WaitingAtSensor,
        Worker1Pickup,
        Station1Process,
        Worker2Pickup,
        Station2Process,
        Worker3Pickup,
        Station3Process,
        SinkExit,
        DefectScrap
    }

    /// <summary>
    /// Central MU lifecycle state machine. Coordinates robot → conveyor → workers → stations.
    /// One instance per cell. Reads config from LeanCellManager, pushes state events.
    /// Uses Invoke-based polling instead of Update() for realvirtual compatibility.
    /// </summary>
    public class CellOrchestrator : MonoBehaviour
    {
        [Header("Robot")]
        public RobotController Robot;

        [Header("Conveyor")]
        public realvirtual.Drive ConveyorDrive;
        public realvirtual.Sensor ConveyorEndSensor;
        public float ConveyorSpeed = 500f; // mm/s

        [Header("Workers (indexed 0-2)")]
        public WorkerController[] Workers = new WorkerController[3];

        [Header("Stations (indexed 0-2)")]
        public WorkStation[] Stations = new WorkStation[3];

        [Header("Sink")]
        public realvirtual.Sink Sink;
        public Transform SinkPosition;

        [Header("Conveyor Pickup Point")]
        public Transform ConveyorPickupPoint; // where Worker 1 picks from conveyor end

        [Header("Exit Conveyor")]
        public float ExitConveyorEndX = 12.5f; // X position where MUs fall off the exit belt

        [Header("State (Read-Only)")]
        [SerializeField] private bool conveyorBlocked;
        [SerializeField] private int activeMUCount;

        public bool ConveyorIsBlocked => conveyorBlocked;

        // Track each MU's current flow state
        private Dictionary<realvirtual.MU, MUFlowState> muStates = new Dictionary<realvirtual.MU, MUFlowState>();

        // MU waiting at sensor for Worker 1
        private realvirtual.MU muAtSensor;

        // MUs waiting at station outputs for downstream workers (index = workerID)
        private realvirtual.MU[] pendingStationPickup = new realvirtual.MU[3];

        // Conveyor transit — programmatic MU movement
        private System.Collections.Generic.List<realvirtual.MU> conveyorMUs = new System.Collections.Generic.List<realvirtual.MU>();
        private System.Collections.Generic.List<realvirtual.MU> exitConveyorMUs = new System.Collections.Generic.List<realvirtual.MU>();
        private float conveyorSpeedMs; // m/s (ConveyorSpeed is mm/s)

        private bool initialized;

        void OnEnable()
        {
            LeanCellEvents.OnRobotCycleComplete += HandleRobotPlacedOnConveyor;
            LeanCellEvents.OnProcessComplete += HandleStationComplete;
            LeanCellEvents.OnMUDefective += HandleDefect;
            LeanCellEvents.OnMUCompleted += HandleMUCompleted;

            Invoke(nameof(Initialize), 0.5f);
        }

        void OnDisable()
        {
            CancelInvoke();
            LeanCellEvents.OnRobotCycleComplete -= HandleRobotPlacedOnConveyor;
            LeanCellEvents.OnProcessComplete -= HandleStationComplete;
            LeanCellEvents.OnMUDefective -= HandleDefect;
            LeanCellEvents.OnMUCompleted -= HandleMUCompleted;
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            // Start conveyor drive
            if (ConveyorDrive != null)
            {
                ConveyorDrive.TargetSpeed = ConveyorSpeed;
                ConveyorDrive.JogForward = true;
                Debug.Log($"[LeanCell] Orchestrator: conveyor started, speed={ConveyorSpeed}");
            }
            else
            {
                Debug.LogError("[LeanCell] Orchestrator: ConveyorDrive is null!");
            }

            conveyorSpeedMs = ConveyorSpeed / 1000f; // convert mm/s to m/s

            // Start polling and conveyor motion (replaces Update)
            InvokeRepeating(nameof(PollSensorAndUpdate), 0.5f, 0.1f);
            Debug.Log("[LeanCell] Orchestrator: initialized, polling sensor every 0.1s");
        }

        /// <summary>Replaces Update() — moves MUs along both conveyors, checks arrival.</summary>
        private void PollSensorAndUpdate()
        {
            activeMUCount = muStates.Count;
            MoveConveyorMUs();
            MoveExitConveyorMUs();
            CheckConveyorSensor();
            CheckStationOutputs();
        }

        /// <summary>Programmatically slide MUs along +X at conveyor speed.</summary>
        private void MoveConveyorMUs()
        {
            if (conveyorMUs.Count == 0 || conveyorBlocked) return;

            float sensorX = ConveyorEndSensor != null ? ConveyorEndSensor.transform.position.x : 1.7f;
            float step = conveyorSpeedMs * 0.1f; // speed * poll interval

            for (int i = conveyorMUs.Count - 1; i >= 0; i--)
            {
                var mu = conveyorMUs[i];
                if (mu == null) { conveyorMUs.RemoveAt(i); continue; }

                var pos = mu.transform.position;
                pos.x += step;
                mu.transform.position = pos;

                // Check if MU reached sensor position
                if (pos.x >= sensorX)
                {
                    conveyorMUs.RemoveAt(i);
                    HandleMUReachedSensor(mu);
                }
            }
        }

        /// <summary>MU arrived at conveyor end — dispatch to worker or block.</summary>
        private void HandleMUReachedSensor(realvirtual.MU mu)
        {
            muAtSensor = mu;
            Debug.Log($"[LeanCell] Orchestrator: MU {mu.name} reached sensor at conveyor end");

            if (Workers[0] != null && Workers[0].CurrentState == WorkerState.Idle)
            {
                SetMUState(mu, MUFlowState.Worker1Pickup);
                LeanCellEvents.FireMUReadyAtPickup(0, mu);
                Debug.Log($"[LeanCell] Orchestrator: dispatching Worker 0 for {mu.name}");
            }
            else
            {
                SetMUState(mu, MUFlowState.WaitingAtSensor);
                if (!conveyorBlocked)
                {
                    conveyorBlocked = true;
                    LeanCellEvents.FireConveyorBlocked();
                    Debug.Log("[LeanCell] Orchestrator: conveyor blocked — Worker 0 busy");
                }
            }
        }

        /// <summary>Robot placed MU on conveyor — add to conveyor transit list.</summary>
        private void HandleRobotPlacedOnConveyor(realvirtual.MU mu)
        {
            SetMUState(mu, MUFlowState.ConveyorTransit);
            conveyorMUs.Add(mu);
            Debug.Log($"[LeanCell] Orchestrator: tracking {mu.name} on conveyor ({conveyorMUs.Count} in transit)");
        }

        /// <summary>Check if Worker 1 became available while MU waits at sensor.</summary>
        private void CheckConveyorSensor()
        {
            if (conveyorBlocked && muAtSensor != null &&
                Workers[0] != null && Workers[0].CurrentState == WorkerState.Idle)
            {
                conveyorBlocked = false;
                LeanCellEvents.FireConveyorUnblocked();

                SetMUState(muAtSensor, MUFlowState.Worker1Pickup);
                LeanCellEvents.FireMUReadyAtPickup(0, muAtSensor);
                Debug.Log($"[LeanCell] Orchestrator: unblocked, dispatching Worker 0");
            }
        }

        /// <summary>Check if downstream workers became available while an MU waits at a station output.</summary>
        private void CheckStationOutputs()
        {
            for (int i = 1; i < Workers.Length; i++)
            {
                if (pendingStationPickup[i] != null &&
                    Workers[i] != null && Workers[i].CurrentState == WorkerState.Idle)
                {
                    var mu = pendingStationPickup[i];
                    pendingStationPickup[i] = null;
                    LeanCellEvents.FireMUReadyAtPickup(i, mu);
                    Debug.Log($"[LeanCell] Orchestrator: Worker {i} now idle, dispatching queued {mu.name}");
                }
            }
        }

        /// <summary>Called when a station completes processing. Routes MU to next worker or Sink.</summary>
        private void HandleStationComplete(int stationIndex, realvirtual.MU mu, float actualTime)
        {
            if (mu == null) return;

            int nextWorkerIndex = stationIndex + 1;

            if (nextWorkerIndex < Workers.Length && nextWorkerIndex < Stations.Length)
            {
                // Route to next worker
                MUFlowState nextState = nextWorkerIndex switch
                {
                    1 => MUFlowState.Worker2Pickup,
                    2 => MUFlowState.Worker3Pickup,
                    _ => MUFlowState.SinkExit
                };
                SetMUState(mu, nextState);

                if (Workers[nextWorkerIndex] != null && Workers[nextWorkerIndex].CurrentState == WorkerState.Idle)
                {
                    LeanCellEvents.FireMUReadyAtPickup(nextWorkerIndex, mu);
                    Debug.Log($"[LeanCell] Orchestrator: routing {mu.name} to Worker {nextWorkerIndex}");
                }
                else
                {
                    pendingStationPickup[nextWorkerIndex] = mu;
                    Debug.Log($"[LeanCell] Orchestrator: Worker {nextWorkerIndex} busy, queuing {mu.name} for pickup");
                }
            }
            else
            {
                // Last station done — route to Sink
                SetMUState(mu, MUFlowState.SinkExit);
            }
        }

        private void HandleDefect(realvirtual.MU mu, int stationIndex)
        {
            SetMUState(mu, MUFlowState.DefectScrap);
        }

        private void HandleMUCompleted(realvirtual.MU mu)
        {
            muStates.Remove(mu);
        }

        /// <summary>Called by Worker 0 when they pick a specific MU from conveyor end.</summary>
        public void NotifyWorker1PickedUp(realvirtual.MU pickedMU)
        {
            // Only clear muAtSensor if the picked MU IS the one at the sensor
            if (muAtSensor == pickedMU)
                muAtSensor = null;

            conveyorBlocked = false;
            LeanCellEvents.FireConveyorUnblocked();
            Debug.Log($"[LeanCell] Orchestrator: Worker 0 picked up {(pickedMU != null ? pickedMU.name : "null")}, conveyor ready");
        }

        // === Exit Conveyor ===

        /// <summary>Called by Worker 2 after placing finished MU on exit belt.</summary>
        public void AddToExitConveyor(realvirtual.MU mu)
        {
            exitConveyorMUs.Add(mu);
            Debug.Log($"[LeanCell] Orchestrator: {mu.name} on exit conveyor");
        }

        /// <summary>Slide exit conveyor MUs along +X, drop with gravity at the end.</summary>
        private void MoveExitConveyorMUs()
        {
            if (exitConveyorMUs.Count == 0) return;

            float step = conveyorSpeedMs * 0.1f;

            for (int i = exitConveyorMUs.Count - 1; i >= 0; i--)
            {
                var mu = exitConveyorMUs[i];
                if (mu == null) { exitConveyorMUs.RemoveAt(i); continue; }

                var pos = mu.transform.position;
                pos.x += step;
                mu.transform.position = pos;

                // Fell off the end — enable gravity and let it drop
                if (pos.x >= ExitConveyorEndX)
                {
                    exitConveyorMUs.RemoveAt(i);
                    var rb = mu.GetComponentInChildren<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                    }
                    Debug.Log($"[LeanCell] Orchestrator: {mu.name} fell off exit belt");
                    Destroy(mu.gameObject, 5f); // clean up after 5s
                }
            }
        }

        private void StopConveyor()
        {
            if (ConveyorDrive != null)
            {
                ConveyorDrive.JogForward = false;
            }
        }

        private void ResumeConveyor()
        {
            if (ConveyorDrive != null)
            {
                ConveyorDrive.TargetSpeed = ConveyorSpeed;
                ConveyorDrive.JogForward = true;
            }
        }

        private void SetMUState(realvirtual.MU mu, MUFlowState state)
        {
            muStates[mu] = state;
        }

        public MUFlowState GetMUState(realvirtual.MU mu)
        {
            return muStates.TryGetValue(mu, out var state) ? state : MUFlowState.RobotPickup;
        }

        /// <summary>True if conveyor has no MUs in transit and is not blocked.</summary>
        public bool CanAcceptConveyorMU()
        {
            return !conveyorBlocked && conveyorMUs.Count == 0 && muAtSensor == null;
        }

        /// <summary>Reset all tracking state.</summary>
        public void ResetOrchestrator()
        {
            muStates.Clear();
            muAtSensor = null;
            conveyorBlocked = false;
            for (int i = 0; i < pendingStationPickup.Length; i++)
                pendingStationPickup[i] = null;
            ResumeConveyor();
        }
    }
}
