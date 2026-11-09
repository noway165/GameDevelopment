namespace Metropolis.Core
{
    /// <summary>A player action. Applies completely or not at all.</summary>
    public interface ICommand
    {
        /// <summary>Reports whether the command would succeed. Never changes state or raises events.</summary>
        CommandResult Validate(SimContext ctx);

        CommandResult Execute(SimContext ctx);
    }
}
