using System;
using System.Threading.Tasks;
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
            Client = new Client<MyTestClass>(config);

            config.Warning += (object sender, string message) => Console.WriteLine(message);

            // Make sure the database exists
            await Client.CreateDatabaseAsync();
        }
    }
}