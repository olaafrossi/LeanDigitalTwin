using UnityEngine;
using UnityEngine.AI;

namespace LeanCell
{
    /// <summary>
    /// Worker controller using Invoke-based state machine for realvirtual compatibility.
    /// No Update/LateUpdate/coroutines — all timing via Invoke/InvokeRepeating.
    /// </summary>
    public class WorkerController : MonoBehaviour
    {
        [Header("Identity")]
        public int WorkerID;

        [Header("Assignment (v2: one station per worker)")]
        public WorkStation AssignedStation;
        public Transform PickupPoint;     // conveyor end for Worker 0, previous station OutputPoint for Workers 1/2
        public Transform IdlePosition;    // where worker waits between cycles
        public Transform SinkDropPoint;   // only used by last worker (Worker 2)
        public bool IsLastWorker;         // Worker 2 delivers to Sink after processing

        [Header("Orchestrator")]
        public CellOrchestrator Orchestrator;

        [Header("Grip")]
        public realvirtual.Grip HandGrip;

        [Header("Tuning")]
        public float StoppingDistance = 0.5f;
        public float PickPlaceDelay = 0.8f;

        [Header("State (Read-Only)")]
        [SerializeField] private WorkerState currentState = WorkerState.Idle;
        [SerializeField] private float idleStartTime;
        [SerializeField] private float distanceTraveledThisCycle;

        public WorkerState CurrentState => currentState;
        public bool IsCarryingMU => carriedMU != null;
        public float IdleStartTime => idleStartTime;

        [HideInInspector] public realvirtual.MU carriedMU;

        private NavMeshAgent agent;
        private Animator animator;
        private Vector3 lastPosition;
        private Material workerDotMat;
        private realvirtual.MU pendingPickupMU;
        private realvirtual.MU stationMU; // tracks MU during station processing
        private bool initialized;
        private bool useSimpleMovement; // fallback if NavMesh unavailable
        private Vector3 moveTarget;

        private static readonly int IsWalking = Animator.StringToHash("IsWalking");
        private static readonly int IsWorking = Animator.StringToHash("IsWorking");
        private static readonly int IsCarrying = Animator.StringToHash("IsCarrying");

        void OnEnable()
        {
            LeanCellEvents.OnMUReadyAtPickup += HandleMUReadyAtPickup;
            Invoke(nameof(Initialize), 0.3f);
        }

        void OnDisable()
        {
            CancelInvoke();
            LeanCellEvents.OnMUReadyAtPickup -= HandleMUReadyAtPickup;
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.stoppingDistance = StoppingDistance;
                agent.speed = 1.2f;
                agent.acceleration = 2f;
                agent.angularSpeed = 120f;

                // Try to place agent on NavMesh
                if (!agent.isOnNavMesh)
                {
                    agent.Warp(transform.position);
                    if (!agent.isOnNavMesh)
                    {
                        Debug.LogWarning($"[LeanCell] Worker {WorkerID}: NavMesh unavailable at {transform.position}, using simple movement");
                        useSimpleMovement = true;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[LeanCell] Worker {WorkerID}: no NavMeshAgent, using simple movement");
                useSimpleMovement = true;
            }

            animator = GetComponentInChildren<Animator>();
            lastPosition = transform.position;

            // Status dot above worker head
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = $"WorkerDot_{WorkerID}";
            dot.transform.SetParent(transform);
            dot.transform.localPosition = new Vector3(0, 2.2f, 0);
            dot.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            Destroy(dot.GetComponent<Collider>());
            workerDotMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            workerDotMat.color = Color.green;
            dot.GetComponent<Renderer>().material = workerDotMat;

            EnterState(WorkerState.Idle);

            // Go to idle position at start
            if (IdlePosition != null)
                NavigateTo(IdlePosition.position);

            // Start poll loop (replaces Update/LateUpdate)
            InvokeRepeating(nameof(PollUpdate), 0.1f, 0.1f);
            Debug.Log($"[LeanCell] Worker {WorkerID}: initialized at {transform.position}, simpleMove={useSimpleMovement}");
        }

        private void HandleMUReadyAtPickup(int workerID, realvirtual.MU mu)
        {
            if (workerID != WorkerID) return;
            if (currentState != WorkerState.Idle) return;

            pendingPickupMU = mu;
            distanceTraveledThisCycle = 0;
            NavigateTo(PickupPoint.position);
            EnterState(WorkerState.WalkingToPickup);
            Debug.Log($"[LeanCell] Worker {WorkerID}: dispatched to pickup {mu.name}");
        }

        // === Polling (replaces Update/LateUpdate) ===

        private void PollUpdate()
        {
            // Distance tracking
            float moved = Vector3.Distance(transform.position, lastPosition);
            if (moved > 0.01f)
            {
                distanceTraveledThisCycle += moved;
                LeanCellEvents.FireWorkerMovedDistance(WorkerID, moved);
            }
            lastPosition = transform.position;

            UpdateAnimator();

            // MU following (was LateUpdate)
            if (carriedMU != null)
                carriedMU.transform.position = transform.position + Vector3.up * 1.2f;

            // Update worker dot color
            if (workerDotMat != null)
            {
                workerDotMat.color = currentState switch
                {
                    WorkerState.Idle => WasteColors.Waiting,
                    WorkerState.Processing => WasteColors.ValueAdd,
                    _ => Color.yellow
                };
            }

            // Simple movement update
            if (useSimpleMovement)
                UpdateSimpleMovement();

            // Walking arrival check
            switch (currentState)
            {
                case WorkerState.WalkingToPickup:
                case WorkerState.WalkingToStation:
                case WorkerState.WalkingToSink:
                    HandleWalking();
                    break;
            }
        }

        private void EnterState(WorkerState newState)
        {
            var oldState = currentState;
            currentState = newState;
            if (newState == WorkerState.Idle)
                idleStartTime = Time.time;
            LeanCellEvents.FireWorkerStateChanged(WorkerID, oldState, newState);
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            bool walking = currentState == WorkerState.WalkingToPickup ||
                           currentState == WorkerState.WalkingToStation ||
                           currentState == WorkerState.WalkingToSink;
            animator.SetBool(IsWalking, walking);
            animator.SetBool(IsWorking, currentState == WorkerState.Processing);
            animator.SetBool(IsCarrying, IsCarryingMU);
        }

        // === Walking / arrival detection ===

        private void HandleWalking()
        {
            if (useSimpleMovement)
            {
                if (Vector3.Distance(transform.position, moveTarget) <= StoppingDistance)
                {
                    OnArrivedAtDestination();
                }
                return;
            }

            if (agent == null || !agent.isOnNavMesh) return;

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[LeanCell] Worker {WorkerID}: path invalid, switching to simple movement");
                useSimpleMovement = true;
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= StoppingDistance)
            {
                agent.ResetPath();
                OnArrivedAtDestination();
            }
        }

