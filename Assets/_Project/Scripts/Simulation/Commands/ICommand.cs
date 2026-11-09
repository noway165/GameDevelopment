namespace Metropolis.Core
{
    /// <summary>A player action. Applies completely or not at all.</summary>
    public interface ICommand
    {
        CommandResult Execute(SimContext ctx);
    }
}
