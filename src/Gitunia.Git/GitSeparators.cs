namespace Gitunia.Git;

/// <summary>Separator bytes and their <c>--format</c> spellings, shared by every format that git output is parsed from.</summary>
public static class GitSeparators
{
    /// <summary>Unit separator (0x1f) between the fields of one record.</summary>
    public const byte UnitSeparator = 0x1f;

    /// <summary>Record separator (0x1e) that ends a record where the fields are NUL-separated.</summary>
    public const char RecordSeparator = '\x1e';

    /// <summary>NUL ends a record under <c>-z</c>.</summary>
    public const byte RecordTerminator = 0;

    /// <summary>The unit separator as a <c>--format</c> placeholder.</summary>
    public const string UnitSeparatorPlaceholder = "%x1f";

    /// <summary>The record separator as a <c>--format</c> placeholder.</summary>
    public const string RecordSeparatorPlaceholder = "%1e";

    /// <summary>The record separator as a <c>log</c>-style <c>--format</c> placeholder (<c>for-each-ref</c> spells it without the x).</summary>
    public const string LogRecordSeparatorPlaceholder = "%x1e";

    /// <summary>NUL as a <c>log</c>-style <c>--format</c> placeholder.</summary>
    public const string LogNulPlaceholder = "%x00";

    /// <summary>NUL as a <c>for-each-ref --format</c> placeholder.</summary>
    public const string NulPlaceholder = "%00";
}
