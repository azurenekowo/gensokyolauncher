using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public interface IGameDiscoveryService
{
    Task<IReadOnlyList<DiscoveredGame>> DiscoverAsync(string root, CancellationToken cancellationToken = default);
}
