using UnityEngine;

namespace LeanCell
{
    /// <summary>
    /// Sets up factory environment visuals: dark floor, back wall with title text.
    /// Attach to any GameObject in the scene.
    /// </summary>
    public class FactoryEnvironment : MonoBehaviour
    {
        [Header("MU Template")]
        public GameObject MUTemplate;
        public Color MUColor = new Color(0.15f, 0.4f, 0.8f);

        [Header("Floor")]
        public GameObject Floor;
        public Color FloorColor = new Color(0.06f, 0.06f, 0.06f);

        [Header("Back Wall")]
        public float WallWidth = 24f;
        public float WallHeight = 4f;
        public float WallZ = 500f;
        public float WallCenterX = 5f;
        public Color WallColor = new Color(0.18f, 0.18f, 0.2f);
        public string TitleText = "Lean Digital Twin w/ 7 Waste Detection";

        [Header("Worker Status Display")]
        public WorkerController[] Workers;

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

            SetMUColor();
            SetFloorColor();
            CreateBackWall();
            CreateSideWall();
            CreateWorkerStatusBoard();
            InvokeRepeating(nameof(UpdateWorkerStatus), 1f, 0.5f);
        }

        private void SetMUColor()
        {
            if (MUTemplate == null) return;
            var renderer = MUTemplate.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = MUColor;
                mat.SetFloat("_Smoothness", 0.6f);
                renderer.material = mat;
            }
        }

        private void SetFloorColor()
        {
            if (Floor == null) return;
            var renderer = Floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = FloorColor;
                mat.SetFloat("_Smoothness", 0.4f);
                mat.SetFloat("_Metallic", 0.0f);
                renderer.material = mat;
            }
        }

        private void CreateBackWall()
        {
            // Wall panel
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "BackWall";
            wall.transform.SetParent(transform);
            wall.transform.position = new Vector3(WallCenterX, WallHeight / 2f, WallZ);
            wall.transform.localScale = new Vector3(WallWidth, WallHeight, 0.1f);
            Destroy(wall.GetComponent<Collider>());

            var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            wallMat.color = WallColor;
            wallMat.SetFloat("_Smoothness", 0.15f);
            wall.GetComponent<Renderer>().material = wallMat;

            // Title text
            var textGO = new GameObject("TitleText");
            textGO.transform.SetParent(wall.transform);
            textGO.transform.localPosition = new Vector3(0, 0.3f, -0.6f);
            textGO.transform.localRotation = Quaternion.identity;

            var tm = textGO.AddComponent<TextMesh>();
            tm.text = TitleText;
            tm.fontSize = 64;
            tm.characterSize = 0.04f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.9f, 0.9f, 0.95f);
            tm.fontStyle = FontStyle.Bold;

            // Accent stripe below the text
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "AccentStripe";
            stripe.transform.SetParent(wall.transform);
            stripe.transform.localPosition = new Vector3(0, 0.15f, -0.6f);
            stripe.transform.localScale = new Vector3(0.7f, 0.005f, 1f);
            Destroy(stripe.GetComponent<Collider>());

            var stripeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            stripeMat.color = new Color(0f, 0.6f, 0.9f); // blue accent
            stripe.GetComponent<Renderer>().material = stripeMat;
        }

        private TextMesh[] workerStatusTexts;

        private void CreateWorkerStatusBoard()
        {
            if (Workers == null || Workers.Length == 0) return;

            // Board background on the back wall, left side
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "WorkerStatusBoard";
            board.transform.SetParent(transform);
            board.transform.position = new Vector3(WallCenterX - 4f, WallHeight * 0.7f, WallZ - 0.06f);
            board.transform.localScale = new Vector3(5f, 2f, 0.05f);
            Destroy(board.GetComponent<Collider>());

            var boardMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            boardMat.color = new Color(0.1f, 0.1f, 0.12f);
            boardMat.SetFloat("_Smoothness", 0.1f);
            board.GetComponent<Renderer>().material = boardMat;

            // Board title
            var titleGO = new GameObject("BoardTitle");
            titleGO.transform.SetParent(board.transform);
            titleGO.transform.localPosition = new Vector3(0, 0.38f, -1.1f);
            var titleTM = titleGO.AddComponent<TextMesh>();
            titleTM.text = "WORKER STATUS";
            titleTM.fontSize = 48;
            titleTM.characterSize = 0.025f;
            titleTM.anchor = TextAnchor.MiddleCenter;
            titleTM.color = new Color(0f, 0.7f, 0.9f);
            titleTM.fontStyle = FontStyle.Bold;

            // Worker rows
            workerStatusTexts = new TextMesh[Workers.Length];
            string[] stationNames = { "Prep", "Assembly", "QC" };
            for (int i = 0; i < Workers.Length; i++)
            {
                var rowGO = new GameObject($"WorkerRow_{i}");
                rowGO.transform.SetParent(board.transform);
                rowGO.transform.localPosition = new Vector3(0, 0.15f - i * 0.22f, -1.1f);

                var tm = rowGO.AddComponent<TextMesh>();
                tm.fontSize = 40;
                tm.characterSize = 0.02f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.white;
                string station = i < stationNames.Length ? stationNames[i] : "?";
                tm.text = $"W{i + 1} [{station}]  --  Idle";
                workerStatusTexts[i] = tm;
            }
        }

        private void UpdateWorkerStatus()
        {
            if (Workers == null || workerStatusTexts == null) return;

            string[] stationNames = { "Prep", "Assembly", "QC" };
            for (int i = 0; i < Workers.Length && i < workerStatusTexts.Length; i++)
            {
                if (Workers[i] == null || workerStatusTexts[i] == null) continue;

                var w = Workers[i];
                string station = i < stationNames.Length ? stationNames[i] : "?";
                string state;
                string waste;
                Color color;

                switch (w.CurrentState)
                {
                    case WorkerState.Idle:
                        state = "IDLE";
                        waste = "Waiting (Muda #3)";
                        color = new Color(1f, 0.3f, 0.3f);
                        break;
                    case WorkerState.WalkingToPickup:
                    case WorkerState.WalkingToStation:
                    case WorkerState.WalkingToSink:
                        state = "WALKING";
                        waste = "Motion (Muda #6)";
                        color = new Color(1f, 0.8f, 0.2f);
                        break;
                    case WorkerState.PickingUp:
                    case WorkerState.Placing:
                    case WorkerState.PickingResult:
                        state = "HANDLING";
                        waste = "Transport (Muda #1)";
                        color = new Color(1f, 0.65f, 0.2f);
                        break;
                    case WorkerState.Processing:
                        state = "PROCESSING";
                        waste = "Value-Add";
                        color = new Color(0.2f, 0.9f, 0.3f);
                        break;
                    default:
                        state = w.CurrentState.ToString();
                        waste = "--";
                        color = Color.gray;
                        break;
                }

                workerStatusTexts[i].text = $"W{i + 1} [{station}]  {state,-12} {waste}";
                workerStatusTexts[i].color = color;
            }
        }

        private void CreateSideWall()
        {
            // Side wall at the far end (after QC station / exit conveyor)
            float sideX = 14f;
            var sideWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideWall.name = "SideWall";
            sideWall.transform.SetParent(transform);
            sideWall.transform.position = new Vector3(sideX, WallHeight / 2f, WallZ / 2f);
            sideWall.transform.localScale = new Vector3(0.1f, WallHeight, WallZ);
            Destroy(sideWall.GetComponent<Collider>());

            var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            wallMat.color = WallColor;
            wallMat.SetFloat("_Smoothness", 0.15f);
            sideWall.GetComponent<Renderer>().material = wallMat;
        }
    }
}
