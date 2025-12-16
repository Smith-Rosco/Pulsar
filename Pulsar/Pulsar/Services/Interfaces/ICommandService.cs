namespace Pulsar.Services.Interfaces
{
    public interface ICommandService
    {
        Task ExecuteAsync(string command);
        (string fileName, string arguments) ParseCommand(string raw);
    }
}