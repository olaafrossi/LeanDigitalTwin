using UnityEngine;

namespace LeanCell
{
    public class WorkStation : MonoBehaviour
    {
        [Header("Configuration")]
        public int StationIndex;
        public string StationName = "Station";
        public float CycleTime = 10f; // shorter for visible demo
        public Transform WorkPosition;
        public Transform MUSlot;

        [Header("realvirtual References")]
        public realvirtual.Sensor EntrySensor;
        public realvirtual.Fixer StationFixer;

        [Header("Visual")]
        public Color StationColor = Color.white;

        [Header("State (Read-Only)")]
        [SerializeField] private bool isProcessing;
        [SerializeField] private bool hasMU;
        [SerializeField] private bool isOccupiedByWorker;
        [SerializeField] private float idleStartTime;
        [SerializeField] private float processStartTime;
        [SerializeField] private float processProgress; // 0 to 1

        public bool IsProcessing => isProcessing;
        public bool HasMU => hasMU;
        public bool IsOccupiedByWorker => isOccupiedByWorker;
        public float IdleStartTime => idleStartTime;
        public float ProcessProgress => processProgress;

        private realvirtual.MU currentMU;
        private GameObject progressBar;
        private Transform progressFill;
        private Renderer stationRenderer;

        void Start()
        {
            idleStartTime = Time.time;
            stationRenderer = GetComponentInChildren<Renderer>();

            // Set station color
            if (stationRenderer != null)
            {
                stationRenderer.material.color = StationColor;
            }

            // Create a simple progress bar above the station
            CreateProgressBar();

            // Create floating station label
            CreateStationLabel();

            // Create status dot (shows station state)
            CreateStatusDot();
        }

        void Update()
        {
            if (EntrySensor != null)
                hasMU = EntrySensor.Occupied;

            // Update progress
            if (isProcessing)
            {
                processProgress = (Time.time - processStartTime) / CycleTime;
                processProgress = Mathf.Clamp01(processProgress);
                UpdateProgressBar(processProgress);
            }
            else
            {
                processProgress = 0;
                UpdateProgressBar(0);
            }

            // Update status dot color
            UpdateStatusDot();

            // Idle waste detection
            if (hasMU && !isProcessing && !isOccupiedByWorker)
            {
                if (idleStartTime <= 0)
                {
                    idleStartTime = Time.time;
                    LeanCellEvents.FireStationIdle(StationIndex);
                }
            }
        }

        private void CreateProgressBar()
        {
            // Background bar (dark)
            progressBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            progressBar.name = $"ProgressBar_{StationName}";
            progressBar.transform.SetParent(transform);
            progressBar.transform.localPosition = new Vector3(0, 2.5f, 0);
            progressBar.transform.localScale = new Vector3(1.5f, 0.15f, 0.15f);
            Destroy(progressBar.GetComponent<Collider>());

            var bgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            bgMat.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            progressBar.GetComponent<Renderer>().material = bgMat;

            // Fill bar (colored)
            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Fill";
            fill.transform.SetParent(progressBar.transform);
            fill.transform.localPosition = new Vector3(-0.5f, 0, -0.01f); // start from left
            fill.transform.localScale = new Vector3(0, 1.1f, 1.1f); // initially zero width
            Destroy(fill.GetComponent<Collider>());

            var fillMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            fillMat.color = WasteColors.ValueAdd; // green
            fill.GetComponent<Renderer>().material = fillMat;

            progressFill = fill.transform;
            progressBar.SetActive(false); // hidden until processing starts
        }

        private void UpdateProgressBar(float progress)
        {
            if (progressBar == null) return;

            if (progress > 0)
            {
                progressBar.SetActive(true);
                if (progressFill != null)
                {
                    // Scale the fill bar from 0 to 1 on X axis
                    var s = progressFill.localScale;
                    s.x = progress;
                    progressFill.localScale = s;

                    // Move it so it grows from the left
                    var p = progressFill.localPosition;
                    p.x = -0.5f + (progress * 0.5f);
                    progressFill.localPosition = p;
                }
            }
            else
            {
                progressBar.SetActive(false);
            }
        }

        public void SetCycleTime(float newCycleTime)
        {
            CycleTime = newCycleTime;
        }

        public void WorkerArrived(WorkerController worker)
        {
            isOccupiedByWorker = true;
        }

        public void WorkerLeft()
        {
            isOccupiedByWorker = false;
        }

        public void StartProcessing(realvirtual.MU mu)
        {
            currentMU = mu;
            isProcessing = true;
            processStartTime = Time.time;
            idleStartTime = 0;
            LeanCellEvents.FireProcessStart(StationIndex, mu);
        }

        public void CompleteProcessing()
        {
            float actualTime = Time.time - processStartTime;
            isProcessing = false;

            var manager = LeanCellManager.Instance;
            if (manager != null && Random.value < manager.CurrentDefectRate)
            {
                if (currentMU != null)
                {
                    // Visual: turn MU red for defect
                    var r = currentMU.GetComponentInChildren<Renderer>();
                    if (r != null) r.material.color = WasteColors.Defects;

                    LeanCellEvents.FireMUDefective(currentMU, StationIndex);
                }
            }

            LeanCellEvents.FireProcessComplete(StationIndex, currentMU, actualTime);
            currentMU = null;
            idleStartTime = Time.time;
        }

        public realvirtual.MU GetCurrentMU() => currentMU;

        // === Visual elements ===

        private GameObject statusDot;
        private Renderer statusDotRenderer;
        private Material statusDotMat;

        private void CreateStationLabel()
        {
            // 3D text label floating above the workbench
            var labelGO = new GameObject($"Label_{StationName}");
            labelGO.transform.SetParent(transform);
            labelGO.transform.localPosition = new Vector3(0, 2f, 0);

            var tm = labelGO.AddComponent<TextMesh>();
            tm.text = StationName;
            tm.fontSize = 48;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            // Make it face the camera
            var billboard = labelGO.AddComponent<BillboardText>();
        }

        private void CreateStatusDot()
        {
            statusDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            statusDot.name = $"StatusDot_{StationName}";
            statusDot.transform.SetParent(transform);
            statusDot.transform.localPosition = new Vector3(0, 1.5f, 0);
            statusDot.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            Destroy(statusDot.GetComponent<Collider>());

            statusDotMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            statusDotMat.color = Color.gray;
            statusDotRenderer = statusDot.GetComponent<Renderer>();
            statusDotRenderer.material = statusDotMat;
        }

        private void UpdateStatusDot()
        {
            if (statusDotRenderer == null) return;

            Color c;
            if (isProcessing)
                c = WasteColors.ValueAdd; // green = working
            else if (isOccupiedByWorker)
                c = Color.cyan; // worker here but not processing yet
            else if (hasMU)
                c = WasteColors.Waiting; // red = MU waiting, no worker
            else
                c = Color.gray; // idle, nothing happening

            statusDotMat.color = c;
        }
    }

    /// <summary>
    /// Simple billboard that makes a TextMesh always face the camera.
    /// </summary>
    public class BillboardText : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Camera.main != null)
            {
                transform.LookAt(Camera.main.transform);
                transform.Rotate(0, 180, 0); // flip so text isn't mirrored
            }
        }
    }
}
