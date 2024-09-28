using System.Collections.Generic;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement;

public class RegistrationNumberGeneratorFactory
{
	Dictionary<CarBrand, IRegistrationNumberGenerator> _generators = new();

	public void RegisterGenerator(CarBrand carBrand, IRegistrationNumberGenerator registrationNumberGenerator)
	{
		_generators.Add(carBrand, registrationNumberGenerator);
	}

	public IRegistrationNumberGenerator ResolveGenerator(CarBrand carBrand)
	{
		return _generators[carBrand];
	}
}