using System.ComponentModel.DataAnnotations;

namespace DriverTime.Application.Vehicles.DTOs;

public class UpdateVehicleVinDto
{
    [Required(ErrorMessage = "Podaj VIN pojazdu.")]
    [RegularExpression("^[A-HJ-NPR-Z0-9]{17}$", ErrorMessage = "VIN musi zawierać 17 liter i cyfr, bez liter I, O i Q.")]
    public string Vin { get; set; } = string.Empty;
}
