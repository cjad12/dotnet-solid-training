namespace DevBasics.CarManagement.RegistrationNumberGenerators;

public class FordRegistrationNumberGenerator : IRegistrationNumberGenerator
{
	public string GenerateRegistrationNumber(string endCustomerRegistrationReference, string registrationRegistrationId)
	{
		return string.IsNullOrWhiteSpace(endCustomerRegistrationReference) ? registrationRegistrationId : endCustomerRegistrationReference;
	}
}