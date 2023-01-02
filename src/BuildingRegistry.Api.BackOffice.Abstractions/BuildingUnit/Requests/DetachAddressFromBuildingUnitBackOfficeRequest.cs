using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace BuildingRegistry.Api.BackOffice.Abstractions.BuildingUnit.Requests
{
    [DataContract(Name = "OntkoppelAdres", Namespace = "")]
    public class DetachAddressFromBuildingUnitBackOfficeRequest
    {
        /// <summary>
        /// De unieke en persistente identificator van het adres.
        /// </summary>
        [DataMember(Name = "AdresId", Order = 0)]
        [JsonProperty(Required = Required.Always)]
        public string AdresId { get; set; }
    }
}