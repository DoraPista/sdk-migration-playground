namespace MauiGym.Exercises.E1202MigrationList;

public partial class MigrationBoardPage : ContentPage
{
    private readonly MigrationBoardViewModel _viewModel;

    public MigrationBoardPage()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MigrationBoardViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}
