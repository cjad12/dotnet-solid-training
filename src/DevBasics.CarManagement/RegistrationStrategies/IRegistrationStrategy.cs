using System.Threading.Tasks;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement.RegistrationStrategies;

public interface IRegistrationStrategy
{
    bool CanProcess(RegistrationType registrationType);
    Task<TransactionResult?> Process(RegistrationType registrationType, BulkRegistrationResponse apiResponse, CarRegistrationDto dbCar, TransactionResult oldTxState);
}