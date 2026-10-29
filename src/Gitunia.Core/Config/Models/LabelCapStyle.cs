namespace Gitunia.Core.Config.Models;

/// <summary>How the ends of a branch label chip are drawn (the <c>labelCaps</c> option, #851).</summary>
public enum LabelCapStyle
{
    /// <summary>A plain filled box (default; needs no special font).</summary>
    None,

    /// <summary>Rounded Powerline caps (U+E0B6, U+E0B4); needs a Nerd Font or Powerline-patched font.</summary>
    Rounded,

    /// <summary>Angled Powerline caps (U+E0B2, U+E0B0); needs a Nerd Font or Powerline-patched font.</summary>
    Angled,
}
