namespace Pehlione.ViewModels.Navigation;

public sealed class MainNavVm
{
    public string? ActiveSlug { get; init; }
    public IReadOnlyList<MainNavItemVm> Categories { get; init; } = Array.Empty<MainNavItemVm>();
}
