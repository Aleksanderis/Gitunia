namespace Gitunia.Tui.Input;

/// <summary>Runs work in a later loop iteration of the UI, so keys are handled in between.</summary>
public interface IUiDeferral
{
    /// <summary>Runs the work in a later loop iteration.</summary>
    /// <param name="work">The work.</param>
    void DeferToNextIteration(Action work);
}
