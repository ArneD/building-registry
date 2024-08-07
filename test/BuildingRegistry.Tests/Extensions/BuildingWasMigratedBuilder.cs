#pragma warning disable CS0618 // Type or member is obsolete
namespace BuildingRegistry.Tests.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using Building;
    using Building.Events;
    using BuildingRegistry.Legacy;
    using BuildingId = Building.BuildingId;
    using BuildingStatus = Building.BuildingStatus;
    using BuildingGeometry = BuildingRegistry.Legacy.BuildingGeometry;
    using ExtendedWkbGeometry = BuildingRegistry.Legacy.ExtendedWkbGeometry;
    using BuildingGeometryMethod = BuildingRegistry.Legacy.BuildingGeometryMethod;
    using BuildingUnit = Building.Commands.BuildingUnit;
    using BuildingUnitId = BuildingRegistry.Legacy.BuildingUnitId;
    using BuildingUnitPosition = BuildingRegistry.Legacy.BuildingUnitPosition;
    using BuildingUnitFunction = BuildingRegistry.Legacy.BuildingUnitFunction;
    using BuildingUnitPositionGeometryMethod = BuildingRegistry.Legacy.BuildingUnitPositionGeometryMethod;

    public class BuildingWasMigratedBuilder
    {
        private readonly Fixture _fixture;
        private BuildingPersistentLocalId? _buildingPersistentLocalId;
        private BuildingStatus? _buildingStatus;
        private bool _isBuildingRemoved;
        private readonly List<BuildingUnit> _buildingUnits = new();
        private BuildingRegistry.Building.BuildingGeometry? _buildingGeometry;

        public BuildingWasMigratedBuilder(Fixture fixture)
        {
            _fixture = fixture;
        }

        public BuildingWasMigratedBuilder WithBuildingPersistentLocalId(BuildingPersistentLocalId buildingPersistentLocalId)
        {
            _buildingPersistentLocalId = buildingPersistentLocalId;
            return this;
        }

        public BuildingWasMigratedBuilder WithBuildingStatus(string status)
        {
            _buildingStatus = BuildingStatus.Parse(status);
            return this;
        }

        public BuildingWasMigratedBuilder WithBuildingStatus(BuildingStatus status)
        {
            _buildingStatus = status;
            return this;
        }

        public BuildingWasMigratedBuilder WithIsRemoved()
        {
            _isBuildingRemoved = true;
            return this;
        }

        public BuildingWasMigratedBuilder WithBuildingGeometry(BuildingRegistry.Building.BuildingGeometry buildingGeometry)
        {
            if (_buildingUnits.Any())
            {
                throw new Exception("Can't add building geometry after one or more building units were added.");
            }

            _buildingGeometry = buildingGeometry;
            return this;
        }

        public BuildingWasMigratedBuilder WithBuildingUnit(
            BuildingRegistry.Legacy.BuildingUnitStatus status,
            BuildingUnitPersistentLocalId? buildingUnitPersistentLocalId = null,
            BuildingUnitFunction? function = null,
            BuildingUnitPositionGeometryMethod? positionGeometryMethod = null,
            ExtendedWkbGeometry? extendedWkbGeometry = null,
            List<AddressPersistentLocalId>? attachedAddresses = null,
            bool isRemoved = false)
        {
            var buildingUnitFunction = function ?? BuildingUnitFunction.Unknown;

            var buildingGeometry = _buildingGeometry is not null
                ? new BuildingGeometry(
                    new ExtendedWkbGeometry(_buildingGeometry.Geometry.ToString()),
                    _buildingGeometry.Method == BuildingRegistry.Building.BuildingGeometryMethod.Outlined
                        ? BuildingGeometryMethod.Outlined
                        : BuildingGeometryMethod.MeasuredByGrb)
                : _fixture.Create<BuildingGeometry>();

            var buildingUnitPosition =
                DetermineBuildingUnitPosition(buildingUnitFunction, positionGeometryMethod, extendedWkbGeometry, buildingGeometry);

            _buildingUnits.Add(
                new BuildingUnit(
                    _fixture.Create<BuildingUnitId>(),
                    buildingUnitPersistentLocalId is not null
                        ? new PersistentLocalId(buildingUnitPersistentLocalId)
                        : new PersistentLocalId(_fixture.Create<int>()),
                    buildingUnitFunction,
                    status,
                    attachedAddresses ?? new List<AddressPersistentLocalId>(),
                    buildingUnitPosition,
                    buildingGeometry,
                    isRemoved));

            return this;
        }

        private BuildingUnitPosition DetermineBuildingUnitPosition(
            BuildingUnitFunction function,
            BuildingUnitPositionGeometryMethod? positionGeometryMethod,
            ExtendedWkbGeometry? extendedWkbGeometry,
            BuildingGeometry buildingGeometry)
        {
            if (function == BuildingUnitFunction.Common)
            {
                return new BuildingUnitPosition(
                    extendedWkbGeometry ?? buildingGeometry.Center, BuildingUnitPositionGeometryMethod.DerivedFromObject);
            }

            if (positionGeometryMethod == BuildingUnitPositionGeometryMethod.DerivedFromObject)
            {
                return new BuildingUnitPosition(buildingGeometry.Center, BuildingUnitPositionGeometryMethod.DerivedFromObject);
            }

            if (positionGeometryMethod == BuildingUnitPositionGeometryMethod.AppointedByAdministrator)
            {
                return new BuildingUnitPosition(
                    extendedWkbGeometry ?? buildingGeometry.Center, BuildingUnitPositionGeometryMethod.AppointedByAdministrator);
            }

            if (extendedWkbGeometry is not null)
            {
                return new BuildingUnitPosition(
                    extendedWkbGeometry, BuildingUnitPositionGeometryMethod.AppointedByAdministrator);
            }

            return _fixture.Create<BuildingUnitPosition>();
        }

        public BuildingWasMigratedBuilder WithBuildingUnit(BuildingUnit buildingUnit)
        {
            _buildingUnits.Add(buildingUnit);

            return this;
        }

        public BuildingWasMigrated Build()
        {
            if (_buildingUnits
                .GroupBy(x => x.BuildingUnitPersistentLocalId)
                .Any(x => x.Count() > 1))
            {
                throw new Exception(
                    "Test setup contains multiple building units with the same BuildingUnitPersistentLocalId.");
            }

            var buildingWasMigrated = new BuildingWasMigrated(
                _fixture.Create<BuildingId>(),
                _buildingPersistentLocalId ?? _fixture.Create<BuildingPersistentLocalId>(),
                _fixture.Create<BuildingPersistentLocalIdAssignmentDate>(),
                _buildingStatus ?? BuildingStatus.Planned,
                _buildingGeometry ?? _fixture.Create<BuildingRegistry.Building.BuildingGeometry>(),
                _isBuildingRemoved,
                _buildingUnits);
            ((ISetProvenance)buildingWasMigrated).SetProvenance(_fixture.Create<Provenance>());

            return buildingWasMigrated;
        }
    }
}
