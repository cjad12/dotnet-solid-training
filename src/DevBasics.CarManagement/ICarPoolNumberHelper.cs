using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement;

public interface ICarPoolNumberHelper
{
	void Generate(CarBrand requestOrigin, string endCustomerRegistrationReference, out string registrationRegistrationId, out string registrationNumber);
	string GenerateRegistrationRegistrationId();
}