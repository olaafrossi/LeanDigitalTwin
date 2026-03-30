using UnityEngine;

namespace LeanCell
{
    public enum RobotState { Idle, Picking, MovingToPlace, Placing, WaitingForConveyor }

    public class RobotController : MonoBehaviour
    {
        [Header("Waypoints")]
        public Transform PickTransform;
        public Transform PlaceTransform;

        [Header("References")]
        public CellOrchestrator Orchestrator;

        [Header("Timing")]
        public float GripDelay = 0.5f;
        public float PlaceDelay = 0.5f;
        public float MoveTime = 2f;

        [Header("State (Read-Only)")]
        public RobotState currentState = RobotState.Idle;

        private realvirtual.MU currentMU;
        private realvirtual.MU pendingMU;

        void OnEnable()
        {
            LeanCellEvents.OnMUCreated += HandleMUCreated;
            LeanCellEvents.OnConveyorUnblocked += HandleConveyorUnblocked;
        }

        void OnDisable()
        {
            LeanCellEvents.OnMUCreated -= HandleMUCreated;
            LeanCellEvents.OnConveyorUnblocked -= HandleConveyorUnblocked;
        }

        private void HandleMUCreated(realvirtual.MU mu)
        {
            if (currentState != RobotState.Idle)
            {
                pendingMU = mu; // only keeps the latest — earlier ones stay at source
                return;
            }
            BeginPick(mu);
        }

        /// <summary>When conveyor unblocks, try to place the held MU or pick next.</summary>
        private void HandleConveyorUnblocked()
        {
            if (currentState == RobotState.WaitingForConveyor && currentMU != null)
            {
                // Conveyor cleared — proceed to place
                currentState = RobotState.MovingToPlace;
                currentMU.transform.position = PlaceTransform.position + Vector3.up * 0.1f;
                Debug.Log($"[LeanCell] Robot: conveyor clear, moving {currentMU.name} to belt");
                Invoke(nameof(CompletePlacement), PlaceDelay);
            }
        }

        private void BeginPick(realvirtual.MU mu)
        {
            currentMU = mu;
            currentState = RobotState.Picking;
            LeanCellEvents.FireRobotCycleStart(mu);

            // Disable physics and snap to pick point
            var rb = mu.GetComponentInChildren<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }
            foreach (var col in mu.GetComponentsInChildren<Collider>())
                col.enabled = false;
            mu.transform.position = PickTransform.position;

            Debug.Log($"[LeanCell] Robot: picking {mu.name}");
            Invoke(nameof(BeginMove), GripDelay);
        }

        private void BeginMove()
        {
            if (currentMU == null) { currentState = RobotState.Idle; return; }

            // Check if conveyor can accept an MU (not blocked, no MU in transit)
            bool conveyorReady = Orchestrator == null || Orchestrator.CanAcceptConveyorMU();

            if (!conveyorReady)
            {
                // Hold MU at pick position, wait for conveyor to clear
                currentState = RobotState.WaitingForConveyor;
                Debug.Log($"[LeanCell] Robot: holding {currentMU.name}, conveyor busy");
                return;
            }

            currentState = RobotState.MovingToPlace;
            currentMU.transform.position = PlaceTransform.position + Vector3.up * 0.1f;
            Debug.Log($"[LeanCell] Robot: moved {currentMU.name} to conveyor");
            Invoke(nameof(CompletePlacement), PlaceDelay);
        }

        private void CompletePlacement()
        {
            currentState = RobotState.Placing;

            if (currentMU != null)
            {
                currentMU.transform.position = PlaceTransform.position;

                foreach (var col in currentMU.GetComponentsInChildren<Collider>())
                    col.enabled = true;
                currentMU.enabled = true;

                // Keep MU kinematic — CellOrchestrator drives conveyor motion programmatically
                var rb = currentMU.GetComponentInChildren<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                LeanCellEvents.FireRobotCycleComplete(currentMU);
                Debug.Log($"[LeanCell] Robot: placed {currentMU.name} on belt");
            }

            currentMU = null;
            currentState = RobotState.Idle;

            // Process queued MU
            if (pendingMU != null)
            {
                var next = pendingMU;
                pendingMU = null;
                BeginPick(next);
            }
        }
    }
}
