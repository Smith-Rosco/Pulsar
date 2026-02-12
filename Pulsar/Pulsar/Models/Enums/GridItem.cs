namespace Pulsar.Models
{
    public class GridItem
    {
        // 这里的 RadialMenuMode 现在会自动识别为 Pulsar.Models.Enums.RadialMenuMode
        public RadialMenuMode Type { get; set; } 
        public int Slot { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Cmd { get; set; } = string.Empty;
        public string? Process { get; set; }
    }
}