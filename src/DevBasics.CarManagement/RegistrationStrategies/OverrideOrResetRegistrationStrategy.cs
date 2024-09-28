using System.Threading.Tasks;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement.RegistrationStrategies;

public class OverrideOrResetRegistrationStrategy : RegistrationStrategyBase
{
    public OverrideOrResetRegistrationStrategy(ICarRegistrationRepository carRegistrationRepository) : base(carRegistrationRepository)
    {
    }

    public override bool CanProcess(RegistrationType registrationType)
    {
        return registrationType == RegistrationType.Override || registrationType == RegistrationType.Reset;
    }

    public override async Task<TransactionResult?> Process(RegistrationType registrationType, BulkRegistrationResponse apiResponse, CarRegistrationDto dbCar,
        TransactionResult oldTxState)
    {
        if (apiResponse.RegistrationId != null)
        {
            return TransactionResult.Progress;
        }

        if (apiResponse.TransactionId != null || apiResponse.Errors != null)
        {
            return oldTxState;
        }

        return null;
    }
}