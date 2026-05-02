using System.Windows;
using System.Windows.Controls;
using RAM.Core.Models;

namespace RAM.UI.Controls;

public partial class WorkerStateBadge : UserControl
{
    public WorkerStateBadge() => InitializeComponent();

    public static readonly DependencyProperty WorkerStateProperty =
        DependencyProperty.Register(
            nameof(WorkerState), typeof(RejoinWorkerState), typeof(WorkerStateBadge),
            new PropertyMetadata(RejoinWorkerState.Idle));

    public RejoinWorkerState WorkerState
    {
        get => (RejoinWorkerState)GetValue(WorkerStateProperty);
        set => SetValue(WorkerStateProperty, value);
    }
}
