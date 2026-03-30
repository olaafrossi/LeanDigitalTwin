using UnityEngine;

namespace LeanCell
{
    public enum RobotState { Idle, Picking, MovingToPlace, Placing }

    public class RobotController : MonoBehaviour
    {
        [Header("Waypoints")]
        public Transform PickTransform;
        public Transform PlaceTransform;

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
        }

        void OnDisable()
        {
            LeanCellEvents.OnMUCreated -= HandleMUCreated;
        }

        private void HandleMUCreated(realvirtual.MU mu)
        {
            if (currentState != RobotState.Idle)
            {
                pendingMU = mu;
                return;
            }
            BeginPick(mu);
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

            // Schedule next step via Invoke
            Invoke(nameof(BeginMove), GripDelay);
        }

        private void BeginMove()
        {
            if (currentMU == null) { currentState = RobotState.Idle; return; }

            currentState = RobotState.MovingToPlace;
            // Teleport MU to place position (arc animation deferred to later polish)
            currentMU.transform.position = PlaceTransform.position + Vector3.up * 0.1f;

            Debug.Log($"[LeanCell] Robot: moved {currentMU.name} to conveyor");

            Invoke(nameof(CompletePlacement), PlaceDelay);
        }

        private void CompletePlacement()
        {
            currentState = RobotState.Placing;

            if (currentMU != null)
            {
                // Place MU just above the PlaceTransform (should be on conveyor surface)
                currentMU.transform.position = PlaceTransform.position;

                // Re-enable in correct order: colliders first, then MU component, then physics
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
                Debug.Log($"[LeanCell] Robot: placed {currentMU.name} at {PlaceTransform.position}");
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
