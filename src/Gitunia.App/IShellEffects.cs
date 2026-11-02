
namespace Gitunia.App;

/// <summary>
/// Every effect only the terminal layer can perform in one object. The runner uses it through <see cref="ShellRoles"/>;
/// the terminal layer passes the role objects directly, a test or <see cref="NoShellEffects"/> implements this.
/// </summary>
public interface IShellEffects :
    ICopyEffects,
    ICursorEffects,
    IFlowEffects,
    ISessionEffects,
    IStashEffects,
    IToolEffects,
    IActionRouteEffects;
