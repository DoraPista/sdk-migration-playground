namespace MauiGym.Exercises.E1201DetailFlow;

public partial class MigrationListPage : ContentPage
{
    private readonly MigrationListViewModel _viewModel;

    /// <summary>Created by the DI container (registered in MauiProgram); no service locator.</summary>
    public MigrationListPage(MigrationListViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnMigrationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MigrationSummary migration)
        {
            // Clear the selection so coming back doesn't re-trigger navigation.
            ((CollectionView)sender!).SelectedItem = null;
            await _viewModel.OpenAsync(migration);
        }
    }
}
