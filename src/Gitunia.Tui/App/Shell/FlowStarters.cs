using Gitunia.Tui.App.FileHistory;
using Gitunia.Tui.App.Reflog;

namespace Gitunia.Tui.App.Shell;

/// <summary>The objects that carry out the flows a state effect hands to the terminal layer.</summary>
/// <param name="Reflog">Starts the flow a reflog entry stands for.</param>
/// <param name="FileHistory">Moves the graph cursor to a file history commit.</param>
public sealed record FlowStarters(ReflogEntryStarter Reflog, FileHistoryJumper FileHistory);