        private void OnArrivedAtDestination()
        {
            switch (currentState)
            {
                case WorkerState.WalkingToPickup:
                    Debug.Log($"[LeanCell] Worker {WorkerID}: arrived at pickup");
                    EnterState(WorkerState.PickingUp);
                    Invoke(nameof(CompletePickup), PickPlaceDelay);
                    break;
                case WorkerState.WalkingToStation:
                    Debug.Log($"[LeanCell] Worker {WorkerID}: arrived at station");
                    if (AssignedStation != null) AssignedStation.WorkerArrived(this);
                    EnterState(WorkerState.Placing);
                    Invoke(nameof(CompletePlacing), PickPlaceDelay);
                    break;
                case WorkerState.WalkingToSink:
                    Debug.Log($"[LeanCell] Worker {WorkerID}: arrived at sink");
                    EnterState(WorkerState.Placing);
                    Invoke(nameof(CompleteSinkDelivery), PickPlaceDelay);
                    break;
            }
        }

        // === Invoke chain: Pickup ===

        private void CompletePickup()
        {
            realvirtual.MU mu = pendingPickupMU;
            pendingPickupMU = null;

            if (mu == null)
                mu = FindNearestMU();

            if (mu == null)
            {
                Debug.LogWarning($"[LeanCell] Worker {WorkerID}: no MU found at pickup");
                EnterState(WorkerState.Idle);
                return;
            }

            GrabMU(mu);
            Debug.Log($"[LeanCell] Worker {WorkerID}: picked up {mu.name}");

            // Notify orchestrator that Worker 0 picked up from conveyor
            if (WorkerID == 0 && Orchestrator != null)
                Orchestrator.NotifyWorker1PickedUp();

            // Navigate to assigned station
            if (AssignedStation != null && AssignedStation.WorkPosition != null)
            {
                NavigateTo(AssignedStation.WorkPosition.position);
                EnterState(WorkerState.WalkingToStation);
            }
            else
            {
                EnterState(WorkerState.Idle);
            }
        }

        // === Invoke chain: Place and Process ===

        private void CompletePlacing()
        {
            if (AssignedStation == null) return;

            stationMU = carriedMU;
            PlaceMUAtStation(stationMU, AssignedStation);
            Debug.Log($"[LeanCell] Worker {WorkerID}: placed MU at {AssignedStation.StationName}");

            EnterState(WorkerState.Processing);
            AssignedStation.StartProcessing(stationMU);

            // Wait for station cycle time then complete
            Invoke(nameof(CompleteProcessing), AssignedStation.CycleTime);
        }

        private void CompleteProcessing()
        {
            if (AssignedStation == null) return;

            AssignedStation.CompleteProcessing();
            Debug.Log($"[LeanCell] Worker {WorkerID}: processing complete at {AssignedStation.StationName}");

            EnterState(WorkerState.PickingResult);
            Invoke(nameof(CompletePickResult), PickPlaceDelay);
        }

