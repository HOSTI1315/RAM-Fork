using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace RAM.UI.Behaviors;

/// <summary>
/// Two-way syncs <see cref="Selector.SelectedItems"/> (read-only DependencyProperty on
/// ListBox) with a bindable <see cref="IList"/> on the view-model. Without this,
/// multi-select XAML can only push from UI to VM via SelectionChanged, leaving VM-driven
/// selection clearing (e.g. <c>ClearSelectionCommand</c>) broken.
/// </summary>
public static class ListBoxSelectedItemsBehavior
{
    public static readonly DependencyProperty BindingProperty =
        DependencyProperty.RegisterAttached(
            "Binding", typeof(IList), typeof(ListBoxSelectedItemsBehavior),
            new PropertyMetadata(null, OnBindingChanged));

    public static IList? GetBinding(DependencyObject d) => (IList?)d.GetValue(BindingProperty);
    public static void SetBinding(DependencyObject d, IList? value) => d.SetValue(BindingProperty, value);

    private static readonly DependencyProperty SyncContextProperty =
        DependencyProperty.RegisterAttached(
            "SyncContext", typeof(SyncContext), typeof(ListBoxSelectedItemsBehavior),
            new PropertyMetadata(null));

    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        // Detach previous sync context
        if (listBox.GetValue(SyncContextProperty) is SyncContext oldCtx)
        {
            oldCtx.Detach();
            listBox.SetValue(SyncContextProperty, null);
        }

        if (e.NewValue is IList newList)
        {
            var ctx = new SyncContext(listBox, newList);
            listBox.SetValue(SyncContextProperty, ctx);
        }
    }

    private sealed class SyncContext
    {
        private readonly ListBox _listBox;
        private readonly IList _vmCollection;
        private readonly INotifyCollectionChanged? _vmNotifier;
        private bool _suppress;

        public SyncContext(ListBox listBox, IList vmCollection)
        {
            _listBox = listBox;
            _vmCollection = vmCollection;
            _vmNotifier = vmCollection as INotifyCollectionChanged;

            _listBox.SelectionChanged += OnListBoxSelectionChanged;
            if (_vmNotifier is not null)
                _vmNotifier.CollectionChanged += OnVmCollectionChanged;

            // Initial sync VM → ListBox so a pre-populated VM collection shows as selected.
            SyncListBoxFromVm();
        }

        public void Detach()
        {
            _listBox.SelectionChanged -= OnListBoxSelectionChanged;
            if (_vmNotifier is not null)
                _vmNotifier.CollectionChanged -= OnVmCollectionChanged;
        }

        private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppress) return;
            _suppress = true;
            try
            {
                foreach (var removed in e.RemovedItems)
                    _vmCollection.Remove(removed);
                foreach (var added in e.AddedItems)
                    if (!_vmCollection.Contains(added))
                        _vmCollection.Add(added);
            }
            finally { _suppress = false; }
        }

        private void OnVmCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppress) return;
            SyncListBoxFromVm();
        }

        private void SyncListBoxFromVm()
        {
            _suppress = true;
            try
            {
                _listBox.SelectedItems.Clear();
                foreach (var item in _vmCollection)
                    _listBox.SelectedItems.Add(item);
            }
            finally { _suppress = false; }
        }
    }
}
