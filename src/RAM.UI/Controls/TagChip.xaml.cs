using System.Windows;
using System.Windows.Controls;

namespace RAM.UI.Controls;

public partial class TagChip : UserControl
{
    public TagChip() => InitializeComponent();

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(TagChip),
            new PropertyMetadata(""));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
