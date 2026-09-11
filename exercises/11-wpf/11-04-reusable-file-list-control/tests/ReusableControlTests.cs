using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Gym.TestUtilities.Wpf;
using MigrationKit.Wpf;

namespace Ex1104.Controls.Tests;

/// <summary>Uses the control the way the Admin Console would: no migration service, no application, no dialogs.</summary>
public sealed class ReusableControlTests
{
    private static readonly FileItem[] AdminFiles =
    [
        new("documents/tender.pdf", "Uploaded", 125_000),
        new("images/site-photo-017.jpg", "Failed", 210_000),
        new("survey/points.csv", "Uploaded", 41_000),
    ];

    [Fact]
    public void Control_can_be_created_without_the_migration_service()
    {
        Sta.Run(() =>
        {
            var control = new FileListControl();

            Assert.NotNull(control);
        });
    }

    [Fact]
    public void Control_shows_the_items_it_is_given()
    {
        Sta.Run(() =>
        {
            var control = Realized(new FileListControl { ItemsSource = AdminFiles });

            Assert.Equal(3, Rows(control).Items.Count);
        });
    }

    [Fact]
    public void Selected_item_works_in_both_directions()
    {
        Sta.Run(() =>
        {
            var host = new AdminViewModel();
            var control = Realized(new FileListControl { ItemsSource = AdminFiles, DataContext = host });

            // A host binds to SelectedItem, so it has to be a bindable (dependency) property.
            var selectedItemProperty = (DependencyProperty?)typeof(FileListControl)
                .GetField("SelectedItemProperty", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            Assert.True(selectedItemProperty is not null, "SelectedItem cannot be bound: there is no SelectedItemProperty to bind to.");
            BindingOperations.SetBinding(control, selectedItemProperty!, new Binding(nameof(AdminViewModel.Selected)) { Mode = BindingMode.TwoWay });

            // The user clicks a row.
            Rows(control).SelectedIndex = 1;
            Sta.DoEvents();
            Assert.Same(AdminFiles[1], host.Selected);

            // The host selects a row.
            host.Selected = AdminFiles[2];
            Sta.DoEvents();
            Assert.Same(AdminFiles[2], control.SelectedItem);
        });
    }

    [Fact]
    public void Double_clicking_a_row_runs_the_host_command()
    {
        Sta.Run(() =>
        {
            object? activated = null;
            var control = Realized(new FileListControl
            {
                ItemsSource = AdminFiles,
                ItemActivatedCommand = new DelegateCommand(item => activated = item),
            });

            DoubleClickRow(control, index: 1);

            Assert.Same(AdminFiles[1], activated);
        });
    }

    private static FileListControl Realized(FileListControl control)
    {
        Sta.Realize(control);
        Sta.DoEvents();
        return control;
    }

    private static ListView Rows(FileListControl control) =>
        Sta.FindDescendant<ListView>(control) ?? throw new InvalidOperationException("The control has no list.");

    private static void DoubleClickRow(FileListControl control, int index)
    {
        var list = Rows(control);
        list.SelectedIndex = index;
        Sta.DoEvents();

        // Control.MouseDoubleClick is a DIRECT routed event, so it is raised on the list itself.
        list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = Control.MouseDoubleClickEvent,
            Source = list,
        });
        Sta.DoEvents();
    }

    private sealed class AdminViewModel : INotifyPropertyChanged
    {
        private object? _selected;

        public object? Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);

        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
