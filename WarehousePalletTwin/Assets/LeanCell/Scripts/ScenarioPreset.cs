using UnityEngine;

namespace LeanCell
{
    [CreateAssetMenu(menuName = "LeanCell/Scenario Preset", fileName = "ScenarioPreset")]
    public class ScenarioPreset : ScriptableObject
    {
        public string PresetName = "Default";
        [TextArea] public string Description;

        [Header("Global Parameters")]
        public float TaktTime = 35f;
        public int BatchSize = 1;
        public float DefectRate = 0f;
        public FlowMode FlowMode = FlowMode.Push;
        public int ActiveWorkerCount = 3;
        public int MaxWIP = 5;

        [Header("Per-Station Cycle Times (4 stations)")]
        public float Station1CycleTime = 30f;
        public float Station2CycleTime = 30f;
        public float Station3CycleTime = 30f;
        public float Station4CycleTime = 30f;
    }
}
