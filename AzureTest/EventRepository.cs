using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Documents.Spatial;
using Microsoft.Extensions.Options;


namespace AzureTest
{
    public class EventRepository
    {
        private const string databaseId = "EventDb";
        private const string collectionId = "EventCollection";

        private readonly Uri eventCollectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);

        private readonly DocumentClient client;

        public EventRepository(IOptions<CosmosDbOptions> options)
        {
            client = new DocumentClient(new Uri(options.Value.EndpointUri), options.Value.PrimaryKey, new ConnectionPolicy
            {
    //            ConnectionMode = ConnectionMode.Direct,
    //            ConnectionProtocol = Protocol.Tcp,
                MaxConnectionLimit = 200
            });

            Initialize(client).Wait();
        }

        private async Task Initialize(DocumentClient client)
        {
            var db = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseId });

            var documentCollection = new DocumentCollection { Id = collectionId };

            documentCollection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });
            documentCollection.IndexingPolicy.IncludedPaths.Add(new IncludedPath
            {
                Path = "/Location/?",
                Indexes = new Collection<Index> { new SpatialIndex(DataType.Point) }
            });
            documentCollection.IndexingPolicy.IncludedPaths.Add(new IncludedPath
            {
                Path = "/Time/?",
                Indexes = new Collection<Index> { new RangeIndex(DataType.String) { Precision = -1 } }
            });

            documentCollection.DefaultTimeToLive = 86400;

            await client.CreateDocumentCollectionIfNotExistsAsync(db.Resource.SelfLink , documentCollection, 
                new RequestOptions {
                    OfferThroughput = 10000
                });
        }


        public async Task CreateEvent(Event eventData)
        {
            await client.CreateDocumentAsync(eventCollectionLink, eventData);
        }

        public async Task<IEnumerable<Event>> GetNearbyEvents(Point point, double distance)
        {
            var query = client.CreateDocumentQuery<Event>(eventCollectionLink, new FeedOptions
            {
                MaxItemCount = -1
            }).Where(e => e.Location.Distance(point) < distance)
            .AsDocumentQuery();

            return await query.ExecuteNextAsync<Event>();
        }

        public async Task<IEnumerable<Event>> GetNearbyEventsPaged(Point point, double distance, int pageSize, int pageNumber)
        {
            return await GetPaged<Event>(q => q.Where(e => e.Location.Distance(point) < distance), pageSize, pageNumber);
        }

        private async Task<IEnumerable<T>> GetPaged<T>(Func<IOrderedQueryable<T>, IQueryable<T>> filter, int pageSize, int pageNumber)
        {
            var continuation = await GetPageContinuation(filter, pageSize, pageNumber);

            if (continuation == null)
            {
                return new T[0];
            }

            var query = filter(client.CreateDocumentQuery<T>(eventCollectionLink,
                                                                    new FeedOptions
                                                                    {
                                                                        MaxItemCount = pageSize,
                                                                        RequestContinuation = continuation
                                                                    }))
                    .AsDocumentQuery();
            return await query.ExecuteNextAsync<T>();

        }

        private async Task<string> GetPageContinuation<T>(Func<IOrderedQueryable<T>, IQueryable<T>> filter, int pageSize, int pageNumber)
        {
            string requestContinuation = string.Empty;
            int responseCount = 0;
            var currentPage = 0;
            while (currentPage < pageNumber && (string.IsNullOrEmpty(requestContinuation) || responseCount == pageSize))
            {
                var query = filter(client.CreateDocumentQuery<T>(eventCollectionLink,
                                                                    new FeedOptions
                                                                    {
                                                                        MaxItemCount = pageSize,
                                                                        RequestContinuation = requestContinuation
                                                                    }))
                    .Select(i => true)
                    .AsDocumentQuery();
                var resp = await query.ExecuteNextAsync<bool>();

                if (++currentPage == pageNumber)
                {
                    return requestContinuation;
                }

                requestContinuation = resp.ResponseContinuation;
                responseCount = resp.Count;
            }
            return null;
        }

        public async Task IncrementVisitors(Event e)
        {
            while(true)
            {
                var response = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, e.Id));
                var document = response.Resource;

                var accessCondition = new AccessCondition
                {
                    Condition = document.ETag,
                    Type = AccessConditionType.IfMatch
                };

                const string VistorsProperty = "Visitors";
                var counter = document.GetPropertyValue<int?>(VistorsProperty) ?? 0;
                document.SetPropertyValue(VistorsProperty, ++counter);

                try
                {
                    await client.ReplaceDocumentAsync(document, new RequestOptions { AccessCondition = accessCondition });
                    break;
                }
                catch (DocumentClientException dce)
                {
                    if (dce.StatusCode == HttpStatusCode.PreconditionFailed)
                    {
                        continue;
                    }
                    throw;
                }
            }
        }

        public async Task ResetVisitors(Event e )
        {
            var resp = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, collectionId, e.Id));
            var document = resp.Resource;
            document.SetPropertyValue("Visitors", 0);
            await client.ReplaceDocumentAsync(document);
        }
    }

    public class CosmosDbOptions
    {
        public string EndpointUri { get; set; }
        public string PrimaryKey { get; set; }
    }
}
