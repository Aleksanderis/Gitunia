namespace Gitunia.Core.Keymap.Models;

/// <summary>An item of a group with its cluster, null when it is a loose action.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Item">The item.</param>
/// <param name="Cluster">The cluster name.</param>
public sealed record ClusteredItem<T>(T Item, string? Cluster);
