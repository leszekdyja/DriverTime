using DriverTime.Application.Planning;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningAssignmentValidationService
{
    void ValidateManualAssignment(PlanningAssignmentValidationRequest request);
}
