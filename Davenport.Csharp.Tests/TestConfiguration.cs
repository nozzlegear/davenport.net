using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Davenport.Csharp;
using Davenport.Csharp.Types;
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
            var config = new Configuration("http://localhost:5984", "davenport_net");

            config.Warning += (object sender, string message) => Console.WriteLine(message);

            Client = new Client<MyTestClass>(config);

            // Make sure the database exists
            await Client.CreateDatabaseAsync();
            await Client.CreateOrUpdateDesignDocAsync("list", new List<ViewConfig>()
            {
                new ViewConfig()
                {
                    Name = "only-bazs-greater-than-10",
                    MapFunction = @"function (doc) {
                        if (doc.Baz > 10) {
                            emit(doc._id, doc);
                        }
                    }"
                }
            });
        }
    }
}