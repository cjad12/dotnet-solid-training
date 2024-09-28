using AutoMapper;
using DevBasics.CarManagement.Dependencies;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static DevBasics.CarManagement.Dependencies.RegistrationApiResponseBase;

namespace DevBasics.CarManagement
{
	public class CarManagementService : BaseService, ICarManagementService
    {
	    private readonly IMapper _mapper;
	    private readonly ICarPoolNumberHelper _carPoolNumberHelper;
	    private readonly CarRepository _carRepository;

	    public CarManagementService(IMapper mapper,
	        IGlobalizationSettings globalizationSettings,
	        IHttpHeaderSettings httpHeader,
	        IBulkRegistrationService bulkRegistrationService,
	        ILeasingRegistrationRepository leasingRegistrationRepository,
	        ICarRegistrationRepository carRegistrationRepository,
	        ISettingsRepository settingsRepository,
	        ICarPoolNumberHelper carPoolNumberHelper)
                : base(globalizationSettings, httpHeader,
                      leasingRegistrationRepository,
                      settingsRepository)
        {
	        Console.WriteLine($"Initializing service {nameof(CarManagementService)}");

	        BulkRegistrationService = bulkRegistrationService;
	        CarLeasingRepository = carRegistrationRepository;
            _mapper = mapper;
            _carPoolNumberHelper = carPoolNumberHelper;
            _carRepository = new CarRepository(leasingRegistrationRepository, carRegistrationRepository);
        }

        public IBulkRegistrationService BulkRegistrationService { get; }
        public ICarRegistrationRepository CarLeasingRepository { get; }


