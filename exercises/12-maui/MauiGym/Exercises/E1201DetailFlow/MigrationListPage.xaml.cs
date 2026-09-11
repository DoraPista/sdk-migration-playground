namespace MauiGym.Exercises.E1201DetailFlow;

public partial class MigrationListPage : ContentPage
{
    private readonly MigrationListViewModel _viewModel;

    public MigrationListPage()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MigrationListViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private void OnMigrationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MigrationSummary migration)
        {
            _viewModel.Open(migration);
        }
    }
}
