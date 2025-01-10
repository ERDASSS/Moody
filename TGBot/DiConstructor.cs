using Database;
using Ninject;

namespace TGBot;

public static class DiConstructor
{
    public static StandardKernel GetContainer()
    {
        var container = new StandardKernel();
        container.Bind<IDbAccessor>().To<SqliteDbAccessor>();
        container.Bind<ReactionToIncorrectInput>().ToConstant(ReactionToIncorrectInput.Ignore);
        container.Bind<StateMachine>().ToConstructor(
            ctor => new StateMachine(
                "7727939273:AAFqtb1fa1rNsHxDDUjLO8JLZztddX1LvMo",
                ctor.Inject<IDbAccessor>(),
                ctor.Inject<ReactionToIncorrectInput>())
        );
        return container;
    }
}