		public async Task<ServiceResult> RegisterCarsAsync(RegisterCarsModel registerCarsModel, bool isForcedRegistration, Claims claims, string identity = "Unknown")
        {
            ServiceResult serviceResult = new ServiceResult();

            try
            {
                // See Feature 307.
                registerCarsModel.Cars.ToList().ForEach(x =>
                {
                    if (string.IsNullOrWhiteSpace(x.VehicleIdentificationNumber) == false)
                    {
                        x.VehicleIdentificationNumber = x.VehicleIdentificationNumber.ToUpper();
                    }
                });

                registerCarsModel.Cars = registerCarsModel.Cars.RemoveDuplicates();

                Console.WriteLine($"Trying to invoke initial bulk registration for {registerCarsModel.Cars.Count} cars. " +
                    $"Cars: {string.Join(", ", registerCarsModel.Cars.Select(x => x.VehicleIdentificationNumber))}, " +
                    $"Is forced registration: {isForcedRegistration}");

                if (isForcedRegistration && !registerCarsModel.DeactivateAutoRegistrationProcessing)
                {
                    List<CarRegistrationModel> existingItems = registerCarsModel.Cars.Where(x => x.IsExistingVehicleInAzureDB).ToList();
                    List<CarRegistrationModel> notExistingItems = registerCarsModel.Cars.Where(x => !x.IsExistingVehicleInAzureDB).ToList();

                    ServiceResult forceResponse = await ForceBulkRegistration(existingItems, "Force Registerment User");

                    if (forceResponse.Message.Contains("ERROR") || notExistingItems.Count == 0)
                    {
                        return forceResponse;
                    }
                    else
                    {
                        registerCarsModel.Cars = notExistingItems;
                    }
                }

                _carPoolNumberHelper.Generate(
                    CarBrand.Toyota,
                    registerCarsModel.Cars.FirstOrDefault().CarPool,
                    out string registrationId,
                    out string carPoolNumber);

                Console.WriteLine($"Created unique car pool number {carPoolNumber} and registration id {registrationId}");

                DateTime today = DateTime.Now.Date;
                foreach (CarRegistrationModel car in registerCarsModel.Cars)
                {
                    car.CarPoolNumber = carPoolNumber;
                    car.RegistrationId = registrationId;

                    // See Bug 281.
                    if (string.IsNullOrWhiteSpace(car.ErpRegistrationNumber))
                    {
                        // See Feature 182.
                        if (car.DeliveryDate == null)
                        {
                            DateTime delivery = today.AddDays(-1);
                            car.DeliveryDate = delivery;
                        }

                        // See Feature 182.
                        if (string.IsNullOrWhiteSpace(car.ErpDeliveryNumber))
                        {
                            car.ErpDeliveryNumber = registrationId;

                            Console.WriteLine($"Car {car.VehicleIdentificationNumber} has no value for Delivery Number: Setting default value to registration id {registrationId}");
                        }
                    }

                    bool hasMissingData = HasMissingData(car);
                    if (hasMissingData)
                    {
                        Console.WriteLine($"Car {car.VehicleIdentificationNumber} has missing data. " +
                            $"Set to transaction status {TransactionResult.MissingData.ToString()}");

                        car.TransactionState = TransactionResult.MissingData.ToString("D");
                    }
                }

                registerCarsModel.VendorId = registerCarsModel.Cars.Select(x => x.CompanyId).FirstOrDefault();
                registerCarsModel.CompanyId = registerCarsModel.VendorId;
                registerCarsModel.CustomerId = registerCarsModel.Cars.FirstOrDefault().CustomerId;

                RegisterCarsResult registrationResult = await new RegistrationService().SaveRegistrations(
                    registerCarsModel, claims, registrationId, identity, isForcedRegistration, CarBrand.Toyota);

                if (registerCarsModel.Cars.Any(x => x.TransactionState == TransactionResult.MissingData.ToString("D") == true)
                        && registerCarsModel.Cars.All(x => x.TransactionState == TransactionResult.MissingData.ToString("D")) == false)
                {
                    registerCarsModel.Cars = registerCarsModel
                        .Cars
                        .Where(x => x.TransactionState != TransactionResult.MissingData.ToString("D"))
                        .ToList();
                }

                if (registrationResult.AlreadyRegistered)
                {
                    serviceResult.Message = TransactionHelper.ALREADY_ENROLLED;
                    return serviceResult;
                }

                if (registrationResult.RegisteredCars != null && registrationResult.RegisteredCars.Count > 0)
                {
                    Console.WriteLine(
                        $"Registering {registrationResult.RegisteredCars.Count} cars for registration with id {registrationResult.RegistrationId}. " +
                        $"(RegistrationId = {registrationId})");

                    bool hasMissingData = HasMissingData(registerCarsModel.Cars.FirstOrDefault());

                    string transactionId = await _carRepository.BeginTransactionGenerateId(
                                            registerCarsModel.Cars.Select(x => x.VehicleIdentificationNumber).ToList(),
                                            registerCarsModel.CustomerId,
                                            registerCarsModel.CompanyId,
                                            RegistrationType.Register,
                                            identity);

                    if (!hasMissingData)
                    {
                        BulkRegistrationRequest requestPayload = null;
                        BulkRegistrationResponse apiTransactionResult = null;
                        try
                        {
                            requestPayload = await MapToModel(RegistrationType.Register, registerCarsModel, transactionId);
                            apiTransactionResult = await BulkRegistrationService.ExecuteRegistrationAsync(requestPayload);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"Registering cars for registration with id {registrationResult.RegistrationId} (RegistrationId = {registrationId}) failed. " +
                                $"Database transaction will be finished anyway: {ex}");
                        }

                        IList<int> identifier = await _carRepository.FinishTransactionAsync(RegistrationType.Register,
                            apiTransactionResult,
                            registrationResult.RegisteredCars,
                            registerCarsModel.CompanyId,
                            identity);

                        // Mapping to model that is excpected by the UI.
                        serviceResult = MapToModel(serviceResult,
                            apiTransactionResult,
                            requestPayload?.TransactionId,
                            identifier,
                            registrationId);
                    }
                    else
                    {
                        Console.WriteLine($"Car has missing data. Trying to set transaction status to {TransactionResult.MissingData}");

                        IEnumerable<IGrouping<string, CarRegistrationModel>> group = registerCarsModel.Cars.GroupBy(x => x.RegistrationId);
                        foreach (IGrouping<string, CarRegistrationModel> grp in group)
                        {
                            IList<CarRegistrationModel> dbApiCars = await CarLeasingRepository.GetApiRegisteredCarsAsync(grp.Key);
                            foreach (CarRegistrationModel dbApiCar in dbApiCars)
                            {
                                CarRegistrationDto dbCar = new CarRegistrationDto
                                {
                                    RegistrationId = dbApiCar.RegistrationId
                                };

                                dbCar.TransactionState = (int?)TransactionResult.MissingData;
                                await CarLeasingRepository.UpdateRegisteredCarAsync(dbCar, identity);

                                Console.WriteLine($"Updated car {dbApiCar.VehicleIdentificationNumber} to database. " +
                                    $"Car (serialized as JSON): {JsonConvert.SerializeObject(dbApiCar)}");
                            }
                        }

                        serviceResult.RegistrationId = registrationResult.RegistrationId;
                        serviceResult.Message = TransactionResult.MissingData.ToString();

                        Console.WriteLine($"Processing of bulk registration ended. Return data (serialized as JSON): {JsonConvert.SerializeObject(serviceResult)}");

                        return serviceResult;
                    }
                }
                else
                {
                    string uiResponseStatusMsg = string.Empty;
                    Console.WriteLine(
                        $"Nothing to do, the list of cars to register is empty! Returning empty result with HTTP 200. " +
                        $"(RegistrationId = {registrationResult.RegistrationId})");

                    IEnumerable<IGrouping<string, CarRegistrationModel>> group = registerCarsModel.Cars.GroupBy(x => x.RegistrationId);

                    foreach (IGrouping<string, CarRegistrationModel> grp in group)
                    {
                        IList<CarRegistrationModel> dbApiCars = await CarLeasingRepository.GetApiRegisteredCarsAsync(grp.Key);

                        foreach (CarRegistrationModel dbApiCar in dbApiCars)
                        {
                            CarRegistrationDto dbCar = new CarRegistrationDto
                            {
                                RegistrationId = dbApiCar.RegistrationId
                            };

                            bool hasMissingData = HasMissingData(dbApiCar);
                            if (registerCarsModel.DeactivateAutoRegistrationProcessing && !hasMissingData)
                            {
                                Console.WriteLine(
                                    $"Automatic registration is deactivated (value = {registerCarsModel.DeactivateAutoRegistrationProcessing})" +
                                    $"and contains all relevant data (HasMissingData = {hasMissingData}). " +
                                    $"Set the transaction status of car {dbApiCar.VehicleIdentificationNumber} to {TransactionResult.ActionRequired.ToString()}" +
                                    $"Car (serialized as JSON): {dbCar}");

                                dbCar.TransactionState = (int?)TransactionResult.ActionRequired;
                                uiResponseStatusMsg = ApiResult.WARNING.ToString();
                            }
                            else
                            {
                                Console.WriteLine(
                                    $"Automatic registration is activated (value = {registerCarsModel.DeactivateAutoRegistrationProcessing}) " +
                                    $"or car doesn't contain all relevant data (HasMissingData = {hasMissingData}) or both. " +
                                    $"Set the transaction status of car {dbApiCar.VehicleIdentificationNumber} to {TransactionResult.MissingData.ToString()}. " +
                                    $"Car (serialized as JSON): {dbCar}");

                                dbCar.TransactionState = (int?)TransactionResult.MissingData;
                                uiResponseStatusMsg = TransactionResult.MissingData.ToString();
                            }

                            await new CarRegistrationRepository(LeasingRegistrationRepository, BulkRegistrationService, _mapper).UpdateRegisteredCarAsync(dbCar, identity);
                        }
                    }

                    serviceResult.RegistrationId = registrationId;
                    serviceResult.Message = uiResponseStatusMsg;
                }

                return serviceResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving registration for {CarBrand.Toyota.ToString()} application: {ex}");
                throw ex;
            }
        }

