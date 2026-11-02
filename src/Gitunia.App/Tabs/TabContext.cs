using Gitunia.App.Status;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.App.Tabs;

/// <summary>The tab a <see cref="TabRefresher"/> keeps current and where it reports to.</summary>
public sealed record TabContext(
    Dispatcher Host,
    IGraphSession Session,
    RepositoryLocation Location,
    StatusReporter Statuses,
    IUiInvoker Ui);
