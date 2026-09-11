namespace MauiGym.Exercises.E1201DetailFlow;

public partial class MigrationDetailPage : ContentPage
{
    private readonly MigrationDetailViewModel _viewModel;
    private readonly IAppNavigation _navigation;

    /// <summary>Resolved by Shell from the DI container because the route is registered.</summary>
    public MigrationDetailPage(MigrationDetailViewModel viewModel, IAppNavigation navigation)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _navigation = navigation;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await _navigation.GoBackAsync();
}
