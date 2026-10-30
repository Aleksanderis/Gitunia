namespace Gitunia.Core.Editing;

/// <summary>
/// The commit being written in one tab's working-tree view: the message editor, the three options and the amend bookkeeping. It
/// lives as long as the tab (ux-workflows.md, workflow 2: the draft is kept until a commit succeeds). Pure and renderer-agnostic.
/// </summary>
public sealed class CommitDraft
{
    private const string DefaultTitle = "Commit message";

    private string _savedText = string.Empty;
    private string? _prefilled;

    /// <summary>The message being written.</summary>
    public MessageEditor Editor { get; } = new();

    /// <summary>Adds a <c>Signed-off-by</c> trailer.</summary>
    public bool SignOff { get; set; }

    /// <summary>Skips the commit hooks.</summary>
    public bool NoVerify { get; set; }

    /// <summary>The commit replaces HEAD.</summary>
    public bool Amend { get; private set; }

    /// <summary>The short hash and subject of the commit being amended, empty until HEAD's message was read.</summary>
    public string AmendTarget { get; private set; } = string.Empty;

    /// <summary>A commit is running: the box is read-only.</summary>
    public bool IsCommitting { get; set; }

    /// <summary>Index of the first visible message line; the view keeps the cursor line visible.</summary>
    public int Top { get; set; }

    /// <summary>
    /// Text the user typed or edited, which would be lost when the tab closes or the program quits (an untouched prefilled
    /// message does not count).
    /// </summary>
    public bool HasUnsentText => !Editor.IsBlank && (_prefilled is null || Editor.Text != _prefilled);

    /// <summary>
    /// The title of the message box. Once there is a message, the subject width and its soft limit follow the first words, so a
    /// narrow box cuts the amend target and not the count.
    /// </summary>
    public string Title
    {
        get
        {
            var label = Amend ? "Amending" : DefaultTitle;
            var count = Editor.IsBlank ? string.Empty : $" {Editor.SubjectWidth}/{MessageEditor.SubjectSoftLimit}";
            var target = Amend && AmendTarget.Length > 0 ? $" {AmendTarget}" : string.Empty;

            return $"{label}{count}{target}";
        }
    }

    /// <summary>Starts an empty box with a prepared message (git's merge message); ignored when something is typed.</summary>
    /// <param name="message">The message to start with.</param>
    public void Prefill(string message)
    {
        if (!Editor.IsBlank || message.Trim().Length == 0)
        {
            return;
        }

        _savedText = string.Empty;
        _prefilled = message;
        Editor.SetText(message);
    }

    /// <summary>Turns Amend on or off. Off restores the draft that was set aside when HEAD's message filled an empty box.</summary>
    public void SetAmend(bool isOn)
    {
        if (Amend == isOn)
        {
            return;
        }

        Amend = isOn;
        if (isOn)
        {
            return;
        }

        AmendTarget = string.Empty;
        if (_prefilled is not null && Editor.Text == _prefilled)
        {
            Editor.SetText(_savedText);
        }

        _prefilled = null;
    }

    /// <summary>
    /// Takes HEAD's message once it is read: it names the commit in the title and, when the box is still empty, fills it (the empty
    /// draft is set aside). Ignored when Amend was switched off meanwhile.
    /// </summary>
    /// <param name="shortHash">Short hash of HEAD.</param>
    /// <param name="message">Full message of HEAD.</param>
    public void ApplyHeadMessage(string shortHash, string message)
    {
        if (!Amend)
        {
            return;
        }

        var subject = message.Split('\n', 2)[0].Trim();
        AmendTarget = subject.Length > 0 ? $"{shortHash} {subject}" : shortHash;
        if (!Editor.IsBlank)
        {
            return;
        }

        _savedText = Editor.Text;
        _prefilled = message;
        Editor.SetText(message);
    }

    /// <summary>A commit succeeded: the message and Amend are gone, SignOff and No-Verify stay.</summary>
    public void Sent()
    {
        Editor.Clear();
        Amend = false;
        AmendTarget = string.Empty;
        _prefilled = null;
        _savedText = string.Empty;
        Top = 0;
    }
}
