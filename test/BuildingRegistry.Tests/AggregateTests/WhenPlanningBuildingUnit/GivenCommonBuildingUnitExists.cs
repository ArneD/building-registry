namespace BuildingRegistry.Tests.AggregateTests.WhenPlanningBuildingUnit
{
    using AutoFixture;
    using Be.Vlaanderen.Basisregisters.AggregateSource;
    using Be.Vlaanderen.Basisregisters.AggregateSource.Testing;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using Building;
    using Building.Commands;
    using Building.Events;
    using Extensions;
    using Fixtures;
    using Xunit;
    using Xunit.Abstractions;
    using BuildingGeometry = Building.BuildingGeometry;
    using BuildingGeometryMethod = Building.BuildingGeometryMethod;
    using BuildingStatus = Building.BuildingStatus;
    using BuildingUnitPositionGeometryMethod = Building.BuildingUnitPositionGeometryMethod;
    using BuildingUnitStatus = Building.BuildingUnitStatus;
    using ExtendedWkbGeometry = Building.ExtendedWkbGeometry;

    public class GivenCommonBuildingUnitExists : BuildingRegistryTest
    {
        public GivenCommonBuildingUnitExists(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            Fixture.Customize(new WithFixedBuildingPersistentLocalId());
        }

        [Fact]
        public void WithActiveBuildingUnit_ThenCommonBuildingUnitWasNotAdded()
        {
            var command = Fixture.Create<PlanBuildingUnit>();

            var buildingWasPlanned = Fixture.Create<BuildingWasPlannedV2>();
            var buildingUnitWasPlanned = Fixture.Create<BuildingUnitWasPlannedV2>().WithBuildingUnitPersistentLocalId(new BuildingUnitPersistentLocalId(1234));
            var buildingUnitWasPlanned2 = Fixture.Create<BuildingUnitWasPlannedV2>().WithBuildingUnitPersistentLocalId(new BuildingUnitPersistentLocalId(5678));

            var buildingGeometry = new BuildingGeometry(
                new ExtendedWkbGeometry(buildingWasPlanned.ExtendedWkbGeometry),
                BuildingGeometryMethod.Outlined);

            var commonBuildingUnitWasAdded = new CommonBuildingUnitWasAddedV2(
                command.BuildingPersistentLocalId,
                new BuildingUnitPersistentLocalId(1),
                BuildingUnitStatus.Realized,
                BuildingUnitPositionGeometryMethod.DerivedFromObject,
                buildingGeometry.Center,
                false);
            ((ISetProvenance)commonBuildingUnitWasAdded).SetProvenance(Fixture.Create<Provenance>());

            Assert(new Scenario()
                .Given(new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasPlanned,
                    buildingUnitWasPlanned,
                    buildingUnitWasPlanned2,
                    commonBuildingUnitWasAdded)
                .When(command)
                .Then(new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                    new BuildingUnitWasPlannedV2(
                        command.BuildingPersistentLocalId,
                        command.BuildingUnitPersistentLocalId,
                        command.PositionGeometryMethod,
                        buildingGeometry.Center,
                        command.Function,
                        command.HasDeviation))));
        }

        [Theory]
        [InlineData("Planned")]
        [InlineData("UnderConstruction")]
        public void WithNotRealizedCommonBuildingUnit_ThenCommonBuildingUnitIsPlanned(string buildingStatus)
        {
            var command = Fixture.Create<PlanBuildingUnit>()
                .WithPersistentLocalId(new BuildingUnitPersistentLocalId(3))
                .WithoutPosition()
                .WithDeviation(false);

            var buildingGeometry = new BuildingGeometry(
                new ExtendedWkbGeometry(GeometryHelper.ValidPolygon.AsBinary()),
                BuildingGeometryMethod.Outlined);

            var buildingWasMigrated = new BuildingWasMigratedBuilder(Fixture)
                .WithBuildingPersistentLocalId(command.BuildingPersistentLocalId)
                .WithBuildingStatus(buildingStatus)
                .WithBuildingGeometry(buildingGeometry)
                .WithBuildingUnit(
                    BuildingRegistry.Legacy.BuildingUnitStatus.Planned,
                    new BuildingUnitPersistentLocalId(1))
                .WithBuildingUnit(
                    BuildingRegistry.Legacy.BuildingUnitStatus.NotRealized,
                    new BuildingUnitPersistentLocalId(2),
                    BuildingRegistry.Legacy.BuildingUnitFunction.Common)
                .Build();

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasMigrated)
                .When(command)
                .Then(
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasPlannedV2(
                            command.BuildingPersistentLocalId,
                            command.BuildingUnitPersistentLocalId,
                            command.PositionGeometryMethod,
                            buildingGeometry.Center,
                            command.Function,
                            false)),
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasCorrectedFromNotRealizedToPlanned(
                            command.BuildingPersistentLocalId,
                            new BuildingUnitPersistentLocalId(2)))));
        }

        [Fact]
        public void WithNotRealizedCommonBuildingUnitAndRealizedBuilding_ThenCommonBuildingUnitIsRealized()
        {
            var command = Fixture.Create<PlanBuildingUnit>()
                .WithPersistentLocalId(new BuildingUnitPersistentLocalId(2))
                .WithoutPosition()
                .WithDeviation(false);

            var buildingGeometry = new BuildingGeometry(
                new ExtendedWkbGeometry(GeometryHelper.ValidPolygon.AsBinary()),
                BuildingGeometryMethod.Outlined);

            var buildingWasMigrated = new BuildingWasMigratedBuilder(Fixture)
                .WithBuildingPersistentLocalId(command.BuildingPersistentLocalId)
                .WithBuildingStatus(BuildingStatus.Realized)
                .WithBuildingGeometry(buildingGeometry)
                .WithBuildingUnit(BuildingRegistry.Legacy.BuildingUnitStatus.Planned, new BuildingUnitPersistentLocalId(3))
                .WithBuildingUnit(
                    BuildingRegistry.Legacy.BuildingUnitStatus.NotRealized,
                    new BuildingUnitPersistentLocalId(1),
                    BuildingRegistry.Legacy.BuildingUnitFunction.Common)
                .Build();

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasMigrated)
                .When(command)
                .Then(
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasPlannedV2(
                            command.BuildingPersistentLocalId,
                            command.BuildingUnitPersistentLocalId,
                            command.PositionGeometryMethod,
                            buildingGeometry.Center,
                            command.Function,
                            false)),
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasCorrectedFromNotRealizedToPlanned(
                            command.BuildingPersistentLocalId,
                            new BuildingUnitPersistentLocalId(1))),
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasRealizedV2(
                            command.BuildingPersistentLocalId,
                            new BuildingUnitPersistentLocalId(1)))));
        }

        [Fact]
        public void WithRetiredCommonBuildingUnitAndRealizedBuilding_ThenCommonBuildingUnitIsRealized()
        {
            var command = Fixture.Create<PlanBuildingUnit>()
                .WithPersistentLocalId(new BuildingUnitPersistentLocalId(3))
                .WithoutPosition()
                .WithDeviation(false);

            var buildingGeometry = new BuildingGeometry(
                new ExtendedWkbGeometry(GeometryHelper.ValidPolygon.AsBinary()),
                BuildingGeometryMethod.Outlined);

            var retiredBuildingUnitPersistentLocalId = new BuildingUnitPersistentLocalId(2);
            var buildingWasMigrated = new BuildingWasMigratedBuilder(Fixture)
                .WithBuildingPersistentLocalId(command.BuildingPersistentLocalId)
                .WithBuildingStatus(BuildingStatus.Realized)
                .WithBuildingGeometry(buildingGeometry)
                .WithBuildingUnit(
                    BuildingRegistry.Legacy.BuildingUnitStatus.Planned,
                    new BuildingUnitPersistentLocalId(1),
                    positionGeometryMethod: BuildingRegistry.Legacy.BuildingUnitPositionGeometryMethod.AppointedByAdministrator,
                    extendedWkbGeometry: new BuildingRegistry.Legacy.ExtendedWkbGeometry(buildingGeometry.Center.ToString()))
                .WithBuildingUnit(
                    BuildingRegistry.Legacy.BuildingUnitStatus.Retired,
                    retiredBuildingUnitPersistentLocalId,
                    BuildingRegistry.Legacy.BuildingUnitFunction.Common)
                .Build();

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasMigrated)
                .When(command)
                .Then(
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasPlannedV2(
                            command.BuildingPersistentLocalId,
                            command.BuildingUnitPersistentLocalId,
                            command.PositionGeometryMethod,
                            buildingGeometry.Center,
                            command.Function,
                            false)),
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasCorrectedFromRetiredToRealized(
                            command.BuildingPersistentLocalId,
                            retiredBuildingUnitPersistentLocalId))));
        }

        [Fact]
        public void WithRetiredCommonBuildingUnitAndPlannedBuilding_ThenCommonBuildingUnitIsPlanned()
        {
            var command = Fixture.Create<PlanBuildingUnit>()
                .WithPersistentLocalId(new BuildingUnitPersistentLocalId(3))
                .WithoutPosition()
                .WithDeviation(false);

            var buildingGeometry = new BuildingGeometry(
                new ExtendedWkbGeometry(GeometryHelper.ValidPolygon.AsBinary()),
                BuildingGeometryMethod.Outlined);

            var retiredBuildingUnitPersistentLocalId = new BuildingUnitPersistentLocalId(2);
            var buildingWasMigrated = new BuildingWasMigratedBuilder(Fixture)
                .WithBuildingPersistentLocalId(command.BuildingPersistentLocalId)
                .WithBuildingStatus(BuildingStatus.Planned)
                .WithBuildingGeometry(buildingGeometry)
                .WithBuildingUnit(
                    BuildingRegistry.Legacy.BuildingUnitStatus.Planned,
                    new BuildingUnitPersistentLocalId(1),
                    positionGeometryMethod: BuildingRegistry.Legacy.BuildingUnitPositionGeometryMethod.AppointedByAdministrator,
                    extendedWkbGeometry: new BuildingRegistry.Legacy.ExtendedWkbGeometry(buildingGeometry.Center.ToString()))
                .WithBuildingUnit(
                    BuildingRegistry.Legacy.BuildingUnitStatus.Retired,
                    retiredBuildingUnitPersistentLocalId,
                    BuildingRegistry.Legacy.BuildingUnitFunction.Common)
                .Build();

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasMigrated)
                .When(command)
                .Then(
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasPlannedV2(
                            command.BuildingPersistentLocalId,
                            command.BuildingUnitPersistentLocalId,
                            command.PositionGeometryMethod,
                            buildingGeometry.Center,
                            command.Function,
                            false)),
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasCorrectedFromRetiredToRealized(
                            command.BuildingPersistentLocalId,
                            retiredBuildingUnitPersistentLocalId)),
                    new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                        new BuildingUnitWasCorrectedFromRealizedToPlanned(
                            command.BuildingPersistentLocalId,
                            retiredBuildingUnitPersistentLocalId))));
        }
    }
}