        public bool HasMissingData(CarRegistrationModel car)
        {
            return (string.IsNullOrWhiteSpace(car.CompanyId))
                        || (string.IsNullOrWhiteSpace(car.VehicleIdentificationNumber))
                            || (string.IsNullOrWhiteSpace(car.CustomerId))
                                || car.DeliveryDate == null
                                    || (string.IsNullOrWhiteSpace(car.ErpDeliveryNumber));
        }

        private async Task<BulkRegistrationRequest> MapToModel(RegistrationType registrationType, RegisterCarsModel cars, string transactionId)
        {
            BulkRegistrationRequest requestModel = new BulkRegistrationRequest();
            List<DeliveryRequest> deliveryModels = new List<DeliveryRequest>();

            try
            {
                requestModel.RequestContext = await base.InitializeRequestContextAsync();

                requestModel.TransactionId = transactionId;
                requestModel.CompanyId = cars.CompanyId;
                requestModel.Registrations = new List<RegistrationRequest>();

                IEnumerable<IGrouping<string, CarRegistrationModel>> groups = cars.Cars.GroupBy(x => x.CarPoolNumber);

                foreach (IGrouping<string, CarRegistrationModel> registration in groups)
                {
                    DateTime registrationDate = registration.Min(item => item.RegistrationDate.GetValueOrDefault());
                    string convertedRegistrationDate = $"{registrationDate.Year}-{registrationDate.Month}-{registrationDate.Day}T{registrationDate.Hour:00}:{registrationDate.Minute:00}:{registrationDate.Second:00}Z";

                    if (registrationType != RegistrationType.Reset)
                    {
                        deliveryModels = GetDeliveryGroups(registration).ToList();
                    }

                    requestModel.Registrations.Add(new RegistrationRequest
                    {
                        RegistrationNumber = registration.Key,
                        CustomerId = cars.CustomerId,
                        RegistrationDate = convertedRegistrationDate,
                        RegistrationType = registrationType.ToString(),
                        Deliveries = deliveryModels
                    });
                }

                Console.WriteLine($"Mapping from registration to request model successful. Data (serialized as JSON): {JsonConvert.SerializeObject(requestModel)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mapping registration to request model failed. Data (serialized as JSON): {JsonConvert.SerializeObject(cars)}: {ex}");

                throw ex;
            }

            return requestModel;
        }

