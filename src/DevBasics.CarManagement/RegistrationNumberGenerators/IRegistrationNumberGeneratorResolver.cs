using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement.RegistrationNumberGenerators;

public interface IRegistrationNumberGeneratorResolver
{
	public IRegistrationNumberGenerator ResolveGenerator(CarBrand carBrand);
}