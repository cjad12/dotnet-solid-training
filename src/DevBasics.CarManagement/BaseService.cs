using DevBasics.CarManagement.Dependencies;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace DevBasics.CarManagement
{
    public class BaseService
    {
        public IGlobalizationSettings GlobalizationSettings { get; set; }

        public HttpHeaderSettings HttpHeader { get; set; }


        public ILeasingRegistrationRepository LeasingRegistrationRepository { get; set; }
        public ISettingsRepository SettingsRepository { get; set; }


        public BaseService(IGlobalizationSettings globalizationSettings,
	        HttpHeaderSettings httpHeader,
	        ILeasingRegistrationRepository leasingRegistrationRepository,
	        ISettingsRepository settingsRepository)
        {
            // Mandatory
            GlobalizationSettings = globalizationSettings;
            HttpHeader = httpHeader;
            LeasingRegistrationRepository = leasingRegistrationRepository;
            SettingsRepository = settingsRepository;
        }

        public async Task<RequestContext> InitializeRequestContextAsync()
        {
            Console.WriteLine("Trying to initialize request context...");

            try
            {
                AppSettingDto settingResult = await SettingsRepository.GetAppSettingAsync(HttpHeader.SalesOrgIdentifier, HttpHeader.WebAppType);

                if (settingResult == null)
                {
                    throw new Exception("Error while retrieving settings from database");
                }

                RequestContext requestContext = new RequestContext()
                {
                    ShipTo = settingResult.SoldTo,
                    LanguageCode = GlobalizationSettings.LanguageCodes["English"],
                    TimeZone = "Europe/Berlin"
                };

                Console.WriteLine($"Initializing request context successful. Data (serialized as JSON): {JsonConvert.SerializeObject(requestContext)}");

                return requestContext;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Initializing request context failed: {ex}");
                return null;
            }
        }
    }
}
