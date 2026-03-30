using UnityEngine;

namespace LeanCell
{
    public enum RobotState { Idle, Picking, MovingToPlace, Placing, WaitingForConveyor }

    /// <summary>
    /// Robot picks MUs from PickTransform (near Source on floor), moves them
    /// along a visible arc to PlaceTransform (conveyor belt start), then places.
    /// Waits if conveyor already has an MU in transit.
    /// </summary>
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
        public float ArcHeight = 1.5f;

        [Header("State (Read-Only)")]
        public RobotState currentState = RobotState.Idle;

        private realvirtual.MU currentMU;
        private realvirtual.MU pendingMU;

        // Arc motion state
        private Vector3 arcStart;
        private Vector3 arcEnd;
        private float arcProgress;

        void OnEnable()
        {
            LeanCellEvents.OnMUCreated += HandleMUCreated;
            LeanCellEvents.OnConveyorUnblocked += HandleConveyorUnblocked;
        }

        void OnDisable()
        {
            CancelInvoke();
            LeanCellEvents.OnMUCreated -= HandleMUCreated;
            LeanCellEvents.OnConveyorUnblocked -= HandleConveyorUnblocked;
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

        private void HandleConveyorUnblocked()
        {
            if (currentState == RobotState.WaitingForConveyor && currentMU != null)
            {
                Debug.Log($"[LeanCell] Robot: conveyor clear, starting arc for {currentMU.name}");
                StartArcMotion();
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

            // Check if conveyor can accept
            bool conveyorReady = Orchestrator == null || Orchestrator.CanAcceptConveyorMU();
            if (!conveyorReady)
            {
                currentState = RobotState.WaitingForConveyor;
                Debug.Log($"[LeanCell] Robot: holding {currentMU.name}, conveyor busy");
                return;
            }

            StartArcMotion();
        }

        private void StartArcMotion()
        {
            currentState = RobotState.MovingToPlace;
            arcStart = PickTransform.position;
            arcEnd = PlaceTransform.position;
            arcProgress = 0f;

            // Poll position along arc
            InvokeRepeating(nameof(UpdateArcMotion), 0.02f, 0.02f);
        }

        private void UpdateArcMotion()
        {
            if (currentMU == null) { CancelInvoke(nameof(UpdateArcMotion)); return; }

            float step = 0.02f / MoveTime; // fraction per tick
            arcProgress += step;

            if (arcProgress >= 1f)
            {
                arcProgress = 1f;
                CancelInvoke(nameof(UpdateArcMotion));
                currentMU.transform.position = arcEnd;
                Debug.Log($"[LeanCell] Robot: moved {currentMU.name} to belt");
                Invoke(nameof(CompletePlacement), PlaceDelay);
                return;
            }

            // Lerp with parabolic arc on Y
            Vector3 pos = Vector3.Lerp(arcStart, arcEnd, arcProgress);
            float yArc = ArcHeight * 4f * arcProgress * (1f - arcProgress); // parabola peaking at 0.5
            pos.y += yArc;
            currentMU.transform.position = pos;
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

            if (pendingMU != null)
            {
                var next = pendingMU;
                pendingMU = null;
                BeginPick(next);
            }
        }
    }
}
