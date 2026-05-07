using System.Windows.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TenderAssistant.Client.Services;

namespace TenderAssistant.Client.Infrastructure;

public sealed class RelayCommand : ICommand, INotifyPropertyChanged
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isRunning;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public bool CanExecute(object? parameter)
    {
        return !IsRunning && (_canExecute?.Invoke(parameter) ?? true);
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            IsRunning = true;
            using var status = TaskStatusService.Instance.Begin("正在执行任务...");
            _execute(parameter);
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
