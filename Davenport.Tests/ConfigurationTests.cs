using System;
using System.Threading.Tasks;
using Davenport.Entities;
using Davenport.Infrastructure;
using Xunit;

namespace Davenport.Tests
{
    public class ConfigurationTests
    {
        Configuration Config = new Configuration()
        {
            CouchUrl = "http://localhost:5984",
            DatabaseName = "davenport_net",
        };

        public ConfigurationTests()
        {
            Config.Warning += (object sender, string message) => 
            {
                Console.WriteLine(message);
            };
        }

        [Fact(DisplayName = "Config Should Configure")]
        public async Task ShouldConfigure()
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
                            Name = "only-foos-greater-than-10",
                            MapFunction = @"function (doc) {
                                if (doc.foo > 10) {
                                    emit(doc._id, doc);
                                }
                            }",
                            ReduceFunction = "_count"
                        }
                    }
                }
            };

            Client<MyTestClass> client;

            try
            {
                client = await Configuration.ConfigureDatabaseAsync<MyTestClass>(Config, new string[] { "Foo" }, designDocs);
            }
            catch (DavenportException ex)
            {
                Console.WriteLine(ex.ResponseBody);

                throw;
            }

            Assert.True(client != null);
        }
    }
}