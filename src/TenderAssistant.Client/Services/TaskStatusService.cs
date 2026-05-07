using TenderAssistant.Client.Infrastructure;

namespace TenderAssistant.Client.Services;

public sealed class TaskStatusService : ObservableObject
{
    private int _runningCount;
    private bool _isBusy;
    private string _message = "空闲";

    public static TaskStatusService Instance { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public IDisposable Begin(string message = "正在执行任务...")
    {
        _runningCount++;
        Message = message;
        IsBusy = true;
        return new Scope(this);
    }

    private void End()
    {
        if (_runningCount > 0)
        {
            _runningCount--;
        }

        if (_runningCount == 0)
        {
            IsBusy = false;
            Message = "空闲";
        }
    }

    private sealed class Scope(TaskStatusService service) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            service.End();
        }
    }
}
