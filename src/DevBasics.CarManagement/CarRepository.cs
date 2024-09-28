using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevBasics.CarManagement.Dependencies;
using DevBasics.CarManagement.RegistrationStrategies;

namespace DevBasics.CarManagement;

public class CarRepository : ICarRepository
{
	private readonly ILeasingRegistrationRepository _leasingRegistrationRepository;
	private readonly ICarRegistrationRepository _carRegistrationRepository;
	private readonly IEnumerable<IRegistrationStrategy> _registrationStrategies;

	public CarRepository(ILeasingRegistrationRepository leasingRegistrationRepository, ICarRegistrationRepository carRegistrationRepository, IEnumerable<IRegistrationStrategy> registrationStrategies)
	{
		_leasingRegistrationRepository = leasingRegistrationRepository;
		_carRegistrationRepository = carRegistrationRepository;
		_registrationStrategies = registrationStrategies;
	}

	public async Task<string> BeginTransactionGenerateId(IList<string> cars,
		string customerId, string companyId, RegistrationType registrationType, string identity, string registrationNumber = null)
	{
		Console.WriteLine(
			$"Trying to generate internal database transaction and initialize the transaction. Cars: {string.Join(",  ", cars)} ");

		try
		{
			string transactionId = DateTime.Now.Ticks.ToString();
			if (transactionId.Length > 32)
			{
				transactionId = transactionId.Substring(0, 32);
			}

			return await BeginTransactionAsync(cars, customerId, companyId, registrationType, identity, transactionId, registrationNumber);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Generating internal Transaction ID and initializing transaction failed. Cars: {string.Join(", ", cars)}: {ex}");

			throw ex;
		}
	}

	public async Task<string> BeginTransactionAsync(IList<string> cars,
		string customerId, string companyId, RegistrationType registrationType, string identity,
		string transactionId = null, string registrationNumber = null)
	{
		Console.WriteLine(
			$"Trying to begin internal database transaction. Cars: {string.Join(",  ", cars)}");

		try
		{
			IList<CarRegistrationDto> dbCarsToUpdate = await _carRegistrationRepository.GetCarsAsync(cars);
			foreach (CarRegistrationDto carToUpdate in dbCarsToUpdate)
			{
				if (!string.IsNullOrWhiteSpace(transactionId))
				{
					carToUpdate.TransactionId = transactionId;
				}

				if (!string.IsNullOrWhiteSpace(registrationNumber))
				{
					carToUpdate.CarPoolNumber = registrationNumber;
				}

				carToUpdate.TransactionEndDate = null;
				carToUpdate.ErrorMessage = string.Empty;
				carToUpdate.ErrorCode = null;

				carToUpdate.TransactionType = (int)registrationType;
				//carToUpdate.TransactionState = (int)TransactionResult.Progress;
				carToUpdate.TransactionState = carToUpdate.TransactionState ?? (int)TransactionResult.NotRegistered;

				Console.WriteLine(
					$"Car hasn't got missing data. Setting status to {carToUpdate.TransactionState}");

				carToUpdate.TransactionStartDate = DateTime.Now;

				Console.WriteLine(
					$"Trying to update car {carToUpdate.CarIdentificationNumber} in database...");

				await _leasingRegistrationRepository.UpdateCarAsync(carToUpdate);
				await _leasingRegistrationRepository.InsertHistoryAsync(carToUpdate,
					identity,
					((carToUpdate.TransactionState.HasValue) ? Enum.GetName(typeof(TransactionResult), (int)carToUpdate.TransactionState) : null),
					((carToUpdate.TransactionType.HasValue) ? Enum.GetName(typeof(RegistrationType), (int)carToUpdate.TransactionType) : null)
				);
			}

			Console.WriteLine(
				$"Beginning internal database transaction ended. Cars: {string.Join(",  ", cars)}, " +
				$"Returning internal Transaction ID: {transactionId}");

			return transactionId;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Beginning internal database transaction failed. Cars: {string.Join(",  ", cars)}: {ex}");

			throw new Exception("Beginning internal database transaction failed", ex);
		}
	}

