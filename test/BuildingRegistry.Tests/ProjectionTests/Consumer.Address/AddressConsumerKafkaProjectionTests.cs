namespace BuildingRegistry.Tests.ProjectionTests.Consumer.Address
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AutoFixture;
    using Be.Vlaanderen.Basisregisters.GrAr.Contracts.AddressRegistry;
    using Be.Vlaanderen.Basisregisters.GrAr.Contracts.Common;
    using Building;
    using BuildingRegistry.Consumer.Address;
    using BuildingRegistry.Consumer.Address.Projections;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Tests.Legacy.Autofixture;
    using Xunit;
    using Xunit.Abstractions;

    public class AddressConsumerKafkaProjectionTests : KafkaProjectionTest<ConsumerAddressContext, AddressKafkaProjection>
    {
        public AddressConsumerKafkaProjectionTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            Fixture.Customize(new InfrastructureCustomization());
        }

        [Fact]
        public async Task AddressMigratedToStreetName_AddsAddress()
        {
            var addressStatus = GetRandomAddressStatus();

            var addressWasMigratedToStreetName = Fixture
                .Build<AddressWasMigratedToStreetName>()
                .FromFactory(() => new AddressWasMigratedToStreetName(
                    Fixture.Create<int>(),
                    Fixture.Create<Guid>().ToString("D"),
                    Fixture.Create<Guid>().ToString("D"),
                    Fixture.Create<int>(),
                    addressStatus.Status,
                    Fixture.Create<string>(),
                    Fixture.Create<string>(),
                    Fixture.Create<string>(),
                    Fixture.Create<string>(),
                    Fixture.Create<string>(),
                    Fixture.Create<bool>(),
                    Fixture.Create<string>(),
                    Fixture.Create<bool>(),
                    Fixture.Create<bool>(),
                    Fixture.Create<int?>(),
                    Fixture.Create<Provenance>()
                ))
                .Create();

            Given(addressWasMigratedToStreetName);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasMigratedToStreetName.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.AddressId.Should().Be(Guid.Parse(addressWasMigratedToStreetName.AddressId));
                address.IsRemoved.Should().Be(address.IsRemoved);
                address.Status.Should().Be(addressStatus);
            });
        }

        [Fact]
        public async Task AddressWasProposedV2_AddsAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            Given(addressWasProposedV2);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.AddressId.Should().BeNull();
                address.IsRemoved.Should().Be(false);
                address.Status.Should().Be(AddressStatus.Proposed);
            });
        }

        [Fact]
        public async Task AddressWasProposedForMunicipalityMerger_AddsAddress()
        {
            var addressWasProposed = Fixture.Create<AddressWasProposedForMunicipalityMerger>();
            Given(addressWasProposed);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposed.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.AddressId.Should().BeNull();
                address.IsRemoved.Should().Be(false);
                address.Status.Should().Be(AddressStatus.Proposed);
            });
        }

        [Fact]
        public async Task AddressWasApproved_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Current);
            });
        }

        [Fact]
        public async Task AddressWasCorrectedFromApprovedToProposed_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasCorrectedFromApprovedToProposed = Fixture.Build<AddressWasCorrectedFromApprovedToProposed>()
                .FromFactory(() => new AddressWasCorrectedFromApprovedToProposed(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasCorrectedFromApprovedToProposed);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Proposed);
            });
        }

        [Fact]
        public async Task AddressWasCorrectedFromApprovedToProposedBecauseHouseNumberWasCorrected_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasCorrectedFromApprovedToProposed = Fixture.Build<AddressWasCorrectedFromApprovedToProposedBecauseHouseNumberWasCorrected>()
                .FromFactory(() => new AddressWasCorrectedFromApprovedToProposedBecauseHouseNumberWasCorrected(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasCorrectedFromApprovedToProposed);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Proposed);
            });
        }

        [Fact]
        public async Task AddressWasDeregulated_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasDeregulated = Fixture.Build<AddressWasDeregulated>()
                .FromFactory(() => new AddressWasDeregulated(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasDeregulated);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Current);
            });
        }

        [Fact]
        public async Task AddressWasRejected_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRejected = Fixture.Build<AddressWasRejected>()
                .FromFactory(() => new AddressWasRejected(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRejected);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Rejected);
            });
        }

        [Fact]
        public async Task AddressWasRejectedBecauseHouseNumberWasRejected_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRejected = Fixture.Build<AddressWasRejectedBecauseHouseNumberWasRejected>()
                .FromFactory(() => new AddressWasRejectedBecauseHouseNumberWasRejected(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRejected);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Rejected);
            });
        }

        [Fact]
        public async Task AddressWasRejectedBecauseHouseNumberWasRetired_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRejected = Fixture.Build<AddressWasRejectedBecauseHouseNumberWasRetired>()
                .FromFactory(() => new AddressWasRejectedBecauseHouseNumberWasRetired(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRejected);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Rejected);
            });
        }

        [Fact]
        public async Task AddressWasRejectedBecauseStreetNameWasRejected_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRejected = Fixture.Build<AddressWasRejectedBecauseStreetNameWasRejected>()
                .FromFactory(() => new AddressWasRejectedBecauseStreetNameWasRejected(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRejected);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Rejected);
            });
        }

        [Fact]
        public async Task AddressWasRejectedBecauseStreetNameWasRetired_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRejected = Fixture.Build<AddressWasRejectedBecauseStreetNameWasRetired>()
                .FromFactory(() => new AddressWasRejectedBecauseStreetNameWasRetired(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRejected);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Rejected);
            });
        }

        [Fact]
        public async Task AddressWasRejectedBecauseOfMunicipalityMerger_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRejected = Fixture.Build<AddressWasRejectedBecauseOfMunicipalityMerger>()
                .FromFactory(() => new AddressWasRejectedBecauseOfMunicipalityMerger(
                    addressWasProposedV2.StreetNamePersistentLocalId,
                    addressWasProposedV2.AddressPersistentLocalId,
                    Fixture.Create<AddressPersistentLocalId>(),
                    Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRejected);

            await Then(async context =>
            {
                var address = await context.AddressConsumerItems.FindAsync(addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Rejected);
            });
        }

        [Fact]
        public async Task AddressWasCorrectedFromRejectedToProposed_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRejected = Fixture.Build<AddressWasRejected>()
                .FromFactory(() => new AddressWasRejected(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasCorrectedFromRejectedToProposed = Fixture.Build<AddressWasCorrectedFromRejectedToProposed>()
                .FromFactory(() => new AddressWasCorrectedFromRejectedToProposed(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRejected, addressWasCorrectedFromRejectedToProposed);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Proposed);
            });
        }

        [Fact]
        public async Task AddressWasRetiredV2_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasRetiredV2 = Fixture.Build<AddressWasRetiredV2>()
                .FromFactory(() => new AddressWasRetiredV2(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasRetiredV2);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Retired);
            });
        }

        [Fact]
        public async Task AddressWasRetiredBecauseHouseNumberWasRetired_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasRetired = Fixture.Build<AddressWasRetiredBecauseHouseNumberWasRetired>()
                .FromFactory(() => new AddressWasRetiredBecauseHouseNumberWasRetired(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasRetired);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Retired);
            });
        }

        [Fact]
        public async Task AddressWasRetiredBecauseStreetNameWasRejected_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasRetired = Fixture.Build<AddressWasRetiredBecauseStreetNameWasRejected>()
                .FromFactory(() => new AddressWasRetiredBecauseStreetNameWasRejected(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasRetired);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Retired);
            });
        }

        [Fact]
        public async Task AddressWasRetiredBecauseStreetNameWasRetired_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasRetired = Fixture.Build<AddressWasRetiredBecauseStreetNameWasRetired>()
                .FromFactory(() => new AddressWasRetiredBecauseStreetNameWasRetired(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasRetired);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Retired);
            });
        }

        [Fact]
        public async Task AddressWasRetiredBecauseOfMunicipalityMerger_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasRetired = Fixture.Build<AddressWasRetiredBecauseOfMunicipalityMerger>()
                .FromFactory(() => new AddressWasRetiredBecauseOfMunicipalityMerger(
                    addressWasProposedV2.StreetNamePersistentLocalId,
                    addressWasProposedV2.AddressPersistentLocalId,
                    Fixture.Create<AddressPersistentLocalId>(),
                    Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasRetired);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Retired);
            });
        }

        [Fact]
        public async Task AddressWasCorrectedFromRetiredToCurrent_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasRetiredV2 = Fixture.Build<AddressWasRetiredV2>()
                .FromFactory(() => new AddressWasRetiredV2(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasCorrectedFromRetiredToCurrent = Fixture.Build<AddressWasCorrectedFromRetiredToCurrent>()
                .FromFactory(() => new AddressWasCorrectedFromRetiredToCurrent(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasRetiredV2, addressWasCorrectedFromRetiredToCurrent);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Current);
            });
        }

        [Fact]
        public async Task AddressWasRemovedV2_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRemovedV2 = Fixture.Build<AddressWasRemovedV2>()
                .FromFactory(() => new AddressWasRemovedV2(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRemovedV2);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Proposed);
                address.IsRemoved.Should().BeTrue();
            });
        }

        [Fact]
        public async Task AddressWasRemovedBecauseStreetNameWasRemoved()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRemovedV2 = Fixture.Build<AddressWasRemovedBecauseStreetNameWasRemoved>()
                .FromFactory(() => new AddressWasRemovedBecauseStreetNameWasRemoved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRemovedV2);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.IsRemoved.Should().BeTrue();
            });
        }

        [Fact]
        public async Task AddressRemovalWasCorrected_UpdatesStatusAddressAndIsRemoved()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRemovedV2 = Fixture.Build<AddressWasRemovedV2>()
                .FromFactory(() => new AddressWasRemovedV2(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressRemovalWasCorrected = Fixture.Build<AddressRemovalWasCorrected>()
                .FromFactory(() => new AddressRemovalWasCorrected(
                    addressWasProposedV2.StreetNamePersistentLocalId,
                    addressWasProposedV2.AddressPersistentLocalId,
                    GetRandomAddressStatus(),
                    Fixture.Create<string>(),
                    Fixture.Create<string>(),
                    Fixture.Create<string>(),
                    Fixture.Create<string>(),
                    Fixture.Create<ExtendedWkbGeometry>().ToString(),
                    Fixture.Create<bool>(),
                    Fixture.Create<string>(),
                    Fixture.Create<int?>(),
                    Fixture.Create<Provenance>()
                ))
                .Create();

            Given(addressWasProposedV2, addressWasRemovedV2, addressRemovalWasCorrected);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Parse(addressRemovalWasCorrected.Status));
                address.IsRemoved.Should().BeFalse();
            });
        }

        [Fact]
        public async Task AddressWasRemovedBecauseHouseNumberWasRemoved_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRemovedV2 = Fixture.Build<AddressWasRemovedBecauseHouseNumberWasRemoved>()
                .FromFactory(() => new AddressWasRemovedBecauseHouseNumberWasRemoved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRemovedV2);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Proposed);
                address.IsRemoved.Should().BeTrue();
            });
        }

        [Fact]
        public async Task AddressHouseNumberWasReaddressed_UpdatesStatusAddress()
        {
            var destinationHouseNumberAddressWasProposed = Fixture.Create<AddressWasProposedV2>();
            var destinationBoxNumberAddressWasProposed = Fixture.Create<AddressWasProposedV2>();

            var addressWasReaddressed = new AddressHouseNumberWasReaddressed(
                streetNamePersistentLocalId: 0,
                addressPersistentLocalId: destinationHouseNumberAddressWasProposed.AddressPersistentLocalId,
                readdressedHouseNumber: new ReaddressedAddressData(
                    1,
                    destinationHouseNumberAddressWasProposed.AddressPersistentLocalId,
                    false,
                    AddressStatus.Current,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false
                ),
                readdressedBoxNumbers: new List<ReaddressedAddressData>
                {
                    new ReaddressedAddressData(
                        1,
                        destinationBoxNumberAddressWasProposed.AddressPersistentLocalId,
                        false,
                        AddressStatus.Current,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        false)
                },
                provenance: Fixture.Create<Provenance>());

            Given(destinationHouseNumberAddressWasProposed, destinationBoxNumberAddressWasProposed, addressWasReaddressed);

            await Then(async context =>
            {
                var houseNumberAddress = await context.AddressConsumerItems.FindAsync(destinationHouseNumberAddressWasProposed.AddressPersistentLocalId);
                houseNumberAddress.Should().NotBeNull();
                houseNumberAddress!.Status.Should().Be(AddressStatus.Current);

                var boxNumberAddress = await context.AddressConsumerItems.FindAsync(destinationBoxNumberAddressWasProposed.AddressPersistentLocalId);
                boxNumberAddress.Should().NotBeNull();
                boxNumberAddress!.Status.Should().Be(AddressStatus.Current);
            });
        }

        [Fact]
        public async Task AddressWasProposedBecauseOfReaddress_AddsAddress()
        {
            var addressWasProposed = Fixture.Create<AddressWasProposedBecauseOfReaddress>();
            Given(addressWasProposed);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposed.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.AddressId.Should().BeNull();
                address.IsRemoved.Should().Be(false);
                address.Status.Should().Be(AddressStatus.Proposed);
            });
        }

        [Fact]
        public async Task AddressWasRejectedBecauseOfReaddress_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasRejected = Fixture.Build<AddressWasRejectedBecauseOfReaddress>()
                .FromFactory(() => new AddressWasRejectedBecauseOfReaddress(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasRejected);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Rejected);
            });
        }

        [Fact]
        public async Task AddressWasRetiredBecauseOfReaddress_UpdatesStatusAddress()
        {
            var addressWasProposedV2 = Fixture.Create<AddressWasProposedV2>();
            var addressWasApproved = Fixture.Build<AddressWasApproved>()
                .FromFactory(() => new AddressWasApproved(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();
            var addressWasRetired = Fixture.Build<AddressWasRetiredBecauseOfReaddress>()
                .FromFactory(() => new AddressWasRetiredBecauseOfReaddress(
                    addressWasProposedV2.StreetNamePersistentLocalId, addressWasProposedV2.AddressPersistentLocalId, Fixture.Create<Provenance>()))
                .Create();

            Given(addressWasProposedV2, addressWasApproved, addressWasRetired);

            await Then(async context =>
            {
                var address =
                    await context.AddressConsumerItems.FindAsync(
                        addressWasProposedV2.AddressPersistentLocalId);

                address.Should().NotBeNull();
                address!.Status.Should().Be(AddressStatus.Retired);
            });
        }

        private AddressStatus GetRandomAddressStatus()
        {
            var addressStatus = Fixture
                .Build<AddressStatus>()
                .FromFactory(() =>
                {
                    var statuses = new List<AddressStatus>
                    {
                        AddressStatus.Current, AddressStatus.Proposed, AddressStatus.Rejected, AddressStatus.Retired
                    };

                    return statuses[new Random(Fixture.Create<int>()).Next(0, statuses.Count - 1)];
                })
                .Create();
            return addressStatus;
        }

        protected override ConsumerAddressContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ConsumerAddressContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ConsumerAddressContext(options);
        }

        protected override AddressKafkaProjection CreateProjection() => new AddressKafkaProjection();
    }
}
