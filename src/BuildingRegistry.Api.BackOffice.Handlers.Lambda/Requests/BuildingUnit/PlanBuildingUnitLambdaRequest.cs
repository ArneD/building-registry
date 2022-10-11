namespace BuildingRegistry.Api.BackOffice.Handlers.Lambda.Requests.BuildingUnit
{
    using Abstractions;
    using BuildingRegistry.Api.BackOffice.Abstractions.Building;
    using BuildingRegistry.Api.BackOffice.Abstractions.BuildingUnit.Converters;
    using BuildingRegistry.Api.BackOffice.Abstractions.BuildingUnit.Requests;
    using BuildingRegistry.Building;
    using BuildingRegistry.Building.Commands;

    public sealed class PlanBuildingUnitLambdaRequest :
        BuildingUnitLambdaRequest,
        IHasBackOfficeRequest<BackOfficePlanBuildingUnitRequest>
    {
        public BackOfficePlanBuildingUnitRequest Request { get; set; }

        /// <summary>
        /// Map to PlanBuildingUnit command
        /// </summary>
        /// <returns>PlanBuildingUnit.</returns>
        public PlanBuildingUnit ToCommand(
            BuildingUnitPersistentLocalId buildingUnitPersistentLocalId)
        {
            return new PlanBuildingUnit(
                BuildingPersistentLocalId,
                buildingUnitPersistentLocalId,
                Request.PositieGeometrieMethode.Map(),
                string.IsNullOrWhiteSpace(Request.Positie) ? null : Request.Positie.ToExtendedWkbGeometry(),
                Request.Functie.Map(),
                Request.AfwijkingVastgesteld,
                Provenance);
        }
    }
}