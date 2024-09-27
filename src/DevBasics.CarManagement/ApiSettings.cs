using System.Collections.Generic;

namespace DevBasics.CarManagement;

public class ApiSettings: IApiSettings
{
	public IDictionary<int, string> ApiEndpoints { get; set; } = new Dictionary<int, string>();
	public IDictionary<string, string> HttpHeaders { get; set; } = new Dictionary<string, string>();

	public ApiSettings()
	{
		// Define all Leasing API Endpoints.
		ApiEndpoints.Add(1, "/bulk-registration-devices");
		ApiEndpoints.Add(2, "/check-transaction-status");
		ApiEndpoints.Add(3, "/show-registration-details");

		// Define headers for HTTP-Requests.
		HttpHeaders.Add("Content-Type", "application/json");

	}
}