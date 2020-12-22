namespace BuildingRegistry.Building.Events
{
    using System;
    using Be.Vlaanderen.Basisregisters.EventHandling;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using Newtonsoft.Json;
    using ValueObjects;

    [EventName("BuildingMeasurementByGrbWasCorrected")]
    [EventDescription("Gebouw werd ingemeten door GRB via correctie.")]
    public class BuildingMeasurementByGrbWasCorrected : IHasProvenance, ISetProvenance
    {
        public Guid BuildingId { get; }
        public string ExtendedWkbGeometry { get; }
        public ProvenanceData Provenance { get; private set; }

        public BuildingMeasurementByGrbWasCorrected(
            BuildingId buildingId,
            ExtendedWkbGeometry geometry)
        {
            BuildingId = buildingId;
            ExtendedWkbGeometry = geometry.ToString();
        }

        [JsonConstructor]
        private BuildingMeasurementByGrbWasCorrected(
            Guid buildingId,
            string extendedWkbGeometry,
            ProvenanceData provenance)
            : this(
                new BuildingId(buildingId),
                new ExtendedWkbGeometry(extendedWkbGeometry)) => ((ISetProvenance)this).SetProvenance(provenance.ToProvenance());

        void ISetProvenance.SetProvenance(Provenance provenance) => Provenance = new ProvenanceData(provenance);
    }
}
