namespace MediaButler.Mobile.Data
{
    public static class ConfigSettings
    {
        public static string OrgDir { get; set; }
        public static string DestDir { get; set; }
        public static bool IsDev { get; set; }

        public static string TrainDataPath { get; set; }
        public static string TrainDataName { get; set; }
        public static string TestDataPath { get; set; }
        public static string TestDataName { get; set; }
        public static string ModelPath { get; set; }
        public static string ModelName { get; set; }
    }
}
