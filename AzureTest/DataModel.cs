using Microsoft.Azure.Documents.Spatial;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzureTest
{
    public class Event
    {
        [JsonProperty(propertyName:"id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Point Location { get; set; }
        public DateTime Time { get; set; }
        public Address Address { get; set; }
  //      public string PartitionKey { get; internal set; }
    }

    public class Address
    {
        public string City { get; set; }
        public string StreetName { get; set; }
        public string StreetNumber { get; set; }
    }
}
