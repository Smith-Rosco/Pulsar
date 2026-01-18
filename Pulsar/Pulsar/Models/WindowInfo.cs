namespace Pulsar.Models
{
    public class WindowInfo
    {
        public string ProcessName { get; }
        public string Path { get; }
        public string Title { get; }

        public WindowInfo(string processName, string path, string title)
        {
            ProcessName = processName;
            Path = path;
            Title = title;
        }
    }
}