using DevBasics.CarManagement.Dependencies;
using System;

namespace DevBasics.CarManagement
{
	public class CarPoolNumberHelper : ICarPoolNumberHelper
	{
	    private readonly IRegistrationNumberGeneratorResolver _registrationNumberGeneratorFactory;

	    public CarPoolNumberHelper(IRegistrationNumberGeneratorResolver registrationNumberGeneratorFactory)
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