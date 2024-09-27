using System.Threading.Tasks;

namespace DevBasics.CarManagement.Dependencies;

internal sealed class SettingsRepository : ISettingsRepository
{
	public Task<AppSettingDto> GetAppSettingAsync(string salesOrgIdentifier, CarBrand requestOrigin)
	{
		return Task.FromResult(new AppSettingDto
		{
			SoldTo = "SoldTo01"
		});
	}
}