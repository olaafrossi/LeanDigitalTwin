using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace LeanCell
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class WorkerController : MonoBehaviour
    {
        [Header("Identity")]
        public int WorkerID;

        [Header("Assignment")]
        public WorkStation[] AssignedStations;
        public Transform SourcePickupPoint;
        public Transform SinkDropPoint;

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
        private int currentStationIndex;
        private Vector3 lastPosition;
        private float nextIdleCheckTime;
        private Material workerDotMat;

        private static readonly int IsWalking = Animator.StringToHash("IsWalking");
        private static readonly int IsWorking = Animator.StringToHash("IsWorking");
        private static readonly int IsCarrying = Animator.StringToHash("IsCarrying");

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.stoppingDistance = StoppingDistance;
            agent.speed = 1.2f;          // normal human walking pace
            agent.acceleration = 2f;      // gentle acceleration
            agent.angularSpeed = 120f;    // smooth turning
            animator = GetComponentInChildren<Animator>();
            lastPosition = transform.position;
            currentStationIndex = 0;

            Debug.Log($"[LeanCell] Worker {WorkerID}: NavMeshAgent speed set to {agent.speed}");

            // Status dot above worker head (parented so it follows automatically)
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
        }

        void Update()
        {
            // Track distance
            float moved = Vector3.Distance(transform.position, lastPosition);
            if (moved > 0.01f)
            {
                distanceTraveledThisCycle += moved;
                LeanCellEvents.FireWorkerMovedDistance(WorkerID, moved);
            }
            lastPosition = transform.position;

            UpdateAnimator();

            switch (currentState)
            {
                case WorkerState.Idle:
                    HandleIdle();
                    break;
                case WorkerState.WalkingToPickup:
                case WorkerState.WalkingToStation:
                case WorkerState.WalkingToNextStation:
                case WorkerState.WalkingToSink:
                    HandleWalking();
                    break;
            }
        }

        void LateUpdate()
        {
            // MU follows worker with zero lag
            if (carriedMU != null)
            {
                carriedMU.transform.position = transform.position + Vector3.up * 1.2f;
            }

            // Update worker dot color
            if (workerDotMat != null)
            {
                workerDotMat.color = currentState switch
                {
                    WorkerState.Idle => WasteColors.Waiting,     // red = idle waste
                    WorkerState.Processing => WasteColors.ValueAdd, // green = value add
                    _ => Color.yellow                              // yellow = moving
                };
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
                           currentState == WorkerState.WalkingToNextStation ||
                           currentState == WorkerState.WalkingToSink;
            animator.SetBool(IsWalking, walking);
            animator.SetBool(IsWorking, currentState == WorkerState.Processing);
            animator.SetBool(IsCarrying, IsCarryingMU);
        }

        private void HandleIdle()
        {
            if (AssignedStations == null || AssignedStations.Length == 0) return;
            if (SourcePickupPoint == null) return;
            if (Time.time < nextIdleCheckTime) return;

            currentStationIndex = 0;
            NavigateTo(SourcePickupPoint.position);
            EnterState(WorkerState.WalkingToPickup);
            distanceTraveledThisCycle = 0;
            Debug.Log($"[LeanCell] Worker {WorkerID}: heading to Source");
        }

        private void HandleWalking()
        {
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[LeanCell] Worker {WorkerID}: path invalid");
                nextIdleCheckTime = Time.time + 2f;
                EnterState(WorkerState.Idle);
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
                    StartCoroutine(DoPickup());
                    break;
                case WorkerState.WalkingToStation:
                case WorkerState.WalkingToNextStation:
                    StartCoroutine(DoPlaceAndProcess());
                    break;
                case WorkerState.WalkingToSink:
                    StartCoroutine(DoDeliverToSink());
                    break;
            }
        }

        private IEnumerator DoPickup()
        {
            EnterState(WorkerState.PickingUp);
            yield return new WaitForSeconds(PickPlaceDelay);

            // Find nearest MU within range
            realvirtual.MU nearestMU = null;
            float nearestDist = 4f;

            var allMUs = FindObjectsByType<realvirtual.MU>(FindObjectsSortMode.None);
            foreach (var mu in allMUs)
            {
                if (mu.transform.parent != null) continue;
                if (mu.FixedBy != null) continue;

                float dist = Vector3.Distance(transform.position, mu.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestMU = mu;
                }
            }

            if (nearestMU == null)
            {
                nextIdleCheckTime = Time.time + 2f;
                EnterState(WorkerState.Idle);
                yield break;
            }

            // Grab it
            GrabMU(nearestMU);

            // Go to first station
            if (AssignedStations.Length > 0 && AssignedStations[currentStationIndex] != null)
            {
                var station = AssignedStations[currentStationIndex];
                if (station.IsOccupiedByWorker)
                {
                    nextIdleCheckTime = Time.time + 2f;
                    EnterState(WorkerState.Idle);
                    yield break;
                }
                NavigateTo(station.WorkPosition.position);
                EnterState(WorkerState.WalkingToStation);
            }
        }

        private IEnumerator DoPlaceAndProcess()
        {
            var station = AssignedStations[currentStationIndex];
            station.WorkerArrived(this);

            // Place MU at station
            EnterState(WorkerState.Placing);
            yield return new WaitForSeconds(PickPlaceDelay);

            realvirtual.MU mu = carriedMU;
            PlaceMUAtStation(mu, station);

            // Process (worker stands at station, MU sits on table)
            EnterState(WorkerState.Processing);
            station.StartProcessing(mu);

            yield return new WaitForSeconds(station.CycleTime);

            station.CompleteProcessing();

            // Pick result back up
            EnterState(WorkerState.PickingResult);
            yield return new WaitForSeconds(PickPlaceDelay);

            if (mu != null)
            {
                // Change MU color to show it's been processed
                ChangeMUColor(mu, currentStationIndex);
                GrabMU(mu);
            }

            station.WorkerLeft();

            // Move to next station or sink
            currentStationIndex++;
            if (currentStationIndex < AssignedStations.Length)
            {
                NavigateTo(AssignedStations[currentStationIndex].WorkPosition.position);
                EnterState(WorkerState.WalkingToNextStation);
            }
            else
            {
                if (SinkDropPoint != null)
                {
                    NavigateTo(SinkDropPoint.position);
                    EnterState(WorkerState.WalkingToSink);
                }
                else
                {
                    DropMU();
                    currentStationIndex = 0;
                    EnterState(WorkerState.Idle);
                }
            }
        }

        private IEnumerator DoDeliverToSink()
        {
            EnterState(WorkerState.Placing);
            yield return new WaitForSeconds(PickPlaceDelay);

            realvirtual.MU mu = carriedMU;
            if (mu != null)
            {
                DropMU();
                LeanCellEvents.FireMUCompleted(mu);
                Destroy(mu.gameObject, 1f);
            }

            currentStationIndex = 0;
            EnterState(WorkerState.Idle);
        }

        // === MU handling helpers ===

        private void GrabMU(realvirtual.MU mu)
        {
            // Disable physics and realvirtual MU logic while carrying
            var rb = mu.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }

            // Disable colliders so MU doesn't interact with anything while carried
            foreach (var col in mu.GetComponentsInChildren<Collider>())
                col.enabled = false;

            // Disable MU component so realvirtual doesn't fight us
            mu.enabled = false;

            // DON'T parent to worker (causes NavMesh issues). Just track reference.
            mu.transform.SetParent(null);
            carriedMU = mu;

            Debug.Log($"[LeanCell] Worker {WorkerID}: Grabbed {mu.name}");
        }

        private void PlaceMUAtStation(realvirtual.MU mu, WorkStation station)
        {
            if (mu == null) return;

            mu.transform.SetParent(null);
            // Place on the workbench: station's own position (not WorkPosition) + table height
            Vector3 tableTop = station.transform.position + Vector3.up * 1.2f;
            mu.transform.position = tableTop;

            var rb = mu.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // Re-enable colliders and MU at station
            foreach (var col in mu.GetComponentsInChildren<Collider>())
                col.enabled = true;
            mu.enabled = true;

            carriedMU = null;
            Debug.Log($"[LeanCell] Worker {WorkerID}: Placed at {station.StationName}");
        }

        private void DropMU()
        {
            if (carriedMU == null) return;

            carriedMU.transform.SetParent(null);
            carriedMU.transform.position = transform.position + Vector3.up * 0.3f;

            // Re-enable everything
            foreach (var col in carriedMU.GetComponentsInChildren<Collider>())
                col.enabled = true;
            carriedMU.enabled = true;

            var rb = carriedMU.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            carriedMU = null;
        }

        private void ChangeMUColor(realvirtual.MU mu, int stationIndex)
        {
            // MU gets progressively greener as it's processed through more stations
            var renderer = mu.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            float progress = (float)(stationIndex + 1) / AssignedStations.Length;
            Color c = Color.Lerp(new Color(0.3f, 0.5f, 0.8f), WasteColors.ValueAdd, progress);
            renderer.material.color = c;
        }

        private void NavigateTo(Vector3 destination)
        {
            agent.SetDestination(destination);
        }
    }
}
