using UnityEngine;

namespace LeanCell
{
    public class SimulationClock : MonoBehaviour
    {
        public static SimulationClock Instance { get; private set; }

        [Header("Read-Only State")]
        [SerializeField] private float elapsedSimTime;
        [SerializeField] private int completedTaktBeats;

        private float simStartTime;
        private float pausedDuration;
        private float pauseStartTime;
        private bool isPaused;

        public float ElapsedSimTime => elapsedSimTime;
        public int CompletedTaktBeats => completedTaktBeats;

        public float TaktProgress
        {
            get
            {
                var manager = LeanCellManager.Instance;
                if (manager == null || manager.CurrentTaktTime <= 0) return 0f;
                return (elapsedSimTime % manager.CurrentTaktTime) / manager.CurrentTaktTime;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartClock()
        {
            simStartTime = Time.time;
            pausedDuration = 0f;
            isPaused = false;
            elapsedSimTime = 0f;
            completedTaktBeats = 0;
        }

        public void Pause()
        {
            if (isPaused) return;
            isPaused = true;
            pauseStartTime = Time.time;
        }

        public void Resume()
        {
            if (!isPaused) return;
            isPaused = false;
            pausedDuration += Time.time - pauseStartTime;
        }

        void Update()
        {
            if (isPaused) return;

            elapsedSimTime = Time.time - simStartTime - pausedDuration;

            var manager = LeanCellManager.Instance;
            if (manager != null && manager.CurrentTaktTime > 0)
            {
                int newBeats = Mathf.FloorToInt(elapsedSimTime / manager.CurrentTaktTime);
                if (newBeats > completedTaktBeats)
                    completedTaktBeats = newBeats;
            }
        }
    }
}
