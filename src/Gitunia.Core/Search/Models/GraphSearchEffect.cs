namespace Gitunia.Core.Search.Models;

/// <summary>Something the runner must do after a search transition.</summary>
public abstract record GraphSearchEffect
{
    private protected GraphSearchEffect()
    {
    }
}

/// <summary>Scan the next slice of rows for the query of this generation.</summary>
/// <param name="Generation">The generation to scan for.</param>
public sealed record ScanSearchEffect(int Generation) : GraphSearchEffect;

/// <summary>Select a match in the graph.</summary>
/// <param name="Index">The row index.</param>
public sealed record RevealMatchEffect(int Index) : GraphSearchEffect;

/// <summary>Ask git which commits have the text in their message (a <c>body:</c> query); the answer comes back as <see cref="BodyHitsCommand"/>.</summary>
/// <param name="Generation">The generation to answer for.</param>
/// <param name="Text">The text to look for.</param>
public sealed record SearchBodiesEffect(int Generation, string Text) : GraphSearchEffect;
