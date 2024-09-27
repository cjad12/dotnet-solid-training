using DevBasics.CarManagement.Dependencies;
using System.Threading.Tasks;

namespace DevBasics.CarManagement
{
    public interface ILeasingRegistrationRepository
    {
        Task<int> InsertHistoryAsync(CarRegistrationDto dbCar, string userName, string transactionStateName = null, string transactionTypeName = null);
        Task<bool> UpdateCarAsync(CarRegistrationDto dbCar);
    }
}
