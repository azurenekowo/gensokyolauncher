namespace GensokyoLauncher.ViewModels;

public sealed record GameGroupViewModel(string Title, bool ShowHeading, IReadOnlyList<GameItemViewModel> Games);
