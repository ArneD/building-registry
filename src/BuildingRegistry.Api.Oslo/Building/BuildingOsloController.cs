namespace BuildingRegistry.Api.Oslo.Building
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Be.Vlaanderen.Basisregisters.Api;
    using Be.Vlaanderen.Basisregisters.Api.Exceptions;
    using Be.Vlaanderen.Basisregisters.Api.Search;
    using Be.Vlaanderen.Basisregisters.Api.Search.Filtering;
    using Be.Vlaanderen.Basisregisters.Api.Search.Pagination;
    using Be.Vlaanderen.Basisregisters.Api.Search.Sorting;
    using Be.Vlaanderen.Basisregisters.GrAr.Common;
    using Be.Vlaanderen.Basisregisters.GrAr.Legacy;
    using Be.Vlaanderen.Basisregisters.GrAr.Legacy.Gebouw;
    using Be.Vlaanderen.Basisregisters.GrAr.Legacy.SpatialTools;
    using Infrastructure;
    using Infrastructure.Grb;
    using Infrastructure.Options;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;
    using Projections.Legacy;
    using Projections.Syndication;
    using Query;
    using Responses;
    using Swashbuckle.AspNetCore.Filters;
    using ValueObjects;
    using ProblemDetails = Be.Vlaanderen.Basisregisters.BasicApiProblem.ProblemDetails;

    [ApiVersion("2.0")]
    [AdvertiseApiVersions("2.0")]
    [ApiRoute("gebouwen")]
    [ApiExplorerSettings(GroupName = "Gebouwen")]
    public class BuildingOsloController : ApiController
    {
        /// <summary>
        /// Vraag een gebouw op.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="syndicationContext"></param>
        /// <param name="responseOptions"></param>
        /// <param name="grbBuildingParcel"></param>
        /// <param name="persistentLocalId"></param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">Als het gebouw gevonden is.</response>
        /// <response code="404">Als het gebouw niet gevonden kan worden.</response>
        /// <response code="410">Als het gebouw verwijderd werd.</response>
        /// <response code="500">Als er een interne fout is opgetreden.</response>
        [HttpGet("{persistentLocalId}")]
        [Produces(AcceptTypes.JsonLd)]
        [ProducesResponseType(typeof(BuildingOsloResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(BuildingOsloResponseExamples))]
        [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(BuildingNotFoundResponseExamples))]
        [SwaggerResponseExample(StatusCodes.Status410Gone, typeof(BuildingGoneResponseExamples))]
        [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(InternalServerErrorResponseExamples))]
        public async Task<IActionResult> Get(
            [FromServices] LegacyContext context,
            [FromServices] SyndicationContext syndicationContext,
            [FromServices] IOptions<ResponseOptions> responseOptions,
            [FromServices] IGrbBuildingParcel grbBuildingParcel,
            [FromRoute] int persistentLocalId,
            CancellationToken cancellationToken = default)
        {
            var building = await context
                .BuildingDetails
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.PersistentLocalId == persistentLocalId, cancellationToken);

            if (building != null && building.IsRemoved)
                throw new ApiException("Gebouw werd verwijderd.", StatusCodes.Status410Gone);

            if (building == null || !building.IsComplete)
                throw new ApiException("Onbestaand gebouw.", StatusCodes.Status404NotFound);

            //TODO: improvement getting buildingunits and parcels in parallel.
            var buildingUnits = await context
                .BuildingUnitDetails
                .Where(x => x.BuildingId == building.BuildingId)
                .Where(x => x.IsComplete && !x.IsRemoved)
                .Select(x => x.PersistentLocalId)
                .ToListAsync(cancellationToken);

            var parcels = grbBuildingParcel
                .GetUnderlyingParcels(building.Geometry)
                .Select(s => CaPaKey.CreateFrom(s).VbrCaPaKey)
                .Distinct();

            var caPaKeys = await syndicationContext
                .BuildingParcelLatestItems
                .Where(x => !x.IsRemoved &&
                            parcels.Contains(x.CaPaKey))
                .Select(x => x.CaPaKey)
                .ToListAsync(cancellationToken);

            var response = new BuildingOsloResponse(
                building.PersistentLocalId.Value,
                responseOptions.Value.GebouwNaamruimte,
                building.Version.ToBelgianDateTimeOffset(),
                GetBuildingPolygon(building.Geometry),
                MapGeometryMethod(building.GeometryMethod.Value),
                MapBuildingStatus(building.Status.Value),
                buildingUnits.OrderBy(x => x.Value).Select(x => new GebouwDetailGebouweenheid(x.ToString(), string.Format(responseOptions.Value.GebouweenheidDetailUrl, x))).ToList(),
                caPaKeys.Select(x => new GebouwDetailPerceel(x, string.Format(responseOptions.Value.PerceelUrl, x))).ToList());

            return Ok(response);
        }

        /// <summary>
        /// Vraag een lijst met actieve gebouwn op.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="responseOptions"></param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">Als de opvraging van een lijst met gebouwn gelukt is.</response>
        /// <response code="500">Als er een interne fout is opgetreden.</response>
        [HttpGet]
        [Produces(AcceptTypes.JsonLd)]
        [ProducesResponseType(typeof(BuildingListOsloResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(BuildingListResponseOsloExamples))]
        [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(InternalServerErrorResponseExamples))]
        public async Task<IActionResult> List(
            [FromServices] LegacyContext context,
            [FromServices] IOptions<ResponseOptions> responseOptions,
            CancellationToken cancellationToken = default)
        {
            var filtering = Request.ExtractFilteringRequest<BuildingFilter>();
            var sorting = Request.ExtractSortingRequest();
            var pagination = Request.ExtractPaginationRequest();

            var pagedBuildings = new BuildingListOsloQuery(context)
                .Fetch(filtering, sorting, pagination);

            Response.AddPagedQueryResultHeaders(pagedBuildings);

            var buildings = await pagedBuildings.Items
                .Select(a => new
                {
                    a.PersistentLocalId,
                    a.Version,
                    a.Status
                })
                .ToListAsync(cancellationToken);

            var listResponse = new BuildingListOsloResponse
            {
                Gebouwen = buildings
                    .Select(x => new GebouwCollectieItemOslo(
                        x.PersistentLocalId.Value,
                        responseOptions.Value.GebouwNaamruimte,
                        responseOptions.Value.GebouwDetailUrl,
                        MapBuildingStatus(x.Status.Value),
                        x.Version.ToBelgianDateTimeOffset()))
                    .ToList(),
                Volgende = pagedBuildings.PaginationInfo.BuildNextUri(responseOptions.Value.GebouwVolgendeUrl)
            };

            return Ok(listResponse);
        }

        /// <summary>
        /// Vraag het totaal aantal actieve gebouwen op.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">Als de opvraging van het totaal aantal gelukt is.</response>
        /// <response code="500">Als er een interne fout is opgetreden.</response>
        [HttpGet("totaal-aantal")]
        [Produces(AcceptTypes.JsonLd)]
        [ProducesResponseType(typeof(TotaalAantalResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(TotalCountOsloResponseExample))]
        [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(InternalServerErrorResponseExamples))]
        public async Task<IActionResult> Count(
            [FromServices] LegacyContext context,
            CancellationToken cancellationToken = default)
        {
            var filtering = Request.ExtractFilteringRequest<BuildingFilter>();
            var sorting = Request.ExtractSortingRequest();
            var pagination = new NoPaginationRequest();

            return Ok(
                new TotaalAantalResponse
                {
                    Aantal = filtering.ShouldFilter
                        ? await new BuildingListOsloQuery(context)
                            .Fetch(filtering, sorting, pagination)
                            .Items
                            .CountAsync(cancellationToken)
                        : Convert.ToInt32(context
                            .BuildingDetailListCountView
                            .First()
                            .Count)
                });
        }

        internal static Polygon GetBuildingPolygon(byte[] polygon)
        {
            var geometry = WKBReaderFactory.Create().Read(polygon) as NetTopologySuite.Geometries.Polygon;

            if (geometry == null) //some buildings have multi polygons (imported) which are incorrect.
                return null;

            return new Polygon
            {
                XmlPolygon = MapGmlPolygon(geometry),
                JsonPolygon = MapToGeoJsonPolygon(geometry)
            };
        }

        private static GeoJSONPolygon MapToGeoJsonPolygon(NetTopologySuite.Geometries.Polygon polygon)
        {
            var rings = polygon.InteriorRings.ToList();
            rings.Insert(0, polygon.ExteriorRing); //insert exterior ring as first item

            var output = new double[rings.Count][][];
            for (var i = 0; i < rings.Count; i++)
            {
                output[i] = new double[rings[i].Coordinates.Length][];

                for (int j = 0; j < rings[i].Coordinates.Length; j++)
                {
                    output[i][j] = new double[2];
                    output[i][j][0] = rings[i].Coordinates[j].X;
                    output[i][j][1] = rings[i].Coordinates[j].Y;
                }
            }

            return new GeoJSONPolygon { Coordinates = output };
        }

        private static GmlPolygon MapGmlPolygon(NetTopologySuite.Geometries.Polygon polygon)
        {
            var gmlPolygon = new GmlPolygon
            {
                Exterior = GetGmlRing(polygon.ExteriorRing as NetTopologySuite.Geometries.LinearRing)
            };

            if (polygon.NumInteriorRings > 0)
                gmlPolygon.Interior = new List<RingProperty>();

            for (var i = 0; i < polygon.NumInteriorRings; i++)
                gmlPolygon.Interior.Add(GetGmlRing(polygon.InteriorRings[i] as NetTopologySuite.Geometries.LinearRing));

            return gmlPolygon;
        }

        private static RingProperty GetGmlRing(NetTopologySuite.Geometries.LinearRing ring)
        {
            var posListBuilder = new StringBuilder();
            foreach (var coordinate in ring.Coordinates)
                posListBuilder.Append($"{coordinate.X.ToPolygonGeometryCoordinateValueFormat()} {coordinate.Y.ToPolygonGeometryCoordinateValueFormat()} ");

            //remove last space
            posListBuilder.Length--;

            var gmlRing = new RingProperty { LinearRing = new LinearRing { PosList = posListBuilder.ToString() } };
            return gmlRing;
        }

        private static GeometrieMethode MapGeometryMethod(BuildingGeometryMethod geometryMethod)
        {
            switch (geometryMethod)
            {
                case BuildingGeometryMethod.Outlined:
                    return GeometrieMethode.Ingeschetst;

                case BuildingGeometryMethod.MeasuredByGrb:
                    return GeometrieMethode.IngemetenGRB;

                default:
                    throw new ArgumentOutOfRangeException(nameof(geometryMethod), geometryMethod, null);
            }
        }

        private static GebouwStatus MapBuildingStatus(BuildingStatus status)
        {
            switch (status)
            {
                case BuildingStatus.Planned:
                    return GebouwStatus.Gepland;

                case BuildingStatus.UnderConstruction:
                    return GebouwStatus.InAanbouw;

                case BuildingStatus.Realized:
                    return GebouwStatus.Gerealiseerd;

                case BuildingStatus.Retired:
                    return GebouwStatus.Gehistoreerd;

                case BuildingStatus.NotRealized:
                    return GebouwStatus.NietGerealiseerd;

                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}