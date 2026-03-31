using UnityEngine;

namespace LeanCell
{
    /// <summary>
    /// Bridges Source.cs to takt time. Adjusts Source.Interval based on
    /// LeanCellManager parameters. v2: always Push mode (robot-fed).
    /// </summary>
    public class SourceTaktController : MonoBehaviour
    {
        private realvirtual.Source source;

        void Awake()
        {
            source = GetComponent<realvirtual.Source>();
        }

        void OnEnable()
        {
            LeanCellEvents.OnParametersChanged += HandleParametersChanged;

            if (source != null)
                source.EventMUCreated.AddListener(OnSourceCreatedMU);
        }

        void OnDisable()
        {
            LeanCellEvents.OnParametersChanged -= HandleParametersChanged;

            if (source != null)
                source.EventMUCreated.RemoveListener(OnSourceCreatedMU);
        }

        private void HandleParametersChanged()
        {
            var manager = LeanCellManager.Instance;
            if (manager == null || source == null) return;

            source.Interval = manager.CurrentTaktTime;
            source.Enabled = true;
            source.AutomaticGeneration = true;
        }

        private void OnSourceCreatedMU(realvirtual.MU mu)
        {
            Debug.Log($"[LeanCell] Source created MU: {mu.name}");
            LeanCellEvents.FireMUCreated(mu);
        }
    }
}
