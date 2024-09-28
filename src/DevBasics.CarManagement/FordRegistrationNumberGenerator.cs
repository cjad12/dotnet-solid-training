namespace DevBasics.CarManagement;

public class FordRegistrationNumberGenerator : IRegistrationNumberGenerator
{
	public string GenerateRegistrationNumber(string endCustomerRegistrationReference, string registrationRegistrationId)
	{
		return string.IsNullOrWhiteSpace(endCustomerRegistrationReference) ? registrationRegistrationId : endCustomerRegistrationReference;
	}
}