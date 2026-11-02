using System.Reflection;
using Gitunia.Core.Running;
using Xunit;

namespace Gitunia.Core.Tests.Running;

// The runner ends the running mark of an effect that failed inside with the effect's own failure command.
public class OperationEffectFailureTests
{
    [Fact]
    public void GivenEveryConcreteOperationEffect_WhenItFailsWithText_ThenItReturnsAResultOfItsOwnKind()
    {
        var effects = ConcreteOperationEffects();
        Assert.NotEmpty(effects);

        foreach (var effect in effects)
        {
            var failure = effect.FailedWith("boom");

            var result = Assert.IsAssignableFrom<IOperationResult>(failure);
            Assert.True(
                result.Kind == effect.Kind,
                $"{effect.GetType().Name} fails with a result of kind {result.Kind}, not {effect.Kind}");
        }
    }

#pragma warning disable IL2026, IL2072, IL2075 // Reflection is fine in tests; they are never trimmed.

    // Tests may use reflection; the binary may not. Each effect is built through its constructor so its kind is set.
    private static List<OperationEffect> ConcreteOperationEffects()
    {
        var effects = new List<OperationEffect>();
        var types = typeof(OperationEffect).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(OperationEffect)) && !type.IsAbstract);
        foreach (var type in types)
        {
            var constructor = type.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length)
                .First();
            var arguments = constructor.GetParameters().Select(DefaultOf).ToArray();
            effects.Add((OperationEffect)constructor.Invoke(arguments));
        }

        return effects;
    }

    private static object? DefaultOf(ParameterInfo parameter) =>
        parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
#pragma warning restore IL2026, IL2072, IL2075
}