	public async Task<IList<int>> FinishTransactionAsync(RegistrationType registrationType,
		BulkRegistrationResponse apiResponse, IList<string> carIdentifier, string companyId, string identity,
		string transactionStateBackup = null, BulkRegistrationRequest requestModel = null)
	{
		Console.WriteLine($"Trying to finish database transaction after bulk registration (Type {registrationType.ToString()})...");

		List<int> updateResult = new List<int>();

		try
		{
			// Get the cars from database.
			IList<CarRegistrationDto> dbCars = await _carRegistrationRepository.GetCarsAsync(carIdentifier);
			foreach (CarRegistrationDto dbCar in dbCars)
			{
				Console.WriteLine($"Now processing car {dbCar.RegisteredCarId}...");

				dbCar.TransactionType = (int)registrationType;
				dbCar.CompanyId = companyId;

				TransactionResult newTransactionState = await GetTransactionResult(apiResponse, dbCar, registrationType, transactionStateBackup);
				string parsedTransactionStateBackup = Enum.GetName(typeof(TransactionResult), (!string.IsNullOrWhiteSpace(transactionStateBackup))
					? int.Parse(transactionStateBackup)
					: (int)TransactionResult.None);

				Console.WriteLine(
					$"Initial new transaction status: {newTransactionState.ToString()}, Backup old transaction status: {parsedTransactionStateBackup}");

				if (apiResponse != null)
				{
					if ((newTransactionState.ToString() == parsedTransactionStateBackup && apiResponse.Response != "SUCCESS")
					    || newTransactionState == TransactionResult.Failed
					    || (newTransactionState == TransactionResult.NotRegistered && dbCar.TransactionType != (int)RegistrationType.Unregister))
					{
						Console.WriteLine(
							$"An error occured or the transaction could not be processed (new transaction status is the old transaction status from car logs)." +
							$"Closing the transaction");

						Tuple<string, string, string> errorValues = GetErrorValues(apiResponse);
						if (errorValues != null)
						{
							dbCar.ErrorCode = errorValues.Item1;
							dbCar.ErrorMessage = errorValues.Item2;
							dbCar.AccTransactionId = errorValues.Item3;
						}

						// if an error occurred or the transaction could not be processed (new transaction state is the old transaction state)
						// close the transaction.
						dbCar.TransactionState = (newTransactionState != TransactionResult.None) ? (int?)newTransactionState : null;
						dbCar.TransactionEndDate = DateTime.Now;
					}
					else
					{
						Console.WriteLine(
							$"Set car {dbCar.CarIdentificationNumber} to status {TransactionResult.Progress.ToString()} " +
							$"and ACC Transaction ID {apiResponse.RegistrationId}");

						dbCar.TransactionState = (int)TransactionResult.Progress;
						dbCar.AccTransactionId = apiResponse.RegistrationId;
					}
				}

				Console.WriteLine(
					$"Trying to update car {dbCar.CarIdentificationNumber} in database...");

				int result = await _carRegistrationRepository.UpdateRegisteredCarAsync(dbCar, identity);

				if (result != -1)
				{
					updateResult.Add(dbCar.RegisteredCarId);
				}
			}

			Console.WriteLine($"Trying to finish database transaction after bulk registration (Type {registrationType.ToString()}) ended.");

			return updateResult;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Finishing database transaction after bulk registration (Type {registrationType.ToString()}) failed: {ex}");

			throw new Exception("Finishing database transaction after bulk registration failed", ex);
		}
	}

	private async Task<TransactionResult> GetTransactionResult(BulkRegistrationResponse apiResponse,
		CarRegistrationDto dbCar, RegistrationType registrationType, string transactionStateBackup)
	{
		Console.WriteLine($"Trying to get the transaction result for car {dbCar.CarIdentificationNumber}...");

		try
		{
			Enum.TryParse(transactionStateBackup, out TransactionResult oldTxState);
			if (apiResponse == null)
			{
				if (transactionStateBackup != null)
				{
					return oldTxState;
				}

				if (registrationType == RegistrationType.Register)
				{
					return TransactionResult.NotRegistered;
				}

				return TransactionResult.Failed;
			}

			bool responsibleRegistrationStrategyFound = false;
			foreach (IRegistrationStrategy registrationStrategy in _registrationStrategies)
			{
				if (!registrationStrategy.CanProcess(registrationType))
					continue;

				responsibleRegistrationStrategyFound = true;

				TransactionResult? registrationResult = await registrationStrategy.Process(registrationType, apiResponse, dbCar, oldTxState);
				if (registrationResult.HasValue)
					return registrationResult.Value;
			}

			if (!responsibleRegistrationStrategyFound)
			{
				Console.WriteLine($"BulkRegistrationType not valid. Transaction result cannot be determined.");
			}

			// If the algorithm executes to this point no transaction state change.
			Console.WriteLine($"Could not determine new transaction result. Transaction state before process was initiated is returned:  {oldTxState}");

			return oldTxState;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Getting transaction result for database by API response failed: {ex}");

			throw new Exception("Getting transaction result for database by API response failed", ex);
		}
	}

	private Tuple<string, string, string> GetErrorValues(BulkRegistrationResponse apiResponse)
	{
		// In case of TransactionId contains a value, a Ship-To-Error occured and only one error message was received.
		if (apiResponse.TransactionId != null)
		{
			return Tuple.Create(apiResponse.ErrorCode, apiResponse.ErrorMessage, apiResponse.TransactionId);
		}
		else if (apiResponse.Errors != null)
		{
			if (apiResponse.Errors.Count > 1)
			{
				return Tuple.Create("MULTI", string.Join(" // ", apiResponse.Errors), apiResponse.RegistrationId);
			}
			else if (apiResponse.Errors.Count == 1)
			{
				return Tuple.Create(apiResponse.Errors.FirstOrDefault(), apiResponse.Errors.FirstOrDefault(), string.Empty);
			}
		}

		return null;
	}
}