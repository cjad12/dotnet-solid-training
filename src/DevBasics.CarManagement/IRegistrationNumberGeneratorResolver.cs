using DevBasics.CarManagement.Dependencies;

namespace DevBasics.CarManagement;

public interface IRegistrationNumberGeneratorResolver
{
	public IRegistrationNumberGenerator ResolveGenerator(CarBrand carBrand);
}