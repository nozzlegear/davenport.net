using System;
using System.Threading.Tasks;
using Davenport.Entities;
using Xunit;

namespace Davenport.Tests
{
    public class TestConfiguration : IAsyncLifetime
    {
        internal Client<MyTestClass> Client { get; private set; }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            var designDocs = new DesignDocConfig[]
            {
                new DesignDocConfig()
                {
                    Name = "list",
                    Views = new View[]
                    {
                        new View()
                        {
                            Name = "only-bazs-greater-than-10",
                            MapFunction = @"function (doc) {
                                if (doc.Baz > 10) {
                                    emit(doc._id, doc);
                                }
                            }",
                            ReduceFunction = "_count"
                        }
                    }
                }
            };
            var config = new Configuration("http://localhost:5984", "davenport_net");

            config.Warning += (object sender, string message) => Console.WriteLine(message);

            // Make sure the database exists
            Client = await Configuration.ConfigureDatabaseAsync<MyTestClass>(config, designDocs: designDocs);
        }
    }
}