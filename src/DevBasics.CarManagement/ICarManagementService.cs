using System.Threading.Tasks;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement;

public interface ICarManagementService
{
	Task<ServiceResult> RegisterCarsAsync(RegisterCarsModel registerCarsModel, bool isForcedRegistration, Claims claims, string identity = "Unknown");
}