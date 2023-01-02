namespace BuildingRegistry.Tests.BackOffice.Api.WhenAttachingAddressToBuildingUnit
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Be.Vlaanderen.Basisregisters.Api.ETag;
    using Be.Vlaanderen.Basisregisters.Api.Exceptions;
    using Be.Vlaanderen.Basisregisters.Sqs.Exceptions;
    using Be.Vlaanderen.Basisregisters.Sqs.Responses;
    using Building;
    using Building.Datastructures;
    using BuildingRegistry.Api.BackOffice.Abstractions.BuildingUnit.Extensions;
    using BuildingRegistry.Api.BackOffice.Abstractions.BuildingUnit.Requests;
    using BuildingRegistry.Api.BackOffice.Abstractions.BuildingUnit.Validators;
    using BuildingRegistry.Api.BackOffice.Building;
    using BuildingRegistry.Api.BackOffice.BuildingUnit;
    using Fixtures;
    using FluentAssertions;
    using Handlers;
    using Microsoft.AspNetCore.Http;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class GivenAttachAddressToBuildingUnit : BackOfficeApiTest
    {
        private readonly BuildingUnitController _controller;

        public GivenAttachAddressToBuildingUnit(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            Fixture.Customize(new WithFixedBuildingPersistentLocalId());

            _controller = CreateBuildingUnitControllerWithUser<BuildingUnitController>(useSqs: false);
        }

        [Fact]
        public async Task ThenAcceptedResponseIsExpected()
        {
            // Arrange
            MockMediator
                .Setup(x => x.Send(It.IsAny<AttachAddressToBuildingUnitRequest>(), CancellationToken.None).Result)
                .Returns(new ETagResponse(string.Empty, string.Empty));

            var buildingUnitPersistentLocalId = Fixture.Create<BuildingUnitPersistentLocalId>();
            var address = new AddressData(Fixture.Create<AddressPersistentLocalId>(), AddressStatus.Current, false);
            var addresses = new Mock<IAddresses>();
            addresses.Setup(x => x.GetOptional(address.AddressPersistentLocalId)).Returns(address);

            //Act
            var result = (AcceptedWithETagResult)await _controller.AttachAddress(
                ResponseOptions,
                MockIfMatchValidator(true),
                new AttachAddressToBuildingUnitRequestValidator(addresses.Object),
                buildingUnitPersistentLocalId,
                new AttachAddressToBuildingUnitRequest() { AdresId = PuriCreator.CreateAdresId(address.AddressPersistentLocalId) },
                string.Empty,
                CancellationToken.None);

            //Assert
            MockMediator.Verify(x => x.Send(It.IsAny<AttachAddressToBuildingUnitRequest>(), CancellationToken.None), Times.Once);

            result.StatusCode.Should().Be(202);
            result.Location.Should().Be(string.Format(BuildingUnitDetailUrl, buildingUnitPersistentLocalId));
        }

        [Fact]
        public async Task WithInvalidIfMatchHeader_ThenPreconditionFailedResponse()
        {
            //Act
            var result = await _controller.AttachAddress(
                ResponseOptions,
                MockIfMatchValidator(false),
                MockValidRequestValidator<AttachAddressToBuildingUnitRequest>(),
                Fixture.Create<BuildingUnitPersistentLocalId>(),
                Fixture.Create<AttachAddressToBuildingUnitRequest>(),
                "IncorrectIfMatchHeader");

            //Assert
            result.Should().BeOfType<PreconditionFailedResult>();
        }

        [Fact]
        public void WithAggregateIdIsNotFound_ThenThrowsApiException()
        {
            MockMediator
                .Setup(x => x.Send(It.IsAny<AttachAddressToBuildingUnitRequest>(), CancellationToken.None))
                .Throws(new AggregateIdIsNotFoundException());

            var request = Fixture.Create<AttachAddressToBuildingUnitRequest>();
            Func<Task> act = async () =>
            {
                await _controller.AttachAddress(
                    ResponseOptions,
                    MockIfMatchValidator(true),
                    MockValidRequestValidator<AttachAddressToBuildingUnitRequest>(),
                    Fixture.Create<BuildingUnitPersistentLocalId>(),
                    request,
                    string.Empty);
            };

            //Assert
            act
                .Should()
                .ThrowAsync<ApiException>()
                .Result
                .Where(x =>
                    x.Message.Contains("Onbestaande gebouweenheid.")
                    && x.StatusCode == StatusCodes.Status404NotFound);
        }
    }
}