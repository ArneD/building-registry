namespace BuildingRegistry.Tests.ProjectionTests.Legacy
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoFixture;
    using Be.Vlaanderen.Basisregisters.GrAr.Common.Pipes;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using Be.Vlaanderen.Basisregisters.ProjectionHandling.SqlStreamStore;
    using Be.Vlaanderen.Basisregisters.Utilities.HexByteConvertor;
    using Building;
    using Building.Events;
    using Extensions;
    using Fixtures;
    using FluentAssertions;
    using Projections.Legacy.BuildingUnitDetailV2WithCount;
    using Tests.Legacy.Autofixture;
    using Xunit;

    public partial class BuildingUnitDetailsV2Tests : BuildingLegacyProjectionTest<BuildingUnitDetailV2Projections>
    {
        private readonly Fixture _fixture = new Fixture();

        public BuildingUnitDetailsV2Tests()
        {
            _fixture.Customize(new InfrastructureCustomization());
            _fixture.Customize(new WithBuildingStatus());
            _fixture.Customize(new WithBuildingGeometryMethod());
            _fixture.Customize(new WithValidExtendedWkbPolygon());
            _fixture.Customize(new WithBuildingUnitStatus());
            _fixture.Customize(new WithBuildingUnitFunction());
            _fixture.Customize(new WithBuildingUnitPositionGeometryMethod());
            _fixture.Customizations.Add(new WithUniqueInteger());
        }

        [Theory]
        [InlineData("Planned")]
        [InlineData("UnderConstruction")]
        [InlineData("Realized")]
        [InlineData("Retired")]
        [InlineData("NotRealized")]
        public async Task WhenBuildingWasMigrated(string buildingStatus)
        {
            _fixture.Register(() => BuildingStatus.Parse(buildingStatus));

            var buildingWasMigrated = _fixture.Create<BuildingWasMigrated>();
            var metadata = new Dictionary<string, object>
            {
                {AddEventHashPipe.HashMetadataKey, buildingWasMigrated.GetHash()}
            };

            await Sut
                .Given(new Envelope<BuildingWasMigrated>(new Envelope(buildingWasMigrated, metadata)))
                .Then(async ct =>
                {
                    var buildingUnits = ct.BuildingUnitDetailsV2WithCount
                        .Where(unit => unit.BuildingPersistentLocalId == buildingWasMigrated.BuildingPersistentLocalId)
                        .ToList();

                    foreach (var unit in buildingWasMigrated.BuildingUnits)
                    {
                        var expectedUnit = buildingUnits
                            .Single(x => x.BuildingUnitPersistentLocalId == unit.BuildingUnitPersistentLocalId);

                        expectedUnit.BuildingPersistentLocalId.Should()
                            .Be(buildingWasMigrated.BuildingPersistentLocalId);
                        expectedUnit.IsRemoved.Should().Be(unit.IsRemoved);
                        expectedUnit.Status.Status.Should().Be(unit.Status);
                        expectedUnit.HasDeviation.Should().BeFalse();
                        expectedUnit.Function.Function.Should().Be(unit.Function);
                        expectedUnit.PositionMethod.GeometryMethod.Should().Be(unit.GeometryMethod);
                        expectedUnit.Version.Should().Be(buildingWasMigrated.Provenance.Timestamp);
                        expectedUnit.Position.Should().BeEquivalentTo(unit.ExtendedWkbGeometry.ToByteArray());
                        expectedUnit.Addresses.Should().NotBeEmpty();
                        expectedUnit.Addresses.Should().BeEquivalentTo(unit.AddressPersistentLocalIds.Select(x =>
                            new BuildingUnitDetailAddressItemV2
                            (
                                unit.BuildingUnitPersistentLocalId,
                                x
                            )));

                        expectedUnit.LastEventHash.Should().Be(buildingWasMigrated.GetHash());
                    }
                });
        }

        [Fact]
        public async Task WhenBuildingOutlineWasChanged()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            var @event = _fixture.Create<BuildingOutlineWasChanged>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingOutlineWasChanged>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object>
                            {
                                {AddEventHashPipe.HashMetadataKey, @event.GetHash()}
                            })))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(buildingUnitWasPlannedV2
                        .BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.PositionMethod.Should().Be(BuildingUnitPositionGeometryMethod.DerivedFromObject);
                    item.Position.Should().BeEquivalentTo(@event.ExtendedWkbGeometryBuildingUnits!.ToByteArray());
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                });
        }

        [Fact]
        public async Task WhenBuildingWasMeasured()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            var @event = _fixture.Create<BuildingWasMeasured>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingWasMeasured>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> { {AddEventHashPipe.HashMetadataKey, @event.GetHash()} })))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.PositionMethod.Should().Be(BuildingUnitPositionGeometryMethod.DerivedFromObject);
                    item.Position.Should().BeEquivalentTo(@event.ExtendedWkbGeometryBuildingUnits!.ToByteArray());
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                });
        }

        [Fact]
        public async Task WhenBuildingMeasurementWasCorrected()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            var @event = _fixture.Create<BuildingMeasurementWasCorrected>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingMeasurementWasCorrected>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> { {AddEventHashPipe.HashMetadataKey, @event.GetHash()} })))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.PositionMethod.Should().Be(BuildingUnitPositionGeometryMethod.DerivedFromObject);
                    item.Position.Should().BeEquivalentTo(@event.ExtendedWkbGeometryBuildingUnits!.ToByteArray());
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                });
        }

        [Fact]
        public async Task WhenBuildingMeasurementWasChanged()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            var @event = _fixture.Create<BuildingMeasurementWasChanged>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingMeasurementWasChanged>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object>
                            {
                                {AddEventHashPipe.HashMetadataKey, @event.GetHash()}
                            })))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(buildingUnitWasPlannedV2
                        .BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.PositionMethod.Should().Be(BuildingUnitPositionGeometryMethod.DerivedFromObject);
                    item.Position.Should().BeEquivalentTo(@event.ExtendedWkbGeometryBuildingUnits!.ToByteArray());
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasPlannedV2()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var metadata = new Dictionary<string, object>
            {
                {AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}
            };

            await Sut
                .Given(new Envelope<BuildingUnitWasPlannedV2>(new Envelope(buildingUnitWasPlannedV2, metadata)))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(buildingUnitWasPlannedV2
                        .BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.BuildingPersistentLocalId.Should().Be(buildingUnitWasPlannedV2.BuildingPersistentLocalId);
                    item.Position.Should().BeEquivalentTo(buildingUnitWasPlannedV2.ExtendedWkbGeometry.ToByteArray());
                    item.PositionMethod.Should()
                        .Be(BuildingUnitPositionGeometryMethod.Parse(buildingUnitWasPlannedV2.GeometryMethod));
                    item.Function.Should().Be(BuildingUnitFunction.Parse(buildingUnitWasPlannedV2.Function));
                    item.Version.Should().Be(buildingUnitWasPlannedV2.Provenance.Timestamp);
                    item.IsRemoved.Should().BeFalse();
                    item.Status.Should().Be(BuildingUnitStatus.Planned);
                    item.HasDeviation.Should().Be(buildingUnitWasPlannedV2.HasDeviation);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasRealizedV2()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            var @event = _fixture.Create<BuildingUnitWasRealizedV2>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRealizedV2>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object>
                            {
                                {AddEventHashPipe.HashMetadataKey, @event.GetHash()}
                            })))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.Realized);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasRealizedBecauseBuildingWasRealized()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            var @event = _fixture.Create<BuildingUnitWasRealizedBecauseBuildingWasRealized>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRealizedBecauseBuildingWasRealized>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object>
                            {
                                {AddEventHashPipe.HashMetadataKey, @event.GetHash()}
                            })))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.Realized);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasCorrectedFromRealizedToPlanned()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitWasRealizedV2 = _fixture.Create<BuildingUnitWasRealizedV2>();

            var @event = _fixture.Create<BuildingUnitWasCorrectedFromRealizedToPlanned>();

            await Sut
                .Given(new Envelope<BuildingUnitWasPlannedV2>(new Envelope(buildingUnitWasPlannedV2,
                        new Dictionary<string, object>
                        {
                            {AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}
                        })),
                    new Envelope<BuildingUnitWasRealizedV2>(new Envelope(buildingUnitWasRealizedV2,
                        new Dictionary<string, object>
                        {
                            {AddEventHashPipe.HashMetadataKey, buildingUnitWasRealizedV2.GetHash()}
                        })),
                    new Envelope<BuildingUnitWasCorrectedFromRealizedToPlanned>(new Envelope(@event,
                        new Dictionary<string, object>
                        {
                            {AddEventHashPipe.HashMetadataKey, @event.GetHash()}
                        })))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item.IsRemoved.Should().BeFalse();
                    item.Status.Should().Be(BuildingUnitStatus.Planned);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasCorrectedFromRealizedToPlannedBecauseBuildingWasCorrected()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitWasRealizedV2 = _fixture.Create<BuildingUnitWasRealizedV2>();
            var @event = _fixture.Create<BuildingUnitWasCorrectedFromRealizedToPlannedBecauseBuildingWasCorrected>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRealizedV2>(
                        new Envelope(
                            buildingUnitWasRealizedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasRealizedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasCorrectedFromRealizedToPlannedBecauseBuildingWasCorrected>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.Planned);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasNotRealizedV2()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            var @event = _fixture.Create<BuildingUnitWasNotRealizedV2>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasNotRealizedV2>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.NotRealized);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasNotRealizedBecauseBuildingWasNotRealized()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            var @event = _fixture.Create<BuildingUnitWasNotRealizedBecauseBuildingWasNotRealized>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasNotRealizedBecauseBuildingWasNotRealized>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.NotRealized);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasCorrectedFromNotRealizedToPlanned()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitWasNotRealizedV2 = _fixture.Create<BuildingUnitWasNotRealizedV2>();
            var @event = _fixture.Create<BuildingUnitWasCorrectedFromNotRealizedToPlanned>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasNotRealizedV2>(
                        new Envelope(
                            buildingUnitWasNotRealizedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasNotRealizedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasCorrectedFromNotRealizedToPlanned>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.Planned);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasRetiredV2()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitWasRealizedV2 = _fixture.Create<BuildingUnitWasRealizedV2>();
            var @event = _fixture.Create<BuildingUnitWasRetiredV2>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRealizedV2>(
                        new Envelope(
                            buildingUnitWasRealizedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasRealizedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRetiredV2>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.Retired);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasCorrectedFromRetiredToRealized()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitWasRetiredV2 = _fixture.Create<BuildingUnitWasRetiredV2>();
            var @event = _fixture.Create<BuildingUnitWasCorrectedFromRetiredToRealized>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRetiredV2>(
                        new Envelope(
                            buildingUnitWasRetiredV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasRetiredV2.GetHash()}})),
                    new Envelope<BuildingUnitWasCorrectedFromRetiredToRealized>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.Realized);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitPositionWasCorrected()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            ((ISetProvenance) buildingUnitWasPlannedV2).SetProvenance(_fixture.Create<Provenance>());


            var @event = new BuildingUnitPositionWasCorrected(
                new BuildingPersistentLocalId(buildingUnitWasPlannedV2.BuildingPersistentLocalId),
                new BuildingUnitPersistentLocalId(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId),
                BuildingUnitPositionGeometryMethod.Parse("AppointedByAdministrator"),
                new ExtendedWkbGeometry(GeometryHelper.ValidPointInPolygon.AsBinary()));
            ((ISetProvenance) @event).SetProvenance(_fixture.Create<Provenance>());

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitPositionWasCorrected>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item.BuildingPersistentLocalId.Should().Be(@event.BuildingPersistentLocalId);
                    item.Position.Should().BeEquivalentTo(@event.ExtendedWkbGeometry.ToByteArray());
                    item.PositionMethod.Should().Be(BuildingUnitPositionGeometryMethod.Parse(@event.GeometryMethod));
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                    item.IsRemoved.Should().BeFalse();
                    item.Status.Should().Be(BuildingUnitStatus.Planned);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasRemovedV2()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();

            ((ISetProvenance) buildingUnitWasPlannedV2).SetProvenance(_fixture.Create<Provenance>());


            var @event = new BuildingUnitWasRemovedV2(
                new BuildingPersistentLocalId(buildingUnitWasPlannedV2.BuildingPersistentLocalId),
                new BuildingUnitPersistentLocalId(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId));
            ((ISetProvenance) @event).SetProvenance(_fixture.Create<Provenance>());

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRemovedV2>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.Version.Should().Be(@event.Provenance.Timestamp);
                    item.IsRemoved.Should().BeTrue();
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasRemovedBecauseBuildingWasRemoved()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var @event = new BuildingUnitWasRemovedBecauseBuildingWasRemoved(
                new BuildingPersistentLocalId(buildingUnitWasPlannedV2.BuildingPersistentLocalId),
                new BuildingUnitPersistentLocalId(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId));
            ((ISetProvenance) @event).SetProvenance(_fixture.Create<Provenance>());

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRemovedBecauseBuildingWasRemoved>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.Version.Should().Be(@event.Provenance.Timestamp);
                    item.IsRemoved.Should().BeTrue();
                });
        }

        [Fact]
        public async Task WhenBuildingUnitRemovalWasCorrected()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitWasRemovedV2 = _fixture.Create<BuildingUnitWasRemovedV2>();
            var @event = _fixture.Create<BuildingUnitRemovalWasCorrected>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRemovedV2>(
                        new Envelope(
                            buildingUnitWasRemovedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasRemovedV2.GetHash()}})),
                    new Envelope<BuildingUnitRemovalWasCorrected>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.Status.Should().Be(BuildingUnitStatus.Parse(@event.BuildingUnitStatus));
                    item.HasDeviation.Should().Be(@event.HasDeviation);
                    item.Function.Should().Be(BuildingUnitFunction.Parse(@event.Function));
                    item.Position.Should().BeEquivalentTo(@event.ExtendedWkbGeometry.ToByteArray());
                    item.PositionMethod.Should().Be(BuildingUnitPositionGeometryMethod.Parse(@event.GeometryMethod));
                    item.IsRemoved.Should().BeFalse();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                    item.Addresses.Should().BeEmpty();
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasRegularized()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>()
                .WithDeviation(true);

            var @event = new BuildingUnitWasRegularized(
                new BuildingPersistentLocalId(buildingUnitWasPlannedV2.BuildingPersistentLocalId),
                new BuildingUnitPersistentLocalId(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId));
            ((ISetProvenance) @event).SetProvenance(_fixture.Create<Provenance>());

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRegularized>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.HasDeviation.Should().BeFalse();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitRegularizationWasCorrected()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>()
                .WithDeviation(false);

            var @event = new BuildingUnitRegularizationWasCorrected(
                new BuildingPersistentLocalId(buildingUnitWasPlannedV2.BuildingPersistentLocalId),
                new BuildingUnitPersistentLocalId(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId));
            ((ISetProvenance) @event).SetProvenance(_fixture.Create<Provenance>());

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitRegularizationWasCorrected>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.HasDeviation.Should().BeTrue();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasDeregulated()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>()
                .WithDeviation(false);

            var @event = new BuildingUnitWasDeregulated(
                new BuildingPersistentLocalId(buildingUnitWasPlannedV2.BuildingPersistentLocalId),
                new BuildingUnitPersistentLocalId(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId));
            ((ISetProvenance) @event).SetProvenance(_fixture.Create<Provenance>());

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasDeregulated>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.HasDeviation.Should().BeTrue();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitDeregulationWasCorrected()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>()
                .WithDeviation(true);

            var @event = new BuildingUnitDeregulationWasCorrected(
                new BuildingPersistentLocalId(buildingUnitWasPlannedV2.BuildingPersistentLocalId),
                new BuildingUnitPersistentLocalId(buildingUnitWasPlannedV2.BuildingUnitPersistentLocalId));
            ((ISetProvenance) @event).SetProvenance(_fixture.Create<Provenance>());

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitDeregulationWasCorrected>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.HasDeviation.Should().BeFalse();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                });
        }

        [Fact]
        public async Task WhenCommonBuildingUnitWasAddedV2()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var commonBuildingUnitWasAddedV2 = new CommonBuildingUnitWasAddedV2(
                _fixture.Create<BuildingPersistentLocalId>(),
                _fixture.Create<BuildingUnitPersistentLocalId>(),
                BuildingUnitStatus.Planned,
                BuildingUnitPositionGeometryMethod.DerivedFromObject,
                _fixture.Create<ExtendedWkbGeometry>(),
                false);
            ((ISetProvenance) commonBuildingUnitWasAddedV2).SetProvenance(_fixture.Create<Provenance>());

            var metadata = new Dictionary<string, object>
            {
                {AddEventHashPipe.HashMetadataKey, commonBuildingUnitWasAddedV2.GetHash()}
            };

            await Sut
                .Given(new Envelope<CommonBuildingUnitWasAddedV2>(new Envelope(commonBuildingUnitWasAddedV2, metadata)))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(commonBuildingUnitWasAddedV2
                        .BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.BuildingPersistentLocalId.Should().Be(commonBuildingUnitWasAddedV2.BuildingPersistentLocalId);
                    item.Position.Should()
                        .BeEquivalentTo(commonBuildingUnitWasAddedV2.ExtendedWkbGeometry.ToByteArray());
                    item.PositionMethod.Should()
                        .Be(BuildingUnitPositionGeometryMethod.Parse(commonBuildingUnitWasAddedV2.GeometryMethod));
                    item.Function.Should()
                        .Be(BuildingUnitFunction.Common);
                    item.Version.Should().Be(commonBuildingUnitWasAddedV2.Provenance.Timestamp);
                    item.IsRemoved.Should().BeFalse();
                    item.Status.Should().Be(BuildingUnitStatus.Parse(commonBuildingUnitWasAddedV2.BuildingUnitStatus));
                    item.HasDeviation.Should().Be(commonBuildingUnitWasAddedV2.HasDeviation);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitAddressWasAttachedV2()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var @event = _fixture.Create<BuildingUnitAddressWasAttachedV2>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasAttachedV2>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Addresses.Should().HaveCount(1);
                    item.Addresses[0].AddressPersistentLocalId.Should().Be(@event.AddressPersistentLocalId);
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                    item.LastEventHash.Should().Be(@event.GetHash());
                });
        }

        [Fact]
        public async Task WhenBuildingUnitAddressWasDetachedV2()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());
            _fixture.Customize(new WithFixedAddressPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitAddressWasAttached = _fixture.Create<BuildingUnitAddressWasAttachedV2>();
            var @event = _fixture.Create<BuildingUnitAddressWasDetachedV2>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasAttachedV2>(
                        new Envelope(
                            buildingUnitAddressWasAttached,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitAddressWasAttached.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasDetachedV2>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Addresses.Should().BeEmpty();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                    item.LastEventHash.Should().Be(@event.GetHash());
                });
        }

        [Fact]
        public async Task WhenBuildingUnitAddressWasDetachedBecauseAddressWasRetired()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());
            _fixture.Customize(new WithFixedAddressPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitAddressWasAttached = _fixture.Create<BuildingUnitAddressWasAttachedV2>();
            var @event = _fixture.Create<BuildingUnitAddressWasDetachedBecauseAddressWasRetired>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasAttachedV2>(
                        new Envelope(
                            buildingUnitAddressWasAttached,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitAddressWasAttached.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasDetachedBecauseAddressWasRetired>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Addresses.Should().BeEmpty();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                    item.LastEventHash.Should().Be(@event.GetHash());
                });
        }

        [Fact]
        public async Task WhenBuildingUnitAddressWasDetachedBecauseAddressWasRejected()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());
            _fixture.Customize(new WithFixedAddressPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitAddressWasAttached = _fixture.Create<BuildingUnitAddressWasAttachedV2>();
            var @event = _fixture.Create<BuildingUnitAddressWasDetachedBecauseAddressWasRejected>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasAttachedV2>(
                        new Envelope(
                            buildingUnitAddressWasAttached,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitAddressWasAttached.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasDetachedBecauseAddressWasRejected>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Addresses.Should().BeEmpty();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                    item.LastEventHash.Should().Be(@event.GetHash());
                });
        }

        [Fact]
        public async Task WhenBuildingUnitAddressWasDetachedBecauseAddressWasRemoved()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());
            _fixture.Customize(new WithFixedAddressPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitAddressWasAttached = _fixture.Create<BuildingUnitAddressWasAttachedV2>();
            var @event = _fixture.Create<BuildingUnitAddressWasDetachedBecauseAddressWasRemoved>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasAttachedV2>(
                        new Envelope(
                            buildingUnitAddressWasAttached,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitAddressWasAttached.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasDetachedBecauseAddressWasRemoved>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Addresses.Should().BeEmpty();
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                    item.LastEventHash.Should().Be(@event.GetHash());
                });
        }

        [Fact]
        public async Task WhenBuildingUnitAddressWasReplacedBecauseOfMunicipalityMerger()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());
            _fixture.Customizations.Add(new WithUniqueInteger());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var buildingUnitAddressWasAttachedV2 = _fixture.Create<BuildingUnitAddressWasAttachedV2>();
            var @event = new BuildingUnitAddressWasReplacedBecauseOfMunicipalityMergerBuilder(_fixture)
                .WithPreviousAddressPersistentLocalId(buildingUnitAddressWasAttachedV2.AddressPersistentLocalId)
                .Build();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(buildingUnitWasPlannedV2, new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasAttachedV2>(
                        new Envelope(buildingUnitAddressWasAttachedV2, new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, buildingUnitAddressWasAttachedV2.GetHash()}})),
                    new Envelope<BuildingUnitAddressWasReplacedBecauseOfMunicipalityMerger>(
                        new Envelope(@event, new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Addresses.Should().ContainSingle();
                    item.Addresses.Single().AddressPersistentLocalId.Should().Be(@event.NewAddressPersistentLocalId);
                    item.Version.Should().Be(@event.Provenance.Timestamp);
                    item.LastEventHash.Should().Be(@event.GetHash());
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasRetiredBecauseBuildingWasDemolished()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var @event = _fixture.Create<BuildingUnitWasRetiredBecauseBuildingWasDemolished>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasRetiredBecauseBuildingWasDemolished>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.Retired);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasNotRealizedBecauseBuildingWasDemolished()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>();
            var @event = _fixture.Create<BuildingUnitWasNotRealizedBecauseBuildingWasDemolished>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(
                            buildingUnitWasPlannedV2,
                            new Dictionary<string, object>
                                {{AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash()}})),
                    new Envelope<BuildingUnitWasNotRealizedBecauseBuildingWasDemolished>(
                        new Envelope(
                            @event,
                            new Dictionary<string, object> {{AddEventHashPipe.HashMetadataKey, @event.GetHash()}})))
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(@event.BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();

                    item!.Status.Should().Be(BuildingUnitStatus.NotRealized);
                });
        }

        [Fact]
        public async Task WhenBuildingUnitWasMovedIntoBuilding()
        {
            _fixture.Customize(new WithFixedBuildingPersistentLocalId());
            _fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());

            var buildingUnitWasPlannedV2 = _fixture.Create<BuildingUnitWasPlannedV2>()
                .WithFunction(BuildingUnitFunction.Unknown);
            var buildingUnitWasMovedIntoBuilding = _fixture.Create<BuildingUnitWasMovedIntoBuilding>();

            await Sut
                .Given(
                    new Envelope<BuildingUnitWasPlannedV2>(
                        new Envelope(buildingUnitWasPlannedV2,
                            new Dictionary<string, object> { { AddEventHashPipe.HashMetadataKey, buildingUnitWasPlannedV2.GetHash() } })),
                    new Envelope<BuildingUnitWasMovedIntoBuilding>(
                        new Envelope(buildingUnitWasMovedIntoBuilding,
                            new Dictionary<string, object> { { AddEventHashPipe.HashMetadataKey, buildingUnitWasMovedIntoBuilding.GetHash() } }))
                )
                .Then(async ct =>
                {
                    var item = await ct.BuildingUnitDetailsV2WithCount.FindAsync(buildingUnitWasMovedIntoBuilding
                        .BuildingUnitPersistentLocalId);
                    item.Should().NotBeNull();
                    item!.BuildingPersistentLocalId.Should().Be(buildingUnitWasMovedIntoBuilding.BuildingPersistentLocalId);
                    item.Position.Should()
                        .BeEquivalentTo(buildingUnitWasMovedIntoBuilding.ExtendedWkbGeometry.ToByteArray());
                    item.PositionMethod.Should()
                        .Be(BuildingUnitPositionGeometryMethod.Parse(buildingUnitWasMovedIntoBuilding.GeometryMethod));
                    item.Function.Should().Be(BuildingUnitFunction.Parse(buildingUnitWasMovedIntoBuilding.Function));
                    item.Version.Should().Be(buildingUnitWasMovedIntoBuilding.Provenance.Timestamp);
                    item.IsRemoved.Should().BeFalse();
                    item.Status.Should().Be(BuildingUnitStatus.Parse(buildingUnitWasMovedIntoBuilding.BuildingUnitStatus));
                    item.HasDeviation.Should().Be(buildingUnitWasMovedIntoBuilding.HasDeviation);
                });
        }

        protected override BuildingUnitDetailV2Projections CreateProjection() => new BuildingUnitDetailV2Projections();
    }
}
