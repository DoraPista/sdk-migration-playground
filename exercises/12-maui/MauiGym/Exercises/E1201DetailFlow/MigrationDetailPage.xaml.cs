namespace MauiGym.Exercises.E1201DetailFlow;

public partial class MigrationDetailPage : ContentPage
{
    private readonly MigrationDetailViewModel _viewModel;

    public MigrationDetailPage()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MigrationDetailViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        App.Services.GetRequiredService<LegacyNavigationService>().GoBack();
    }
}