        private void CompletePickResult()
        {
            realvirtual.MU mu = stationMU;
            stationMU = null;

            if (mu != null)
            {
                ChangeMUColor(mu);
                GrabMU(mu);
            }

            AssignedStation.WorkerLeft();

            if (IsLastWorker && SinkDropPoint != null)
            {
                // Last worker delivers to Sink
                NavigateTo(SinkDropPoint.position);
                EnterState(WorkerState.WalkingToSink);
            }
            else
            {
                // Place at station OutputPoint for next worker, then return to idle
                if (mu != null && AssignedStation.OutputPoint != null)
                {
                    DropMUAt(mu, AssignedStation.OutputPoint.position);
                }
                else
                {
                    DropMU();
                }

                // Walk back to idle position
                if (IdlePosition != null)
                    NavigateTo(IdlePosition.position);
                EnterState(WorkerState.Idle);
            }
        }

        // === Invoke chain: Sink delivery ===

        private void CompleteSinkDelivery()
        {
            realvirtual.MU mu = carriedMU;
            if (mu != null)
            {
                DropMU();
                LeanCellEvents.FireMUCompleted(mu);
                Debug.Log($"[LeanCell] Worker {WorkerID}: delivered {mu.name} to sink");
                Destroy(mu.gameObject, 1f);
            }

            // Walk back to idle position
            if (IdlePosition != null)
                NavigateTo(IdlePosition.position);
            EnterState(WorkerState.Idle);
        }

        // === Navigation ===

        private void NavigateTo(Vector3 destination)
        {
            moveTarget = destination;
            if (!useSimpleMovement && agent != null && agent.isOnNavMesh)
                agent.SetDestination(destination);
        }

        private void UpdateSimpleMovement()
        {
            if (currentState != WorkerState.WalkingToPickup &&
                currentState != WorkerState.WalkingToStation &&
                currentState != WorkerState.WalkingToSink) return;

            // Move at 1.2 m/s, poll runs every 0.1s → 0.12m per step
            transform.position = Vector3.MoveTowards(
                transform.position, moveTarget, 0.12f);

            // Face movement direction
            Vector3 dir = (moveTarget - transform.position);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        // === MU handling helpers ===

        private realvirtual.MU FindNearestMU()
        {
            realvirtual.MU nearest = null;
            float nearestDist = 4f;

            var allMUs = FindObjectsByType<realvirtual.MU>(FindObjectsSortMode.None);
            foreach (var mu in allMUs)
            {
                if (mu.transform.parent != null) continue;

                float dist = Vector3.Distance(transform.position, mu.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = mu;
                }
            }
            return nearest;
        }

        private void GrabMU(realvirtual.MU mu)
        {
            var rb = mu.GetComponentInChildren<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }

            foreach (var col in mu.GetComponentsInChildren<Collider>())
                col.enabled = false;

            mu.enabled = false;
            mu.transform.SetParent(null);
            carriedMU = mu;

            // Clear output flag on previous station
            if (WorkerID > 0 && WorkerID - 1 < LeanCellManager.Instance.Stations.Length)
            {
                var prevStation = LeanCellManager.Instance.Stations[WorkerID - 1];
                if (prevStation != null)
                    prevStation.ClearOutput();
            }
        }

        private void PlaceMUAtStation(realvirtual.MU mu, WorkStation station)
        {
            if (mu == null) return;

            mu.transform.SetParent(null);
            Vector3 tableTop = station.transform.position + Vector3.up * 1.2f;
            mu.transform.position = tableTop;

            var rb = mu.GetComponentInChildren<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            foreach (var col in mu.GetComponentsInChildren<Collider>())
                col.enabled = true;
            mu.enabled = true;

            carriedMU = null;
        }

        private void DropMU()
        {
            if (carriedMU == null) return;

            carriedMU.transform.SetParent(null);
            carriedMU.transform.position = transform.position + Vector3.up * 0.3f;

            foreach (var col in carriedMU.GetComponentsInChildren<Collider>())
                col.enabled = true;
            carriedMU.enabled = true;

            var rb = carriedMU.GetComponentInChildren<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            carriedMU = null;
        }

        private void DropMUAt(realvirtual.MU mu, Vector3 position)
        {
            if (mu == null) return;

            mu.transform.SetParent(null);
            mu.transform.position = position + Vector3.up * 0.5f;

            foreach (var col in mu.GetComponentsInChildren<Collider>())
                col.enabled = true;
            mu.enabled = true;

            var rb = mu.GetComponentInChildren<Rigidbody>();
            if (rb != null) rb.isKinematic = true; // stay put at output point

            carriedMU = null;
        }

        private void ChangeMUColor(realvirtual.MU mu)
        {
            var renderer = mu.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            // Color based on station progress: S1 = blue-ish, S2 = teal, S3 = green
            float progress = (float)(AssignedStation.StationIndex + 1) / 3f;
            Color c = Color.Lerp(new Color(0.3f, 0.5f, 0.8f), WasteColors.ValueAdd, progress);
            renderer.material.color = c;
        }
    }
}
