using System;
using System.Threading.Tasks;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement.RegistrationStrategies;

public class UnregisterRegistrationStrategy : RegistrationStrategyBase
{
    public UnregisterRegistrationStrategy(ICarRegistrationRepository carRegistrationRepository) : base(carRegistrationRepository)
    {
    }

    public override bool CanProcess(RegistrationType registrationType)
    {
        return registrationType == RegistrationType.Unregister;
    }

    public override async Task<TransactionResult?> Process(RegistrationType registrationType, BulkRegistrationResponse apiResponse, CarRegistrationDto dbCar,
        TransactionResult oldTxState)
    {
        Console.WriteLine("Trying to analyze unregistration transaction result...");
        if (!await IsFirstTransaction(dbCar.CarIdentificationNumber, dbCar.RegistrationId))
        {
            if (apiResponse.RegistrationId != null)
            {
                return TransactionResult.NotRegistered;
            }

            if (apiResponse.TransactionId != null || apiResponse.Errors != null)
            {
                return oldTxState;
            }
        }

        return null;
    }
}