namespace BuildingRegistry.Tests.BackOffice.Api.Building.WhenPlanningBuilding
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Be.Vlaanderen.Basisregisters.Sqs.Requests;
    using BuildingRegistry.Api.BackOffice.Abstractions.Building.Requests;
    using BuildingRegistry.Api.BackOffice.Abstractions.Building.SqsRequests;
    using BuildingRegistry.Api.BackOffice.Building;
    using BuildingRegistry.Tests.Fixtures;
    using FluentAssertions;
    using Microsoft.AspNetCore.Mvc;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class GivenSqsToggleEnabled : BackOfficeApiTest
    {
        private readonly BuildingController _controller;

        public GivenSqsToggleEnabled(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            Fixture.Customize(new WithFixedBuildingPersistentLocalId());

            _controller = CreateBuildingControllerWithUser<BuildingController>();
        }

        [Fact]
        public async Task ThenTicketLocationIsReturned()
        {
            var ticketId = Fixture.Create<Guid>();
            var expectedLocationResult = new LocationResult(CreateTicketUri(ticketId));

            MockMediator
                .Setup(x => x.Send(It.IsAny<PlanBuildingSqsRequest>(), CancellationToken.None))
                .Returns(Task.FromResult(expectedLocationResult));

            var result = (AcceptedResult)await _controller.Plan(
                MockValidRequestValidator<PlanBuildingRequest>(),
                Fixture.Create<PlanBuildingRequest>());

            result.Should().NotBeNull();
            AssertLocation(result.Location, ticketId);
        }
    }
}