        private ServiceResult MapToModel(
            ServiceResult serviceResult, BulkRegistrationResponse registrationResponse,
            string internalTransactionId, IList<int> identifier, string registrationId = "n/a")
        {
            serviceResult.TransactionId = internalTransactionId;
            serviceResult.RegistrationId = registrationId;
            serviceResult.RegisteredCarIds = identifier.ToList();
            serviceResult.TransactionState = "n/a";

            if (registrationResponse != null)
            {
                if (registrationResponse.RegistrationId != null)
                {
                    serviceResult.Message = "SUCCESS";
                }
                else if (registrationResponse.TransactionId != null || registrationResponse.Errors?.Count > 0)
                {
                    serviceResult.Message = "ERROR";
                }
            }
            else
            {
                serviceResult.Message = "ERROR";
            }

            return serviceResult;
        }

        private IList<DeliveryRequest> GetDeliveryGroups(IEnumerable<CarRegistrationModel> cars)
        {
            List<DeliveryRequest> deliveryGroups = new List<DeliveryRequest>();

            List<List<IGrouping<string, CarRegistrationModel>>> groupedCars = cars
                .GroupBy(x => x.DeliveryDate)
                .Select(grp => grp.ToList())
                .ToList()
                .Select(y => y.ToList().GroupBy(z => z.ErpDeliveryNumber))
                .Select(grp => grp.ToList())
                .ToList();

            foreach (List<IGrouping<string, CarRegistrationModel>> group in groupedCars)
            {
                foreach (IGrouping<string, CarRegistrationModel> carList in group)
                {
                    List<CarRequest> carsOfGroup = new List<CarRequest>();
                    foreach (CarRegistrationModel car in carList)
                    {
                        carsOfGroup.Add(new CarRequest() { VehicleIdentificationNumber = car.VehicleIdentificationNumber, AssetTag = string.Empty });
                    }

                    DateTime deliveryDate = carList.FirstOrDefault().DeliveryDate.Value;
                    string convertedDeliveryDate = string.Format("{0}-{1}-{2}T{3:00}:{4:00}:{5:00}Z",
                        deliveryDate.Year, deliveryDate.Month, deliveryDate.Day, deliveryDate.Hour, deliveryDate.Minute, deliveryDate.Second);

                    deliveryGroups.Add(new DeliveryRequest()
                    {
                        DeliveryNumber = carList.FirstOrDefault().ErpDeliveryNumber,
                        DeliveryDate = convertedDeliveryDate,
                        Cars = carsOfGroup
                    });
                }
            }

            return deliveryGroups;
        }

