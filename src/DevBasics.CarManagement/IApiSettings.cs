using System.Collections.Generic;

namespace DevBasics.CarManagement;

public interface IApiSettings {
	IDictionary<int, string> ApiEndpoints { get; }
	IDictionary<string, string> HttpHeaders { get; }
}