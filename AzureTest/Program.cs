using Bogus;
using Microsoft.Azure.Documents.Spatial;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AzureTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appconfig.json")
                .Build();

            var serviceProvider = new ServiceCollection()
                .Configure<CosmosDbOptions>(configuration.GetSection("CosmosDB"))
                .AddSingleton<EventRepository>()
                
                .BuildServiceProvider();


            var eventRepo = serviceProvider.GetService<EventRepository>();

            var events = await eventRepo.GetNearbyEventsPaged(new Point(28, 56), 20000, 10, 10);
            
            foreach(var e in events)
            {
                Console.WriteLine(e.Name);
            }

            Console.WriteLine("Ready");
            Console.Read();
        }

        private static IEnumerable<Event> GenerateEvents(int count)
        {
            var city = new[] { "Minsk", "NY", "Chicago" };

            var testPoint = new Faker<Point>()
                .CustomInstantiator(f => new Point(27 + f.Random.Double(-2, 2), 53 + f.Random.Double(-2, 2)));

            var testAddress = new Faker<Address>()
                .RuleFor(e => e.City, f => f.PickRandom(city))
                .RuleFor(e => e.StreetName, f => f.Address.StreetName())
                .RuleFor(e => e.StreetNumber, f => f.Address.BuildingNumber());

            var testEvents = new Faker<Event>()
                .RuleFor(e => e.Name, f => f.Random.String2(5))
                .RuleFor(e => e.Description, f => f.Random.String2(20))
                .RuleFor(e => e.Address, f => testAddress.Generate())
                .RuleFor(e => e.Time, f => f.Date.Soon())
                .RuleFor(e => e.Location, f => testPoint.Generate())
                ;

            return testEvents.Generate(count);
        }

        private static string GenerateKey(string name, string city)
        {
            return city + name.GetHashCode() % 20;
        }
    }
}
