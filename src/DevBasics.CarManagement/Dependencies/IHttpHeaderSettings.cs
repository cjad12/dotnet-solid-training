namespace DevBasics.CarManagement.Dependencies;

public interface IHttpHeaderSettings
{
	string SalesOrgIdentifier { get; }
	CarBrand WebAppType { get; }
}