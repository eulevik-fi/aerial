using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aerial;

/// <summary>
/// Provides access to video catalog entries and initialization logic.
/// </summary>
internal interface IVideoCatalog
{
    /// <summary>The loaded video entries.</summary>
    IReadOnlyList<Video> Videos { get; }

    /// <summary>Initializes the catalog by downloading and parsing video entries.</summary>
    Task InitializeAsync();
}
