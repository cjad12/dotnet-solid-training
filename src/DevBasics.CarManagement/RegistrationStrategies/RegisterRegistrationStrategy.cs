using System;
using System.Threading.Tasks;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement.RegistrationStrategies;

public class RegisterRegistrationStrategy : RegistrationStrategyBase
{
    public RegisterRegistrationStrategy(ICarRegistrationRepository carRegistrationRepository)
        : base(carRegistrationRepository)
    {
    }

    public override bool CanProcess(RegistrationType registrationType)
    {
        return registrationType == RegistrationType.Register;
    }

    public override async Task<TransactionResult?> Process(RegistrationType registrationType, BulkRegistrationResponse apiResponse, CarRegistrationDto dbCar, TransactionResult oldTxState)
    {
        if (apiResponse.RegistrationId != null
            && apiResponse.Response != null
            && apiResponse.Response == "SUCCESS")
        {
            return TransactionResult.Registered;
        }

        if (apiResponse.TransactionId != null || apiResponse.Errors != null)
        {
            Console.WriteLine("API responded with an error. Now checking if the car was registered the first time or subsequent...");
            if (await IsFirstTransaction(dbCar.CarIdentificationNumber, dbCar.RegistrationId))
            {
                // if the car was imported the first time, set the state to error
                Console.WriteLine($"Car was imported the first time. Returning transaction Result: {TransactionResult.NotRegistered.ToString()}");

                return TransactionResult.NotRegistered;
            }

            Console.WriteLine(
                $"Car was tried to be registered with subsequent Registration-Transaction. " +
                $"Returning the transaction result as it was before it the process started: {oldTxState.ToString()}");

            return oldTxState;
        }

        return null;
    }
}