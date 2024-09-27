using System.Collections.Generic;

namespace DevBasics.CarManagement;

public interface IGlobalizationSettings
{
	IDictionary<string, string> LanguageCodes { get; }
	IDictionary<string, int> TimeZones { get; }
}