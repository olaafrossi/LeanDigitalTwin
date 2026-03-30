using UnityEngine;

namespace LeanCell
{
    public enum WasteType
    {
        Transport,      // T - unnecessary movement of product
        Inventory,      // I - excess WIP between stations
        Motion,         // M - unnecessary worker movement
        Waiting,        // W - idle time
        Overproduction, // O - producing more than demanded
        OverProcessing, // V - doing more work than needed
        Defects         // D - rework or scrap
    }

    public enum WasteSeverity { Low, Medium, High }

    public enum FlowMode { Push, Pull }

    public class WasteEvent
    {
        public WasteType Type;
        public float Timestamp;
        public float Duration;
        public int StationIndex;
        public int WorkerID;
        public int MUID;
        public WasteSeverity Severity;
        public string Description;

        public WasteEvent(WasteType type, float timestamp, int stationIndex = -1, int workerID = -1)
        {
            Type = type;
            Timestamp = timestamp;
            StationIndex = stationIndex;
            WorkerID = workerID;
            MUID = -1;
            Severity = WasteSeverity.Low;
            Description = "";
        }
    }

    public static class WasteColors
    {
        public static readonly Color Transport     = new Color(0.129f, 0.588f, 0.953f); // #2196F3
        public static readonly Color Inventory     = new Color(1.000f, 0.596f, 0.000f); // #FF9800
        public static readonly Color Motion        = new Color(0.612f, 0.153f, 0.690f); // #9C27B0
        public static readonly Color Waiting       = new Color(0.957f, 0.263f, 0.212f); // #F44336
        public static readonly Color Overproduction= new Color(1.000f, 0.435f, 0.000f); // #FF6F00
        public static readonly Color OverProcessing= new Color(0.000f, 0.737f, 0.831f); // #00BCD4
        public static readonly Color Defects       = new Color(0.718f, 0.110f, 0.110f); // #B71C1C
        public static readonly Color ValueAdd      = new Color(0.000f, 0.784f, 0.325f); // #00C853

        public static Color GetColor(WasteType type)
        {
            return type switch
            {
                WasteType.Transport      => Transport,
                WasteType.Inventory      => Inventory,
                WasteType.Motion         => Motion,
                WasteType.Waiting        => Waiting,
                WasteType.Overproduction => Overproduction,
                WasteType.OverProcessing => OverProcessing,
                WasteType.Defects        => Defects,
                _ => Color.white
            };
        }

        public static string GetLabel(WasteType type)
        {
            return type switch
            {
                WasteType.Transport      => "T - Transport",
                WasteType.Inventory      => "I - Inventory",
                WasteType.Motion         => "M - Motion",
                WasteType.Waiting        => "W - Waiting",
                WasteType.Overproduction => "O - Overproduction",
                WasteType.OverProcessing => "V - Over-processing",
                WasteType.Defects        => "D - Defects",
                _ => "Unknown"
            };
        }
    }
}
