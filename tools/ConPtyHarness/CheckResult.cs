/// <summary>The outcome of one smoke check.</summary>
/// <param name="Name">The check name.</param>
/// <param name="Ok">Whether it passed.</param>
/// <param name="Detail">Extra information shown next to the result.</param>
internal sealed record CheckResult(string Name, bool Ok, string Detail);