        private async Task<ServiceResult> ForceBulkRegistration(IList<CarRegistrationModel> forceItems, string identity)
        {
            List<RegisterCarRequest> subsequentRegistrationRequestModels = new List<RegisterCarRequest>();
            RegistrationApiResponse subsequentRegistrationResponse = new RegistrationApiResponse();
            ServiceResult forceResponse = new ServiceResult();
            Dictionary<int, DateTime?> latestHistoryRowCreationDate = new Dictionary<int, DateTime?>();

            try
            {
                Console.WriteLine(
                            $"The registration with registration ids {string.Join(", ", forceItems.Select(x => x.RegistrationId))} has already been processed but forceRegisterment is true, " +
                            $"so the registration registration items will be registrationed again.");

                IList<CarRegistrationModel> currentDbCars = await CarLeasingRepository.GetApiRegisteredCarsAsync(forceItems.Select(x => x.VehicleIdentificationNumber).ToList());

                foreach (CarRegistrationModel forceRegisterCar in forceItems)
                {
                    CarRegistrationModel currentDbCar = currentDbCars
                                            .Where(y => y.VehicleIdentificationNumber == forceRegisterCar.VehicleIdentificationNumber)
                                            .FirstOrDefault();

                    latestHistoryRowCreationDate.Add(
                        currentDbCar.RegisteredCarId,
                        (await CarLeasingRepository.GetLatestCarHistoryEntryAsync(forceRegisterCar.VehicleIdentificationNumber)).RowCreationDate
                     );

                    AssignCarValuesForUpdate(currentDbCar, forceRegisterCar, identity, source: "Force Registerment");

                    // Map the car to the needed request model for a subsequent registration transaction.
                    RegisterCarRequest item = new RegisterCarRequest()
                    {
                        RegistrationNumber = currentDbCar.RegistrationId,
                        Car = forceRegisterCar.VehicleIdentificationNumber,
                        ErpRegistrationNumber = string.Empty,
                        CompanyId = forceRegisterCar.CompanyId,
                        CustomerId = forceRegisterCar.CustomerId,
                    };
                    subsequentRegistrationRequestModels.Add(item);
                }

                forceResponse.TransactionId = subsequentRegistrationResponse.ActionResult.FirstOrDefault().TransactionId;
                if (subsequentRegistrationResponse.Status != ApiResult.ERROR.ToString())
                {
                    forceResponse.Message = subsequentRegistrationResponse.Status;
                }
                else
                {
                    // Revert all force cars to data status of latest history item.
                    IEnumerable<ServiceResult> failedTransactions = subsequentRegistrationResponse.ActionResult.Where(x => x.Message == ApiResult.ERROR.ToString());
                    foreach (ServiceResult item in failedTransactions)
                    {
                        await HandleDataRevertAsync(item.RegisteredCarIds, identity);
                    }

                    throw new ForceRegistermentException("Subsequent registration transaction returned an error");
                }

                Console.WriteLine(
                    $"Forcing registration of an existing registration has been procecces. Return data (serialized as JSON): {JsonConvert.SerializeObject(forceResponse)}");
            }
            catch (ForceRegistermentException feEx)
            {
                Console.WriteLine(
                    $"Forced registration of an already existing registration registration failed. Values have been restored from car history." +
                    $"Data of forced registration (serialized as JSON): {JsonConvert.SerializeObject(forceItems)}: {feEx}");

                forceResponse.Message = "FORCE_ERROR";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Forced registration of an already existing registration failed due to unexpected reason." +
                    $"Data of forced registration (serialized as JSON): {JsonConvert.SerializeObject(forceItems)}: {ex}");

                forceResponse.Message = "FORCE_ERROR";
            }

