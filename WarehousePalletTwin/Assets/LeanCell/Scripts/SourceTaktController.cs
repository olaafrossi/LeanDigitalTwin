using UnityEngine;

namespace LeanCell
{
    /// <summary>
    /// Bridges Source.cs to takt time. Adjusts Source.Interval based on
    /// LeanCellManager parameters. In Pull mode, disables auto-generation.
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

            // Subscribe to source creation events to forward to our event bus
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

            if (manager.CurrentFlowMode == FlowMode.Push)
            {
                source.Interval = manager.CurrentTaktTime;
                source.Enabled = true;
            }
            else // Pull mode
            {
                source.Interval = 0;
                source.Enabled = false;
            }
        }

        private void OnSourceCreatedMU(realvirtual.MU mu)
        {
            LeanCellEvents.FireMUCreated(mu);
        }

        /// <summary>
        /// Called by workers in Pull mode to request a new MU.
        /// </summary>
        public void RequestMU()
        {
            if (source != null)
                source.Generate();
        }
    }
}
