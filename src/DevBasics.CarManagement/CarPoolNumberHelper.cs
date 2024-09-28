using DevBasics.CarManagement.Dependencies;
using System;

namespace DevBasics.CarManagement
{
	public class CarPoolNumberHelper
    {
	    private readonly RegistrationNumberGeneratorFactory _registrationNumberGeneratorFactory;

	    public CarPoolNumberHelper(RegistrationNumberGeneratorFactory registrationNumberGeneratorFactory)
	    {
		    _registrationNumberGeneratorFactory = registrationNumberGeneratorFactory;
	    }

        public void Generate(CarBrand requestOrigin, string endCustomerRegistrationReference, out string registrationRegistrationId, out string registrationNumber)
        {
            registrationRegistrationId = GenerateRegistrationRegistrationId();
            registrationNumber = _registrationNumberGeneratorFactory.ResolveGenerator(requestOrigin)
	            .GenerateRegistrationNumber(endCustomerRegistrationReference, registrationRegistrationId);
        }

        public string GenerateRegistrationRegistrationId()
        {
            return DateTime.Now.Ticks.ToString();
        }

    }
}