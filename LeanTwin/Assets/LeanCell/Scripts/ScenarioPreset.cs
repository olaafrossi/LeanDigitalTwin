using UnityEngine;

namespace LeanCell
{
    [CreateAssetMenu(menuName = "LeanCell/Scenario Preset", fileName = "ScenarioPreset")]
    public class ScenarioPreset : ScriptableObject
    {
        public string PresetName = "Default";
        [TextArea] public string Description;

        [Header("Global Parameters")]
        public float TaktTime = 30f;
        public float DefectRate = 0f;
        public int ActiveWorkerCount = 3;
        public int MaxWIP = 5;

        [Header("Per-Station Cycle Times (3 stations)")]
        public float Station1CycleTime = 10f;
        public float Station2CycleTime = 10f;
        public float Station3CycleTime = 10f;
    }
}
