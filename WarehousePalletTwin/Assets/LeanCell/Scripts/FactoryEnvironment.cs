using UnityEngine;

namespace LeanCell
{
    /// <summary>
    /// Sets up factory environment visuals: dark floor, back wall with title text.
    /// Attach to any GameObject in the scene.
    /// </summary>
    public class FactoryEnvironment : MonoBehaviour
    {
        [Header("Floor")]
        public GameObject Floor;
        public Color FloorColor = new Color(0.06f, 0.06f, 0.06f);

        [Header("Back Wall")]
        public float WallWidth = 24f;
        public float WallHeight = 4f;
        public float WallZ = 7f;
        public float WallCenterX = 5f;
        public Color WallColor = new Color(0.18f, 0.18f, 0.2f);
        public string TitleText = "Lean Digital Twin w/ 7 Waste Detection";

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

            SetFloorColor();
            CreateBackWall();
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
    }
}
