using System.Collections.Generic;
using System.Threading.Tasks;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement;

public interface ICarRepository
{
	Task<string> BeginTransactionGenerateId(IList<string> cars,
		string customerId, string companyId, RegistrationType registrationType, string identity, string registrationNumber = null);

	Task<IList<int>> FinishTransactionAsync(RegistrationType registrationType,
		BulkRegistrationResponse apiResponse, IList<string> carIdentifier, string companyId, string identity,
		string transactionStateBackup = null, BulkRegistrationRequest requestModel = null);
}