namespace BuildingRegistry.Api.Oslo.BuildingUnit.Responses
{
    using System.Runtime.Serialization;
    using System.Xml.Serialization;
    using Be.Vlaanderen.Basisregisters.GrAr.Legacy;
    using Be.Vlaanderen.Basisregisters.GrAr.Legacy.SpatialTools;
    using Newtonsoft.Json;

    /// <summary>
    /// De punt geometrie van het object.
    /// </summary>
    [DataContract(Name = "GebouweenheidPositie", Namespace = "")]
    public class BuildingUnitPosition
    {
        /// <summary>
        /// Een geometrie punt.
        /// </summary>
        [JsonProperty("geometrie")]
        [XmlIgnore]
        public GmlJsonPoint Geometry { get; set; }

        /// <summary>
        /// De geometriemethode van de gebouweenheidpositie.
        /// </summary>
        [DataMember(Name = "PositieGeometrieMethode", Order = 1)]
        [JsonProperty(Required = Required.DisallowNull)]
        public PositieGeometrieMethode GeometryMethod { get; set; }

        public BuildingUnitPosition(GmlJsonPoint geometry, PositieGeometrieMethode geometryMethod)
        {
            Geometry = geometry;
            GeometryMethod = geometryMethod;
        }
    }
}