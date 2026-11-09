namespace Metropolis.Core
{
    public sealed class SetTaxRateCommand : ICommand
    {
        readonly int _rate;

        public SetTaxRateCommand(int rate)
        {
            _rate = rate;
        }

        public CommandResult Execute(SimContext ctx)
        {
            if (_rate < 0 || _rate > ctx.Config.MaxTaxRate)
                return CommandResult.Fail(CommandFailure.InvalidTaxRate);

            ctx.City.TaxRate = _rate;
            return CommandResult.Ok;
        }
    }
}