            return forceResponse;
        }

        private async Task HandleDataRevertAsync(List<int> dbCarsToRevert, string identity, bool onlyForceRegistermentItems = true)
        {
            Console.WriteLine($"Trying to execute data revert of cars. " +
                $"Cars (serialized as JSON): {JsonConvert.SerializeObject(dbCarsToRevert)}, " +
                $"Azure User Identity: {identity}," +
                $"Only Force Registerment Items: {onlyForceRegistermentItems}");

            try
            {
                if (dbCarsToRevert != null && dbCarsToRevert.Count > 0)
                {
                    foreach (int id in dbCarsToRevert)
                    {
                        // Get all history items of car which should be reverted.
                        IEnumerable<CarRegistrationLogDto> carHistory = await CarLeasingRepository.GetCarHistoryAsync(id.ToString());

                        DateTime? rowCreationDate = null;
                        if (onlyForceRegistermentItems)
                        {
                            Console.WriteLine($"Get latest row creation date of car with ID {id} of 'Force Registerment User'");

                            // Extract the RowCreationDate of the item which contains the data to revert the car.
                            rowCreationDate = carHistory?
                                .Where(x => x.UserName != "Force Registerment User" && (string.IsNullOrWhiteSpace(x.TransactionType) || x.TransactionType != TransactionResult.Progress.ToString()))
                                .FirstOrDefault()
                                .RowCreationDate;
                        }
                        else
                        {
                            rowCreationDate = carHistory?
                                .Where(x => (string.IsNullOrWhiteSpace(x.TransactionType) || x.TransactionType != TransactionResult.Progress.ToString()))
                                .FirstOrDefault()
                                .RowCreationDate;
                        }

                        // Call method to revert the car.
                        bool isReverted = await RevertCarDataAsync(id, rowCreationDate, identity);

                        Console.WriteLine($"Revert completed with result: {isReverted}");
                    }
                }
                else
                {
                    Console.WriteLine($"List of cars to revert is empty. Revert not possible");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Unexpected error while handling data revert of cars: Revert failed." +
                    $"Cars (serialized as JSON): {JsonConvert.SerializeObject(dbCarsToRevert)}, " +
                    $"Azure User Identity: {identity}," +
                    $"Only Force Registerment Items: {onlyForceRegistermentItems}: {ex}");
            }
        }

        private async Task<bool> RevertCarDataAsync(int carDatasetId, DateTime? rowCreationDate, string identity)
        {
            Console.WriteLine($"Trying to revert car data to values of latest history item of car with ID {carDatasetId}");

            try
            {
                if (rowCreationDate != null)
                {

                    CarRegistrationModel currentCarData = await CarLeasingRepository.GetApiRegisteredCarAsync(carDatasetId);

                    var car = (await CarLeasingRepository.GetCarHistoryAsync(carDatasetId.ToString()))
                        .Where(x => x.RowCreationDate == rowCreationDate)
                        .FirstOrDefault();

                    CarRegistrationModel latestCarHistoryData = new CarRegistrationModel
                    {
                        RegistrationId = car.RegistrationId
                    };

                    Console.WriteLine(
                        $"Current car data (serialized as JSON): {JsonConvert.SerializeObject(currentCarData)}\n" +
                        $"Latest history data (serialized as JSON): {JsonConvert.SerializeObject(latestCarHistoryData)}");

                    latestCarHistoryData.ErrorNotificationSent = null;
                    AssignCarValuesForUpdate(currentCarData, latestCarHistoryData, identity, true);

                    return true;
                }
                else
                {
                    Console.WriteLine($"RowCreationDate is null, revert item cannot be identified. Aborting revert");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error while reading current data with id {carDatasetId} and revert item of date {rowCreationDate}: {ex}");
            }

            return false;
        }

        private void AssignCarValuesForUpdate(CarRegistrationModel carToUpdate, CarRegistrationModel carUpdateValues, string identity, bool saveWithHistory = false, string source = null)
        {
            Console.WriteLine($"Trying to assign values from revert item." +
                $"Car (serialized as JSON): {carToUpdate}, " +
                $"Revert Data (serialized as JSON): {carUpdateValues}");

            try
            {
                carToUpdate.ErpRegistrationNumber = carUpdateValues.ErpRegistrationNumber;
                carToUpdate.CompanyId = carUpdateValues.CompanyId;
                carToUpdate.CustomerId = carUpdateValues.CustomerId;
                carToUpdate.EmailAddresses = carUpdateValues.EmailAddresses;
                carToUpdate.CustomerRegistrationReference = carUpdateValues.CustomerRegistrationReference;
                carToUpdate.CarPool = carUpdateValues.CarPool;
                carToUpdate.ErrorNotificationSent = carUpdateValues.ErrorNotificationSent;

                carToUpdate.Source = source ?? carUpdateValues.Source;

                if (!string.IsNullOrWhiteSpace(carUpdateValues.CarPoolNumber))
                {
                    carToUpdate.CarPoolNumber = carUpdateValues.CarPoolNumber;
                }

                CarLeasingRepository.UpdateErpRegistrationItemAsync(_mapper.Map<ErpRegistermentRegistration>(carToUpdate));

                CarRegistrationDto dbCar = _mapper.Map<CarRegistrationDto>(carToUpdate);

                // Mapping ignores these two properties, so the values have to be set manually.
                Enum.TryParse(carToUpdate.TransactionType, out RegistrationType parsedTransactionType);
                Enum.TryParse(carToUpdate.TransactionState, out TransactionResult parsedTransactionStatus);
                dbCar.TransactionType = (int)parsedTransactionType;
                dbCar.TransactionState = (int)parsedTransactionStatus;

                CarLeasingRepository.UpdateRegisteredCarAsync(dbCar, identity, saveWithHistory);

                Console.WriteLine($"Reverted car data. Car (serialized as JSON): {JsonConvert.SerializeObject(dbCar)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Unexpected error while assingning values from revert item. " +
                    $"Car (serialized as JSON): {carToUpdate}, " +
                    $"Revert Data (serialized as JSON): {carUpdateValues}: {ex}");
            }
        }
    }
}
