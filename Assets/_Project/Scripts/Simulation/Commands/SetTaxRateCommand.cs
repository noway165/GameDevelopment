namespace Metropolis.Core
{
    public sealed class SetTaxRateCommand : ICommand
    {
        readonly int _rate;

        public SetTaxRateCommand(int rate)
        {
            _rate = rate;
        }

        public CommandResult Validate(SimContext ctx) =>
            _rate < 0 || _rate > ctx.Config.MaxTaxRate
                ? CommandResult.Fail(CommandFailure.InvalidTaxRate)
                : CommandResult.Ok;

        public CommandResult Execute(SimContext ctx)
        {
            var result = Validate(ctx);
            if (result.Succeeded)
                ctx.City.TaxRate = _rate;
            return result;
        }
    }
}
