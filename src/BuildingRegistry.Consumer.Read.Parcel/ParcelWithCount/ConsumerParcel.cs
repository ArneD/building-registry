namespace BuildingRegistry.Consumer.Read.Parcel.ParcelWithCount
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Be.Vlaanderen.Basisregisters.MessageHandling.Kafka.Consumer;
    using Be.Vlaanderen.Basisregisters.ProjectionHandling.Connector;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class ConsumerParcel : BackgroundService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly ILifetimeScope _lifetimeScope;
        private readonly IDbContextFactory<ConsumerParcelContext> _consumerParcelDbContextFactory;
        private readonly IConsumer _consumer;
        private readonly ILogger<ConsumerParcel> _logger;

        public ConsumerParcel(
            IHostApplicationLifetime hostApplicationLifetime,
            ILifetimeScope lifetimeScope,
            IDbContextFactory<ConsumerParcelContext> consumerParcelDbContextFactory,
            IConsumer consumer,
            ILoggerFactory loggerFactory)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _lifetimeScope = lifetimeScope;
            _consumerParcelDbContextFactory = consumerParcelDbContextFactory;
            _consumer = consumer;
            _logger = loggerFactory.CreateLogger<ConsumerParcel>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var projector =
                new ConnectedProjector<ConsumerParcelContext>(
                    Resolve.WhenEqualToHandlerMessageType(new ParcelKafkaProjection(_lifetimeScope).Handlers));

            try
            {
                await _consumer.ConsumeContinuously(async (message, messageContext) =>
                {
                    _logger.LogInformation("Handling next message");

                    await using var context = await _consumerParcelDbContextFactory.CreateDbContextAsync(stoppingToken);
                    await projector.ProjectAsync(context, message, stoppingToken).ConfigureAwait(false);

                    //CancellationToken.None to prevent halfway consumption
                    await context.UpdateProjectionState(typeof(ConsumerParcel).FullName, messageContext.Offset, stoppingToken);
                    await context.SaveChangesAsync(CancellationToken.None);

                }, stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogCritical(exception, $"Critical error occured in {nameof(ConsumerParcel)}.");
                _hostApplicationLifetime.StopApplication();
                throw;
            }
        }
    }
}
