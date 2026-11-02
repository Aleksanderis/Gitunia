
namespace Gitunia.App.Loading.Graph;

/// <summary>The number of distinct lane colours the palette offers, read per load because the scheme can change.</summary>
public interface ILaneColors
{
    /// <summary>Distinct colours of the configured palette, for the layout builder.</summary>
    int Count { get; }
}
