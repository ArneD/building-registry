namespace BuildingRegistry.Api.BackOffice.Building
{
    using System.Threading;
    using System.Threading.Tasks;
    using Abstractions.Building.Requests;
    using Abstractions.Building.SqsRequests;
    using Be.Vlaanderen.Basisregisters.AcmIdm;
    using Be.Vlaanderen.Basisregisters.Api.Exceptions;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using FluentValidation;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Swashbuckle.AspNetCore.Filters;

    public partial class BuildingController
    {
        /// <summary>
        /// Plan een gebouw met schets in.
        /// </summary>
        /// <param name="planBuildingRequest"></param>
        /// <param name="validator"></param>
        /// <param name="cancellationToken"></param>
        /// <response code="202">Als het gebouw (reeds) ingepland is.</response>
        /// <returns></returns>
        [HttpPost("acties/plannen")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [SwaggerResponseHeader(StatusCodes.Status202Accepted, "location", "string", "De url van het geplande gebouw.")]
        [SwaggerRequestExample(typeof(PlanBuildingRequest), typeof(PlanBuildingRequestExamples))]
        [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestResponseExamples))]
        [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(InternalServerErrorResponseExamples))]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Policy = PolicyNames.GeschetstGebouw.DecentraleBijwerker)]
        public async Task<IActionResult> Plan(
            [FromServices] IValidator<PlanBuildingRequest> validator,
            [FromBody] PlanBuildingRequest planBuildingRequest,
            CancellationToken cancellationToken = default)
        {
            await validator.ValidateAndThrowAsync(planBuildingRequest, cancellationToken);

            var result = await Mediator.Send(
                new PlanBuildingSqsRequest
                {
                    Request = planBuildingRequest,
                    Metadata = GetMetadata(),
                    ProvenanceData = new ProvenanceData(CreateFakeProvenance()),
                }, cancellationToken);

            return Accepted(result);
        }
    }
}
