using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LeanCell
{
    /// <summary>
    /// Interactive control panel for the simulation.
    /// Attach to the Controls Canvas.
    /// </summary>
    public class ControlPanel : MonoBehaviour
    {
        [Header("Takt Time")]
        public Slider TaktTimeSlider;
        public TextMeshProUGUI TaktTimeValueText;
        public float MinTakt = 20f;
        public float MaxTakt = 60f;

        [Header("Flow Mode")]
        public Toggle FlowModeToggle; // on = Pull, off = Push
        public TextMeshProUGUI FlowModeLabel;

        [Header("Defect Rate")]
        public Slider DefectRateSlider;
        public TextMeshProUGUI DefectRateValueText;

        [Header("Scenario")]
        public TMP_Dropdown ScenarioDropdown;

        [Header("Simulation Control")]
        public Button PlayButton;
        public Button PauseButton;
        public Button ResetButton;

        [Header("Waste Overlay")]
        public Button WasteToggleButton;
        public TextMeshProUGUI WasteToggleLabel;
        public WasteVisualizer WasteVisualizer;

        void Start()
        {
            // Wire up listeners
            if (TaktTimeSlider != null)
            {
                TaktTimeSlider.minValue = MinTakt;
                TaktTimeSlider.maxValue = MaxTakt;
                TaktTimeSlider.onValueChanged.AddListener(OnTaktTimeChanged);
            }

            if (FlowModeToggle != null)
                FlowModeToggle.onValueChanged.AddListener(OnFlowModeChanged);

            if (DefectRateSlider != null)
            {
                DefectRateSlider.minValue = 0f;
                DefectRateSlider.maxValue = 0.15f;
                DefectRateSlider.onValueChanged.AddListener(OnDefectRateChanged);
            }

            if (ScenarioDropdown != null)
                ScenarioDropdown.onValueChanged.AddListener(OnScenarioSelected);

            if (PlayButton != null) PlayButton.onClick.AddListener(OnPlay);
            if (PauseButton != null) PauseButton.onClick.AddListener(OnPause);
            if (ResetButton != null) ResetButton.onClick.AddListener(OnReset);
            if (WasteToggleButton != null) WasteToggleButton.onClick.AddListener(OnWasteToggle);

            // Populate scenario dropdown
            PopulateScenarios();

            // Sync UI to current values
            SyncToManager();
        }

        private void PopulateScenarios()
        {
            if (ScenarioDropdown == null) return;
            var manager = LeanCellManager.Instance;
            if (manager == null || manager.AvailablePresets == null) return;

            ScenarioDropdown.ClearOptions();
            foreach (var preset in manager.AvailablePresets)
            {
                if (preset != null)
                    ScenarioDropdown.options.Add(new TMP_Dropdown.OptionData(preset.PresetName));
            }
            ScenarioDropdown.RefreshShownValue();
        }

        public void SyncToManager()
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;

            if (TaktTimeSlider != null)
                TaktTimeSlider.SetValueWithoutNotify(manager.CurrentTaktTime);
            if (TaktTimeValueText != null)
                TaktTimeValueText.text = $"{manager.CurrentTaktTime:F0}s";

            if (FlowModeToggle != null)
                FlowModeToggle.SetIsOnWithoutNotify(manager.CurrentFlowMode == FlowMode.Pull);
            if (FlowModeLabel != null)
                FlowModeLabel.text = manager.CurrentFlowMode == FlowMode.Pull ? "Pull" : "Push";

            if (DefectRateSlider != null)
                DefectRateSlider.SetValueWithoutNotify(manager.CurrentDefectRate);
            if (DefectRateValueText != null)
                DefectRateValueText.text = $"{manager.CurrentDefectRate * 100:F0}%";
        }

        private void OnTaktTimeChanged(float value)
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;
            manager.CurrentTaktTime = value;
            if (TaktTimeValueText != null)
                TaktTimeValueText.text = $"{value:F0}s";
            LeanCellEvents.FireParametersChanged();
        }

        private void OnFlowModeChanged(bool isPull)
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;
            manager.CurrentFlowMode = isPull ? FlowMode.Pull : FlowMode.Push;
            if (FlowModeLabel != null)
                FlowModeLabel.text = isPull ? "Pull" : "Push";
            LeanCellEvents.FireParametersChanged();
        }

        private void OnDefectRateChanged(float value)
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;
            manager.CurrentDefectRate = value;
            if (DefectRateValueText != null)
                DefectRateValueText.text = $"{value * 100:F0}%";
        }

        private void OnScenarioSelected(int index)
        {
            var manager = LeanCellManager.Instance;
            if (manager == null || manager.AvailablePresets == null) return;
            if (index >= 0 && index < manager.AvailablePresets.Length)
            {
                manager.ApplyPreset(manager.AvailablePresets[index]);
                SyncToManager();
            }
        }

        private void OnPlay()
        {
            var manager = LeanCellManager.Instance;
            if (manager != null) manager.StartSimulation();
        }

        private void OnPause()
        {
            var manager = LeanCellManager.Instance;
            if (manager != null) manager.PauseSimulation();
        }

        private void OnReset()
        {
            var manager = LeanCellManager.Instance;
            if (manager != null) manager.ResetSimulation();
        }

        private void OnWasteToggle()
        {
            if (WasteVisualizer != null)
            {
                WasteVisualizer.ToggleOverlay();
                if (WasteToggleLabel != null)
                    WasteToggleLabel.text = WasteVisualizer.OverlayActive ? "Hide Waste" : "Show Waste";
            }
        }
    }
}
