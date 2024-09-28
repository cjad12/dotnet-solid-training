using AutoMapper;
using DevBasics.CarManagement.Dependencies;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;


namespace DevBasics.CarManagement
{
    internal sealed class Program
    {
        internal static async Task Main()
        {

	        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

            builder.Services.AddSingleton<CarRegistrationModel>();
            builder.Services.AddTransient<IHaveCustomMappings>(services => services.GetRequiredService<CarRegistrationModel>());

			builder.Services.AddSingleton<MapperConfiguration>(services =>
			{
                var model = services.GetRequiredService<IHaveCustomMappings>();
                return new MapperConfiguration(cnfgrtn => model.CreateMappings(cnfgrtn));
			});

			builder.Services.AddSingleton<IMapper>(services =>
			{
                var configuration = services.GetRequiredService<MapperConfiguration>();
				return configuration.CreateMapper();
			});

            builder.Services.AddSingleton<IBulkRegistrationService, BulkRegistrationServiceMock>();
            builder.Services.AddSingleton<ILeasingRegistrationRepository, LeasingRegistrationRepository>();
            builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
            builder.Services.AddSingleton<ICarRegistrationRepository, CarRegistrationRepository>();

            var registrationNumberFactory = new RegistrationNumberGeneratorFactory();
            registrationNumberFactory.RegisterGenerator(CarBrand.Ford, new FordRegistrationNumberGenerator());
            registrationNumberFactory.RegisterGenerator(CarBrand.Toyota, new ToyotaRegistrationNumberGenerator());
            registrationNumberFactory.RegisterGenerator(CarBrand.Undefined, new UndefinedRegistrationNumberGenerator());
            builder.Services.AddSingleton<IRegistrationNumberGeneratorResolver>(registrationNumberFactory);

            builder.Services.AddSingleton<ICarPoolNumberHelper, CarPoolNumberHelper>();
            builder.Services.AddSingleton<IGlobalizationSettings, GlobalizationSettings>();
            builder.Services.AddSingleton<IHttpHeaderSettings, HttpHeaderSettings>();
            builder.Services.AddSingleton<ICarManagementService, CarManagementService>();

            var host = builder.Build();

            var service = host.Services.GetRequiredService<ICarManagementService>();

            var result = await service.RegisterCarsAsync(
                new RegisterCarsModel
                {
                    CompanyId = "Company",
                    CustomerId = "Customer",
                    VendorId = "Vendor",
                    Cars = new List<CarRegistrationModel>
                    {
                        new CarRegistrationModel
                        {
                            CompanyId = "Company",
                            CustomerId = "Customer",
                            VehicleIdentificationNumber = Guid.NewGuid().ToString(),
                            DeliveryDate = DateTime.Now.AddDays(14).Date,
                            ErpDeliveryNumber = Guid.NewGuid().ToString()
                        }
                    }
                },
                false,
                new Claims());
        }
    }
}
