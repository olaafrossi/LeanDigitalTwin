using UnityEngine;

namespace LeanCell
{
    /// <summary>
    /// WorkStation with Invoke-based polling for realvirtual compatibility.
    /// </summary>
    public class WorkStation : MonoBehaviour
    {
        [Header("Configuration")]
        public int StationIndex;
        public string StationName = "Station";
        public float CycleTime = 10f;
        public Transform WorkPosition;
        public Transform MUSlot;
        public Transform OutputPoint;

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
        [SerializeField] private float processProgress;
        [SerializeField] private bool hasOutputMU;

        public bool IsProcessing => isProcessing;
        public bool HasMU => hasMU;
        public bool HasOutputMU => hasOutputMU;
        public bool IsOccupiedByWorker => isOccupiedByWorker;
        public float IdleStartTime => idleStartTime;
        public float ProcessProgress => processProgress;

        private realvirtual.MU currentMU;
        private GameObject progressBar;
        private Transform progressFill;
        private Renderer stationRenderer;
        private bool initialized;

        void OnEnable()
        {
            Invoke(nameof(Initialize), 0.3f);
        }

        void OnDisable()
        {
            CancelInvoke();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            idleStartTime = Time.time;
            stationRenderer = GetComponentInChildren<Renderer>();

            if (stationRenderer != null)
                stationRenderer.material.color = StationColor;

            CreateProgressBar();
            CreateStationLabel();
            CreateStatusDot();

            // Start polling (replaces Update)
            InvokeRepeating(nameof(PollStation), 0.5f, 0.1f);
        }

        /// <summary>Replaces Update() — polls sensor, updates progress bar and status dot.</summary>
        private void PollStation()
        {
            if (EntrySensor != null)
                hasMU = EntrySensor.Occupied;

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
            progressBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            progressBar.name = $"ProgressBar_{StationName}";
            progressBar.transform.SetParent(transform);
            progressBar.transform.localPosition = new Vector3(0, 2.5f, 0);
            progressBar.transform.localScale = new Vector3(1.5f, 0.15f, 0.15f);
            Destroy(progressBar.GetComponent<Collider>());

            var bgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            bgMat.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            progressBar.GetComponent<Renderer>().material = bgMat;

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Fill";
            fill.transform.SetParent(progressBar.transform);
            fill.transform.localPosition = new Vector3(-0.5f, 0, -0.01f);
            fill.transform.localScale = new Vector3(0, 1.1f, 1.1f);
            Destroy(fill.GetComponent<Collider>());

            var fillMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            fillMat.color = WasteColors.ValueAdd;
            fill.GetComponent<Renderer>().material = fillMat;

            progressFill = fill.transform;
            progressBar.SetActive(false);
        }

        private void UpdateProgressBar(float progress)
        {
            if (progressBar == null) return;

            if (progress > 0)
            {
                progressBar.SetActive(true);
                if (progressFill != null)
                {
                    var s = progressFill.localScale;
                    s.x = progress;
                    progressFill.localScale = s;

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
                    var r = currentMU.GetComponentInChildren<Renderer>();
                    if (r != null) r.material.color = WasteColors.Defects;

                    LeanCellEvents.FireMUDefective(currentMU, StationIndex);
                }
            }

            LeanCellEvents.FireProcessComplete(StationIndex, currentMU, actualTime);

            if (OutputPoint != null && currentMU != null)
            {
                currentMU.transform.position = OutputPoint.position + Vector3.up * 0.5f;
                hasOutputMU = true;
            }

            currentMU = null;
            idleStartTime = Time.time;
        }

        public realvirtual.MU GetCurrentMU() => currentMU;

        public void ClearOutput()
        {
            hasOutputMU = false;
        }

        // === Visual elements ===

        private GameObject statusDot;
        private Renderer statusDotRenderer;
        private Material statusDotMat;

        private void CreateStationLabel()
        {
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

            labelGO.AddComponent<BillboardText>();
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
                c = WasteColors.ValueAdd;
            else if (isOccupiedByWorker)
                c = Color.cyan;
            else if (hasMU)
                c = WasteColors.Waiting;
            else
                c = Color.gray;

            statusDotMat.color = c;
        }
    }

    /// <summary>
    /// Billboard that makes a TextMesh face the camera. Uses Invoke for realvirtual compat.
    /// </summary>
    public class BillboardText : MonoBehaviour
    {
        void OnEnable()
        {
            InvokeRepeating(nameof(FaceCamera), 0.1f, 0.1f);
        }

        void OnDisable()
        {
            CancelInvoke();
        }

        private void FaceCamera()
        {
            if (Camera.main != null)
            {
                transform.LookAt(Camera.main.transform);
                transform.Rotate(0, 180, 0);
            }
        }
    }
}
