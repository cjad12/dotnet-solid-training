﻿namespace DevBasics.CarManagement.RegistrationNumberGenerators;

public interface IRegistrationNumberGenerator
{
	string GenerateRegistrationNumber(string endCustomerRegistrationReference, string registrationRegistrationId);
}