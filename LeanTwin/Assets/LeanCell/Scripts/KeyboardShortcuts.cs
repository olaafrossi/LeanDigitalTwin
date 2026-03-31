using UnityEngine;

namespace LeanCell
{
    /// <summary>
    /// Keyboard shortcuts for smooth demo flow.
    /// Space=play/pause, R=reset, W=waste toggle, 1-4=presets, Escape=dismiss summary.
    /// </summary>
    public class KeyboardShortcuts : MonoBehaviour
    {
        public ControlPanel ControlPanel;
        public WasteVisualizer WasteVisualizer;
        public SessionSummary SessionSummary;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var manager = LeanCellManager.Instance;
                if (manager != null)
                {
                    if (manager.IsRunning)
                        manager.PauseSimulation();
                    else
                        manager.ResumeSimulation();
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                var manager = LeanCellManager.Instance;
                if (manager != null) manager.ResetSimulation();
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                if (WasteVisualizer != null)
                    WasteVisualizer.ToggleOverlay();
            }

            // Scenario presets 1-4
            if (Input.GetKeyDown(KeyCode.Alpha1) && ControlPanel != null)
                ControlPanel.OnScenarioSelected(0);
            if (Input.GetKeyDown(KeyCode.Alpha2) && ControlPanel != null)
                ControlPanel.OnScenarioSelected(1);
            if (Input.GetKeyDown(KeyCode.Alpha3) && ControlPanel != null)
                ControlPanel.OnScenarioSelected(2);
            if (Input.GetKeyDown(KeyCode.Alpha4) && ControlPanel != null)
                ControlPanel.OnScenarioSelected(3);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (SessionSummary != null)
                    SessionSummary.Dismiss();
            }
        }
    }
}
