using UnityEngine;

namespace LeanCell
{
    public enum RobotState { Idle, MovingToPick, Gripping, MovingToPlace, Releasing, WaitingForConveyor }

    /// <summary>
    /// Controls the Fanuc robot arm via realvirtual axis Drives.
    /// Swings between PickPose and PlacePose, parenting the MU to the tool tip.
    /// </summary>
    public class RobotController : MonoBehaviour
    {
        [Header("Robot Axis Drives (assign 6)")]
        public realvirtual.Drive Axis1;
        public realvirtual.Drive Axis2;
        public realvirtual.Drive Axis3;
        public realvirtual.Drive Axis4;
        public realvirtual.Drive Axis5;
        public realvirtual.Drive Axis6;

        [Header("Tool Tip (parent MU here)")]
        public Transform ToolTip; // GripperRobot or Axis6 end-effector

        [Header("Poses — axis angles in degrees")]
        public float[] HomePose  = {   0,   0,   0, 0, 0, 0 };
        public float[] PickPose  = {  80, -30,  30, 0, -30, 0 };
        public float[] PlacePose = { -45, -20,  20, 0, -20, 0 };

        [Header("Speeds")]
        public float AxisSpeed = 40f;
        public float GripDelay = 0.5f;
        public float PlaceDelay = 0.5f;

        [Header("References")]
        public CellOrchestrator Orchestrator;
        public Transform PlaceTransform;

        [Header("State (Read-Only)")]
        public RobotState currentState = RobotState.Idle;

        private realvirtual.MU currentMU;
        private realvirtual.MU pendingMU;
        private realvirtual.Drive[] drives;

        void OnEnable()
        {
            LeanCellEvents.OnMUCreated += HandleMUCreated;
            LeanCellEvents.OnConveyorUnblocked += HandleConveyorUnblocked;
            Invoke(nameof(Initialize), 0.5f);
        }

        void OnDisable()
        {
            CancelInvoke();
            LeanCellEvents.OnMUCreated -= HandleMUCreated;
            LeanCellEvents.OnConveyorUnblocked -= HandleConveyorUnblocked;
        }

        private void Initialize()
        {
            drives = new[] { Axis1, Axis2, Axis3, Axis4, Axis5, Axis6 };
            GoToPose(HomePose);
            Debug.Log("[LeanCell] Robot: initialized, at home pose");
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
                Debug.Log($"[LeanCell] Robot: conveyor clear, moving to place");
                GoToPose(PlacePose);
                currentState = RobotState.MovingToPlace;
                InvokeRepeating(nameof(PollArrival), 0.1f, 0.1f);
            }
        }

        // === Pick-Place State Machine ===

        private void BeginPick(realvirtual.MU mu)
        {
            currentMU = mu;
            currentState = RobotState.MovingToPick;
            LeanCellEvents.FireRobotCycleStart(mu);

            // Disable MU physics while robot handles it
            var rb = mu.GetComponentInChildren<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }
            foreach (var col in mu.GetComponentsInChildren<Collider>())
                col.enabled = false;

            Debug.Log($"[LeanCell] Robot: moving to pick {mu.name}");
            GoToPose(PickPose);
            InvokeRepeating(nameof(PollArrival), 0.1f, 0.1f);
        }

        /// <summary>Polls until all axes reach target, then proceeds to next step.</summary>
        private void PollArrival()
        {
            if (!AllAxesAtTarget()) return;
            CancelInvoke(nameof(PollArrival));

            switch (currentState)
            {
                case RobotState.MovingToPick:
                    currentState = RobotState.Gripping;
                    GripMU();
                    Invoke(nameof(AfterGrip), GripDelay);
                    break;

                case RobotState.MovingToPlace:
                    currentState = RobotState.Releasing;
                    Invoke(nameof(ReleaseMU), PlaceDelay);
                    break;
            }
        }

        private void GripMU()
        {
            if (currentMU == null) return;
            // Parent MU to tool tip so it follows the arm
            currentMU.transform.SetParent(ToolTip);
            currentMU.transform.localPosition = Vector3.zero;
            currentMU.transform.localRotation = Quaternion.identity;
            Debug.Log($"[LeanCell] Robot: gripped {currentMU.name}");
        }

        private void AfterGrip()
        {
            // Always move to place — MUs queue on the belt
            Debug.Log("[LeanCell] Robot: moving to place");
            GoToPose(PlacePose);
            currentState = RobotState.MovingToPlace;
            InvokeRepeating(nameof(PollArrival), 0.1f, 0.1f);
        }

        private void ReleaseMU()
        {
            if (currentMU != null)
            {
                // Unparent and place at belt position
                currentMU.transform.SetParent(null);
                currentMU.transform.position = PlaceTransform.position;
                currentMU.transform.rotation = Quaternion.identity;

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

            // Return to home, then go idle
            GoToPose(HomePose);
            currentState = RobotState.Idle;

            // Process next MU
            if (pendingMU != null)
            {
                var next = pendingMU;
                pendingMU = null;
                BeginPick(next);
            }
        }

        // === Drive Helpers ===

        private void GoToPose(float[] angles)
        {
            if (drives == null) return;
            for (int i = 0; i < drives.Length && i < angles.Length; i++)
            {
                if (drives[i] == null) continue;
                drives[i].TargetSpeed = AxisSpeed;
                drives[i].TargetPosition = angles[i];
                drives[i].TargetStartMove = true;
            }
        }

        private bool AllAxesAtTarget()
        {
            if (drives == null) return true;
            foreach (var d in drives)
            {
                if (d != null && !d.IsAtTarget) return false;
            }
            return true;
        }
    }
}
