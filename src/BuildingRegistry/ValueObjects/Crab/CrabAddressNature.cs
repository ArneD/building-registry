namespace BuildingRegistry.ValueObjects.Crab
{
    using Be.Vlaanderen.Basisregisters.AggregateSource;
    using Newtonsoft.Json;

    public class CrabAddressNature : StringValueObject<CrabAddressNature>
    {
        public CrabAddressNature([JsonProperty("value")] string addressNature) : base(addressNature) { }
    }
}
