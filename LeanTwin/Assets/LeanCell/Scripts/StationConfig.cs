using UnityEngine;

namespace LeanCell
{
    [CreateAssetMenu(menuName = "LeanCell/Station Config", fileName = "StationConfig")]
    public class StationConfig : ScriptableObject
    {
        public string StationName = "Station";
        public int StationIndex;
        public float BaseCycleTime = 30f;
        public float ValueAddTime = 25f;
        public float DefectProbability = 0f;
    }
}
