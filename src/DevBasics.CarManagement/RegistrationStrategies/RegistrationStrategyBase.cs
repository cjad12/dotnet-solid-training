using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement.RegistrationStrategies;

public abstract class RegistrationStrategyBase : IRegistrationStrategy
{
    private readonly ICarRegistrationRepository _carRegistrationRepository;

    public RegistrationStrategyBase(ICarRegistrationRepository carRegistrationRepository)
    {
        _carRegistrationRepository = carRegistrationRepository;
    }

    protected async Task<bool> IsFirstTransaction(string carIdentificationNumber, string registrationRegistrationId)
    {
        Console.WriteLine($"Trying to analyze if this is the first transaction for car {carIdentificationNumber}...");

        IEnumerable<CarRegistrationLogDto> carHistory = (await _carRegistrationRepository.GetCarHistoryAsync(carIdentificationNumber)).Where(x => x.RegistrationId == registrationRegistrationId);

        if (carHistory != null)
        {
            IOrderedEnumerable<CarRegistrationLogDto> sortedCarHistory = carHistory.OrderBy(d => d.RowCreationDate);
            bool isInitialTransaction = !sortedCarHistory.Any(x => x.TransactionState == TransactionResult.Registered.ToString());

            Console.WriteLine($"History of car {carIdentificationNumber} is not null, returning {isInitialTransaction}");

            return isInitialTransaction;
        }
        else
        {
            Console.WriteLine($"History of car {carIdentificationNumber} is null, returning true");
            return true;
        }
    }

    public abstract bool CanProcess(RegistrationType registrationType);
    public abstract Task<TransactionResult?> Process(RegistrationType registrationType, BulkRegistrationResponse apiResponse, CarRegistrationDto dbCar, TransactionResult oldTxState);
}