using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LeanCell
{
    /// <summary>
    /// Right Command Rail — all interactive controls for the simulation.
    /// v2: station cycle sliders, scenario buttons (not dropdown), no flow mode toggle.
    /// </summary>
    public class ControlPanel : MonoBehaviour
    {
        [Header("Line Rate (Takt Time)")]
        public Slider TaktTimeSlider;
        public TextMeshProUGUI TaktTimeValueText;
        public float MinTakt = 20f;
        public float MaxTakt = 120f;

        [Header("Station Cycle Time Sliders")]
        public Slider[] StationCycleSliders; // 3 sliders, one per station
        public TextMeshProUGUI[] StationCycleValueTexts; // 3 labels

        [Header("Defect Rate")]
        public Slider DefectRateSlider;
        public TextMeshProUGUI DefectRateValueText;

        [Header("Scenario Buttons")]
        public Button[] ScenarioButtons; // 4 buttons
        public Color ActiveScenarioColor = Color.white;
        public Color InactiveScenarioColor = new Color(0.6f, 0.6f, 0.6f);

        [Header("Simulation Control")]
        public Button PlayButton;
        public Button PauseButton;
        public Button ResetButton;

        [Header("Waste Overlay")]
        public Button WasteToggleButton;
        public TextMeshProUGUI WasteToggleLabel;
        public WasteVisualizer WasteVisualizer;

        private int activeScenarioIndex = -1;

        void Start()
        {
            // Takt time slider
            if (TaktTimeSlider != null)
            {
                TaktTimeSlider.minValue = MinTakt;
                TaktTimeSlider.maxValue = MaxTakt;
                TaktTimeSlider.onValueChanged.AddListener(OnTaktTimeChanged);
            }

            // Station cycle sliders
            if (StationCycleSliders != null)
            {
                for (int i = 0; i < StationCycleSliders.Length; i++)
                {
                    if (StationCycleSliders[i] != null)
                    {
                        StationCycleSliders[i].minValue = 10f;
                        StationCycleSliders[i].maxValue = 90f;
                        int stationIndex = i; // capture for closure
                        StationCycleSliders[i].onValueChanged.AddListener(
                            (value) => OnStationCycleChanged(stationIndex, value));
                    }
                }
            }

            // Defect rate slider
            if (DefectRateSlider != null)
            {
                DefectRateSlider.minValue = 0f;
                DefectRateSlider.maxValue = 0.35f;
                DefectRateSlider.onValueChanged.AddListener(OnDefectRateChanged);
            }

            // Scenario buttons
            if (ScenarioButtons != null)
            {
                for (int i = 0; i < ScenarioButtons.Length; i++)
                {
                    if (ScenarioButtons[i] != null)
                    {
                        int idx = i;
                        ScenarioButtons[i].onClick.AddListener(() => OnScenarioSelected(idx));
                    }
                }
            }

            // Sim control buttons
            if (PlayButton != null) PlayButton.onClick.AddListener(OnPlay);
            if (PauseButton != null) PauseButton.onClick.AddListener(OnPause);
            if (ResetButton != null) ResetButton.onClick.AddListener(OnReset);
            if (WasteToggleButton != null) WasteToggleButton.onClick.AddListener(OnWasteToggle);

            SyncToManager();
        }

        public void SyncToManager()
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;

            if (TaktTimeSlider != null)
                TaktTimeSlider.SetValueWithoutNotify(manager.CurrentTaktTime);
            if (TaktTimeValueText != null)
                TaktTimeValueText.text = $"{manager.CurrentTaktTime:F0}s";

            // Sync station sliders
            if (StationCycleSliders != null)
            {
                for (int i = 0; i < StationCycleSliders.Length && i < manager.Stations.Length; i++)
                {
                    if (StationCycleSliders[i] != null && manager.Stations[i] != null)
                    {
                        StationCycleSliders[i].SetValueWithoutNotify(manager.Stations[i].CycleTime);
                        if (StationCycleValueTexts != null && i < StationCycleValueTexts.Length && StationCycleValueTexts[i] != null)
                            StationCycleValueTexts[i].text = $"{manager.Stations[i].CycleTime:F0}s";
                    }
                }
            }

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

        private void OnStationCycleChanged(int stationIndex, float value)
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;
            if (stationIndex < manager.Stations.Length && manager.Stations[stationIndex] != null)
            {
                manager.Stations[stationIndex].SetCycleTime(value);
                if (StationCycleValueTexts != null && stationIndex < StationCycleValueTexts.Length && StationCycleValueTexts[stationIndex] != null)
                    StationCycleValueTexts[stationIndex].text = $"{value:F0}s";
            }
        }

        private void OnDefectRateChanged(float value)
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;
            manager.CurrentDefectRate = value;
            if (DefectRateValueText != null)
                DefectRateValueText.text = $"{value * 100:F0}%";
        }

        public void OnScenarioSelected(int index)
        {
            var manager = LeanCellManager.Instance;
            if (manager == null || manager.AvailablePresets == null) return;
            if (index >= 0 && index < manager.AvailablePresets.Length)
            {
                manager.ApplyPreset(manager.AvailablePresets[index]);
                activeScenarioIndex = index;
                UpdateScenarioButtonHighlights();
                SyncToManager();
            }
        }

        private void UpdateScenarioButtonHighlights()
        {
            if (ScenarioButtons == null) return;
            for (int i = 0; i < ScenarioButtons.Length; i++)
            {
                if (ScenarioButtons[i] == null) continue;
                var text = ScenarioButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                    text.color = (i == activeScenarioIndex) ? ActiveScenarioColor : InactiveScenarioColor;
            }
        }

        public void OnPlay()
        {
            var manager = LeanCellManager.Instance;
            if (manager == null) return;
            if (manager.IsRunning)
                return;
            manager.ResumeSimulation();
        }

        public void OnPause()
        {
            var manager = LeanCellManager.Instance;
            if (manager != null) manager.PauseSimulation();
        }

        public void OnReset()
        {
            var manager = LeanCellManager.Instance;
            if (manager != null) manager.ResetSimulation();
        }

        public void OnWasteToggle()
        {
            if (WasteVisualizer != null)
            {
                WasteVisualizer.ToggleOverlay();
                if (WasteToggleLabel != null)
                    WasteToggleLabel.text = WasteVisualizer.OverlayActive ? "OFF" : "ON";
            }
        }
    }
}
