using System.ComponentModel;

namespace Photobooth.Services;

public interface IPrintQueueService : INotifyPropertyChanged
{
    int PendingTaskCount { get; }
    bool HasPendingTasks { get; }
    bool TryEnqueue(string imagePath);
}
