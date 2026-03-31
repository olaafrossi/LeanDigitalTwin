using UnityEngine;

namespace LeanCell
{
    public enum RobotState { Idle, MovingToPick, Gripping, MovingToPlace, Releasing, WaitingAtHome }

    /// <summary>
    /// Controls the Fanuc robot arm by directly overriding axis Drive positions.
    /// Smoothly interpolates between poses using InvokeRepeating.
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
        public Transform ToolTip;

        [Header("Poses — axis angles in degrees")]
        public float[] HomePose  = {   0,   0,   0, 0, 0, 0 };
        public float[] PickPose  = {  80, -30,  30, 0, -30, 0 };
        public float[] PlacePose = { -45, -20,  20, 0, -20, 0 };

        [Header("Motion")]
        public float MoveSpeed = 60f; // degrees per second per axis
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
        private float[] currentAngles;
        private float[] targetAngles;

        void OnEnable()
        {
            drives = new[] { Axis1, Axis2, Axis3, Axis4, Axis5, Axis6 };
            currentAngles = new float[6];
            targetAngles = new float[6];

            // Enable position override on all axes
            foreach (var d in drives)
            {
                if (d != null)
                {
                    d.PositionOverwrite = true;
                    d.PositionOverwriteValue = 0;
                }
            }

            LeanCellEvents.OnMUCreated += HandleMUCreated;
            LeanCellEvents.OnConveyorUnblocked += HandleConveyorUnblocked;
            Debug.Log("[LeanCell] Robot: initialized with position override");
        }

        void OnDisable()
        {
            CancelInvoke();
            foreach (var d in drives)
                if (d != null) d.PositionOverwrite = false;
            LeanCellEvents.OnMUCreated -= HandleMUCreated;
            LeanCellEvents.OnConveyorUnblocked -= HandleConveyorUnblocked;
        }

        private void HandleMUCreated(realvirtual.MU mu)
        {
            if (currentState != RobotState.Idle && currentState != RobotState.WaitingAtHome)
            {
                pendingMU = mu;
                return;
            }
            if (currentState == RobotState.WaitingAtHome)
            {
                pendingMU = mu;
                return;
            }
            BeginPick(mu);
        }

        private void HandleConveyorUnblocked()
        {
            if (currentState == RobotState.WaitingAtHome && currentMU != null)
            {
                Debug.Log("[LeanCell] Robot: sensor clear, moving to place");
                currentState = RobotState.MovingToPlace;
                StartMoveTo(PlacePose);
            }
        }

        // === State Machine ===

        private void BeginPick(realvirtual.MU mu)
        {
            currentMU = mu;
            currentState = RobotState.MovingToPick;
            LeanCellEvents.FireRobotCycleStart(mu);

            var rb = mu.GetComponentInChildren<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }
            foreach (var col in mu.GetComponentsInChildren<Collider>())
                col.enabled = false;

            Debug.Log($"[LeanCell] Robot: moving to pick {mu.name}");
            StartMoveTo(PickPose);
        }

        private void OnMoveComplete()
        {
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
            currentMU.transform.SetParent(ToolTip);
            currentMU.transform.localPosition = Vector3.zero;
            currentMU.transform.localRotation = Quaternion.identity;
            Debug.Log($"[LeanCell] Robot: gripped {currentMU.name}");
        }

        private void AfterGrip()
        {
            bool conveyorReady = Orchestrator == null || Orchestrator.CanAcceptConveyorMU();
            if (!conveyorReady)
            {
                Debug.Log("[LeanCell] Robot: sensor occupied, going home to wait");
                currentState = RobotState.WaitingAtHome;
                StartMoveTo(HomePose);
                return;
            }

            Debug.Log("[LeanCell] Robot: moving to place");
            currentState = RobotState.MovingToPlace;
            StartMoveTo(PlacePose);
        }

        private void ReleaseMU()
        {
            if (currentMU != null)
            {
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
            currentState = RobotState.Idle;
            StartMoveTo(HomePose);

            if (pendingMU != null)
            {
                var next = pendingMU;
                pendingMU = null;
                BeginPick(next);
            }
        }

        // === Direct Position Override Motion ===

        private void StartMoveTo(float[] target)
        {
            CancelInvoke(nameof(StepMotion));
            System.Array.Copy(target, targetAngles, 6);
            InvokeRepeating(nameof(StepMotion), 0.02f, 0.02f);
        }

        private void StepMotion()
        {
            float maxRemaining = 0f;
            float step = MoveSpeed * 0.02f; // degrees per tick

            for (int i = 0; i < drives.Length; i++)
            {
                if (drives[i] == null) continue;

                float diff = targetAngles[i] - currentAngles[i];
                float absDiff = Mathf.Abs(diff);
                maxRemaining = Mathf.Max(maxRemaining, absDiff);

                if (absDiff <= step)
                    currentAngles[i] = targetAngles[i];
                else
                    currentAngles[i] += Mathf.Sign(diff) * step;

                drives[i].PositionOverwriteValue = currentAngles[i];
            }

            // All axes arrived
            if (maxRemaining <= MoveSpeed * 0.02f)
            {
                CancelInvoke(nameof(StepMotion));

                // Only fire completion for pick/place moves
                if (currentState == RobotState.MovingToPick || currentState == RobotState.MovingToPlace)
                    OnMoveComplete();
            }
        }
    }
}
