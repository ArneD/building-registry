namespace BuildingRegistry.Api.Oslo.Building.List
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Be.Vlaanderen.Basisregisters.Api.Search;
    using Be.Vlaanderen.Basisregisters.Api.Search.Filtering;
    using Be.Vlaanderen.Basisregisters.Api.Search.Pagination;
    using Be.Vlaanderen.Basisregisters.Api.Search.Sorting;
    using Be.Vlaanderen.Basisregisters.GrAr.Common;
    using BuildingRegistry.Api.Oslo.Building.Query;
    using BuildingRegistry.Api.Oslo.Converters;
    using BuildingRegistry.Api.Oslo.Infrastructure;
    using MediatR;
    using Microsoft.EntityFrameworkCore;

    public class BuildingListHandlerV2 : IRequestHandler<BuildingListRequest, BuildingListOsloResponse>
    {
        public async Task<BuildingListOsloResponse> Handle(BuildingListRequest request, CancellationToken cancellationToken)
        {
            var pagedBuildings = new BuildingListOsloQueryV2(request.Context)
                .Fetch(request.FilteringHeader, request.SortingHeader, request.PaginationRequest);

            request.HttpResponse.AddPagedQueryResultHeaders(pagedBuildings);

            var buildings = await pagedBuildings.Items
                .Select(a => new
                {
                    a.PersistentLocalId,
                    a.Version,
                    a.Status
                })
                .ToListAsync(cancellationToken);

            return new BuildingListOsloResponse
            {
                Gebouwen = buildings
                    .Select(x => new GebouwCollectieItemOslo(
                        x.PersistentLocalId,
                        request.ResponseOptions.Value.GebouwNaamruimte,
                        request.ResponseOptions.Value.GebouwDetailUrl,
                        x.Status.Map(),
                        x.Version.ToBelgianDateTimeOffset()))
                    .ToList(),
                Volgende = pagedBuildings.PaginationInfo.BuildNextUri(request.ResponseOptions.Value.GebouwVolgendeUrl),
                Context = request.ResponseOptions.Value.ContextUrlList
            };
        }
    }
}