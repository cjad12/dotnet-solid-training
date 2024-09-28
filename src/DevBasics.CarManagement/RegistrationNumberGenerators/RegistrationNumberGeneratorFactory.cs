using System.Collections.Generic;
using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement.RegistrationNumberGenerators;

public class RegistrationNumberGeneratorFactory: IRegistrationNumberGeneratorResolver
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