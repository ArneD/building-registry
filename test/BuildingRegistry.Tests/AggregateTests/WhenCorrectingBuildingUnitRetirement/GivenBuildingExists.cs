namespace BuildingRegistry.Tests.AggregateTests.WhenCorrectingBuildingUnitRetirement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Be.Vlaanderen.Basisregisters.AggregateSource;
    using Be.Vlaanderen.Basisregisters.AggregateSource.Snapshotting;
    using Be.Vlaanderen.Basisregisters.AggregateSource.Testing;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using Building;
    using Building.Commands;
    using Building.Events;
    using Building.Exceptions;
    using Fixtures;
    using FluentAssertions;
    using Xunit;
    using Xunit.Abstractions;
    using BuildingGeometry = Building.BuildingGeometry;
    using BuildingId = Building.BuildingId;
    using BuildingStatus = Building.BuildingStatus;
    using BuildingUnit = Building.Commands.BuildingUnit;
    using BuildingUnitStatus = Building.BuildingUnitStatus;

    public class GivenBuildingExists : BuildingRegistryTest
    {
        public GivenBuildingExists(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            Fixture.Customize(new WithFixedBuildingId());
            Fixture.Customize(new WithFixedBuildingPersistentLocalId());
            Fixture.Customize(new WithFixedBuildingUnitPersistentLocalId());
        }

        [Fact]
        public void WithRetiredBuildingUnit_ThenBuildingUnitWasCorrected()
        {
            var command = Fixture.Create<CorrectBuildingUnitRetirement>();

            var buildingUnitWasPlanned = Fixture.Create<BuildingUnitWasPlannedV2>();
            ((ISetProvenance)buildingUnitWasPlanned).SetProvenance(Fixture.Create<Provenance>());

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    Fixture.Create<BuildingWasPlannedV2>(),
                    Fixture.Create<BuildingWasRealizedV2>(),
                    buildingUnitWasPlanned,
                    Fixture.Create<BuildingUnitWasRetiredV2>())
                .When(command)
                .Then(new Fact(new BuildingStreamId(command.BuildingPersistentLocalId),
                    new BuildingUnitWasCorrectedFromRetiredToRealized(
                        command.BuildingPersistentLocalId,
                        command.BuildingUnitPersistentLocalId,
                        new ExtendedWkbGeometry(buildingUnitWasPlanned.ExtendedWkbGeometry)))));
        }

        [Fact]
        public void WithRealizedBuildingUnit_ThenDoNothing()
        {
            var command = Fixture.Create<CorrectBuildingUnitRetirement>();

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    Fixture.Create<BuildingWasPlannedV2>(),
                    Fixture.Create<BuildingWasRealizedV2>(),
                    Fixture.Create<BuildingUnitWasPlannedV2>(),
                    Fixture.Create<BuildingUnitWasRealizedV2>())
                .When(command)
                .ThenNone());
        }

        [Theory]
        [InlineData("Planned")]
        [InlineData("NotRealized")]
        public void WithInvalidBuildingUnitStatus_ThrowsBuildingUnitHasInvalidStatusException(string status)
        {
            var command = Fixture.Create<CorrectBuildingUnitRetirement>();

            var buildingWasMigrated = new BuildingWasMigrated(
                Fixture.Create<BuildingId>(),
                command.BuildingPersistentLocalId,
                Fixture.Create<BuildingPersistentLocalIdAssignmentDate>(),
                BuildingStatus.Realized,
                Fixture.Create<BuildingGeometry>(),
                isRemoved: false,
                new List<BuildingUnit>
                {
                    new BuildingUnit(
                        Fixture.Create<BuildingRegistry.Legacy.BuildingUnitId>(),
                        Fixture.Create<BuildingRegistry.Legacy.PersistentLocalId>(),
                        BuildingRegistry.Legacy.BuildingUnitFunction.Unknown,
                        BuildingRegistry.Legacy.BuildingUnitStatus.Parse(status) ?? throw new ArgumentException(),
                        new List<AddressPersistentLocalId>(),
                        Fixture.Create<BuildingRegistry.Legacy.BuildingUnitPosition>(),
                        Fixture.Create<BuildingRegistry.Legacy.BuildingGeometry>(),
                        isRemoved: false)
                }
            );
            ((ISetProvenance)buildingWasMigrated).SetProvenance(Fixture.Create<Provenance>());

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasMigrated)
                .When(command)
                .Throws(new BuildingUnitHasInvalidStatusException()));
        }

        [Fact]
        public void WithCommonBuilding_ThrowsBuildingUnitHasInvalidFunctionException()
        {
            var command = Fixture.Create<CorrectBuildingUnitRetirement>();

            var buildingWasMigrated = new BuildingWasMigrated(
                Fixture.Create<BuildingId>(),
                command.BuildingPersistentLocalId,
                Fixture.Create<BuildingPersistentLocalIdAssignmentDate>(),
                BuildingStatus.Realized,
                Fixture.Create<BuildingGeometry>(),
                isRemoved: false,
                new List<BuildingUnit>
                {
                    new BuildingUnit(
                        Fixture.Create<BuildingRegistry.Legacy.BuildingUnitId>(),
                        Fixture.Create<BuildingRegistry.Legacy.PersistentLocalId>(),
                        BuildingRegistry.Legacy.BuildingUnitFunction.Common,
                        BuildingRegistry.Legacy.BuildingUnitStatus.Realized,
                        new List<AddressPersistentLocalId>(),
                        Fixture.Create<BuildingRegistry.Legacy.BuildingUnitPosition>(),
                        Fixture.Create<BuildingRegistry.Legacy.BuildingGeometry>(),
                        isRemoved: false)
                }
            );
            ((ISetProvenance)buildingWasMigrated).SetProvenance(Fixture.Create<Provenance>());

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasMigrated)
                .When(command)
                .Throws(new BuildingUnitHasInvalidFunctionException()));
        }

        [Fact]
        public void NonExistentBuildingUnit_ThrowsBuildingUnitNotFoundException()
        {
            var command = Fixture.Create<CorrectBuildingUnitRetirement>();

            var buildingWasMigrated = new BuildingWasMigrated(
                Fixture.Create<BuildingId>(),
                command.BuildingPersistentLocalId,
                Fixture.Create<BuildingPersistentLocalIdAssignmentDate>(),
                BuildingStatus.Realized,
                Fixture.Create<BuildingGeometry>(),
                isRemoved: false,
                new List<BuildingUnit>()
            );
            ((ISetProvenance)buildingWasMigrated).SetProvenance(Fixture.Create<Provenance>());

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasMigrated)
                .When(command)
                .Throws(new BuildingUnitIsNotFoundException(command.BuildingPersistentLocalId, command.BuildingUnitPersistentLocalId)));
        }

        [Fact]
        public void BuildingUnitIsRemoved_ThrowsBuildingUnitIsRemovedException()
        {
            var command = Fixture.Create<CorrectBuildingUnitRetirement>();

            var buildingWasMigrated = new BuildingWasMigrated(
                Fixture.Create<BuildingId>(),
                command.BuildingPersistentLocalId,
                Fixture.Create<BuildingPersistentLocalIdAssignmentDate>(),
                BuildingStatus.Realized,
                Fixture.Create<BuildingGeometry>(),
                isRemoved: false,
                new List<BuildingUnit>
                {
                        new BuildingUnit(
                            Fixture.Create<BuildingRegistry.Legacy.BuildingUnitId>(),
                            Fixture.Create<BuildingRegistry.Legacy.PersistentLocalId>(),
                            Fixture.Create<BuildingRegistry.Legacy.BuildingUnitFunction>(),
                            BuildingRegistry.Legacy.BuildingUnitStatus.Planned,
                            new List<AddressPersistentLocalId>(),
                            Fixture.Create<BuildingRegistry.Legacy.BuildingUnitPosition>(),
                            Fixture.Create<BuildingRegistry.Legacy.BuildingGeometry>(),
                            isRemoved: true)
                }
            );
            ((ISetProvenance)buildingWasMigrated).SetProvenance(Fixture.Create<Provenance>());

            Assert(new Scenario()
                .Given(
                    new BuildingStreamId(Fixture.Create<BuildingPersistentLocalId>()),
                    buildingWasMigrated)
                .When(command)
                .Throws(new BuildingUnitIsRemovedException(command.BuildingUnitPersistentLocalId)));
        }

        [Fact]
        public void StateCheck()
        {
            var building = new BuildingFactory(NoSnapshotStrategy.Instance).Create();

            var buildingWasPlanned = Fixture.Create<BuildingWasPlannedV2>();
            ((ISetProvenance)buildingWasPlanned).SetProvenance(Fixture.Create<Provenance>());

            var buildingUnitWasPlanned = Fixture.Create<BuildingUnitWasPlannedV2>();
            ((ISetProvenance)buildingUnitWasPlanned).SetProvenance(Fixture.Create<Provenance>());

            var buildingUnitWasRetired = Fixture.Create<BuildingUnitWasRetiredV2>();
            ((ISetProvenance)buildingUnitWasRetired).SetProvenance(Fixture.Create<Provenance>());

            var eventToTest = Fixture.Create<BuildingUnitWasCorrectedFromRetiredToRealized>();
            ((ISetProvenance)eventToTest).SetProvenance(Fixture.Create<Provenance>());

            // Act
            building.Initialize(new object[]
            {
                buildingWasPlanned,
                buildingUnitWasPlanned,
                buildingUnitWasRetired,
                eventToTest
            });

            // Assert
            building.BuildingUnits.Should().NotBeEmpty();
            building.BuildingUnits.Count.Should().Be(1);
            var buildingUnit = building.BuildingUnits.First();
            buildingUnit.Status.Should().Be(BuildingUnitStatus.Realized);
            buildingUnit.LastEventHash.Should().NotBe(building.LastEventHash);
        }
    }
}
