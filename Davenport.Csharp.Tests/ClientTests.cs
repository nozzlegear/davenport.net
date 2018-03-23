using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Davenport.Entities;
using Xunit;

namespace Davenport.Tests
{
    public class ClientTests : IClassFixture<TestConfiguration>
    {
        private TestConfiguration Fixture { get; }

        MyTestClass ExampleClass = new MyTestClass()
        {
            Foo = "test value",
            Bar = false,
            Baz = 11,
            Bat = 5,
        };

        public ClientTests(TestConfiguration fixture)
        {
            this.Fixture = fixture;
        }

        [Fact(DisplayName = "Client PostAsync"), Trait("Category", "Client")]
        public async Task PostAsync()
        {
            var doc = await Fixture.Client.PostAsync(ExampleClass);

            Assert.False(string.IsNullOrEmpty(doc.Id));
            Assert.False(string.IsNullOrEmpty(doc.Rev));
            Assert.True(doc.Ok);
        }

        [Fact(DisplayName = "Client GetAsync"), Trait("Category", "Client")]
        public async Task GetAsync()
        {
            var created = await Fixture.Client.PostAsync(ExampleClass);
            var doc = await Fixture.Client.GetAsync(created.Id);

            Assert.Equal("test value", doc.Foo);
            Assert.False(doc.Bar);
            Assert.Equal(11, doc.Baz);
            Assert.True(doc.Bat.HasValue);
            Assert.Equal(5, doc.Bat);
        }

        [Fact(DisplayName = "Client CountAsync"), Trait("Category", "Client")]
        public async Task CountAsync()
        {
            await Fixture.Client.PostAsync(ExampleClass);

            var count = await Fixture.Client.CountAsync();

            Assert.True(count > 0);
        }

        [Fact(DisplayName = "Client CountByExpressionAsync"), Trait("Category", "Client")]
        public async Task CountByExpressionAsync()
        {
            var expected = "test value 2";
            var cust = new MyTestClass()
            {
                Foo = expected,
                Bar = ExampleClass.Bar,
                Baz = ExampleClass.Baz,
                Bat = ExampleClass.Bat
            };

            await Fixture.Client.PostAsync(cust);
            await Fixture.Client.PostAsync(ExampleClass);

            var count = await Fixture.Client.CountByExpressionAsync(doc => doc.Foo == expected);
            var totalCount = await Fixture.Client.CountAsync();

            Assert.True(count > 0);
            Assert.True(totalCount >= count);
        }

        [Fact(DisplayName = "Client PutAsync"), Trait("Category", "Client")]
        public async Task PutAsync()
        {
            var created = await Fixture.Client.PostAsync(ExampleClass);
            var retrieved = await Fixture.Client.GetAsync(created.Id, created.Rev);

            retrieved.Foo = "test value 2";
            retrieved.Bar = true;
            retrieved.Baz = 9;
            retrieved.Bat = null;

            var updated = await Fixture.Client.PutAsync(created.Id, retrieved, created.Rev);
            retrieved = await Fixture.Client.GetAsync(updated.Id, updated.Rev);

            Assert.Equal("test value 2", retrieved.Foo);
            Assert.True(retrieved.Bar);
            Assert.Equal(9, retrieved.Baz);
            Assert.Null(retrieved.Bat);
            Assert.False(retrieved.Bat.HasValue);
        }

        [Fact(DisplayName = "Client DeleteAsync"), Trait("Category", "Client")]
        public async Task DeleteAsync()
        {
            var created = await Fixture.Client.PostAsync(ExampleClass);

            await Fixture.Client.DeleteAsync(created.Id, created.Rev);
        }

        [Fact(DisplayName = "Client ListWithDocsAsync"), Trait("Category", "Client")]
        public async Task ListWithDocsAsync()
        {
            await Fixture.Client.PostAsync(ExampleClass);

            var list = await Fixture.Client.ListWithDocsAsync();

            Assert.NotNull(list);
            Assert.Equal(0, list.Offset);
            Assert.True(list.Rows.Count() > 0);
            Assert.True(list.DesignDocs.Count() > 0);
            Assert.True(list.Rows.All(row => row.Doc.GetType() == typeof(MyTestClass)));
            Assert.True(list.Rows.All(row => !row.Id.StartsWith("_design")));
            Assert.True(list.Rows.All(row => !string.IsNullOrEmpty(row.Doc.Id)));
            Assert.True(list.Rows.All(row => !string.IsNullOrEmpty(row.Doc.Rev)));
            Assert.True(list.DesignDocs.All(doc => doc.Id.StartsWith("_design")));
            Assert.True(list.DesignDocs.All(doc => doc.Doc != null)); ;
        }

        [Fact(DisplayName = "Client ListWithoutDocsAsync"), Trait("Category", "Client")]
        public async Task ListWithoutDocsAsync()
        {
            await Fixture.Client.PostAsync(ExampleClass);

            var list = await Fixture.Client.ListWithoutDocsAsync();

            Assert.NotNull(list);
            Assert.Equal(0, list.Offset);
            Assert.True(list.Rows.Count() > 0, "Rows.Count should be greater than 0.");
            Assert.True(list.DesignDocs.Count() > 0, "Rows.DesignDocs.Count should be greater than 0.");
            Assert.True(list.Rows.All(row => row.Doc.GetType() == typeof(Revision)), "All row docs should be of type Revision.");
            Assert.True(list.Rows.All(row => !string.IsNullOrEmpty(row.Doc.Rev)), "All rows should have a Doc.Rev value.");
            Assert.True(list.Rows.All(row => !row.Id.StartsWith("_design")), "All row ids should not start with _design.");
            Assert.True(list.DesignDocs.All(doc => doc.Id.StartsWith("_design")), "All design doc ids should start with _design.");
            Assert.True(list.DesignDocs.All(doc => doc.Doc != null), "All design doc documents should not be null."); ;
        }

        [Fact(DisplayName = "Client FindByExpressionAsync"), Trait("Category", "Client")]
        public async Task FindByExpressionAsync()
        {
            var created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value 2"
            });
            var equalsResult = await Fixture.Client.FindByExpressionAsync(doc => doc.Foo == "test value 2");

            Assert.NotNull(equalsResult);
            Assert.True(equalsResult.All(row => row.Foo == "test value 2"));
            Assert.True(equalsResult.All(row => !string.IsNullOrEmpty(row.Id)));
            Assert.True(equalsResult.All(row => !string.IsNullOrEmpty(row.Rev)));

            created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value"
            });
            var notEqualsResult = await Fixture.Client.FindByExpressionAsync(doc => doc.Foo != "test value 2");

            Assert.NotNull(notEqualsResult);
            Assert.True(notEqualsResult.All(row => row.Foo != "test value 2"));
            Assert.True(notEqualsResult.All(row => !string.IsNullOrEmpty(row.Id)));
            Assert.True(notEqualsResult.All(row => !string.IsNullOrEmpty(row.Rev)));
        }

        [Fact(DisplayName = "Client FindByDictionaryAsync"), Trait("Category", "Client")]
        public async Task FindByDictionaryAsync()
        {
            var created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value 2"
            });
            var equalsResult = await Fixture.Client.FindBySelectorAsync(new Dictionary<string, FindExpression>()
            {
                { "Foo", new FindExpression(ExpressionType.Equal, "test value 2") }
            });

            Assert.NotNull(equalsResult);
            Assert.True(equalsResult.All(row => row.Foo == "test value 2"));
            Assert.True(equalsResult.All(row => !string.IsNullOrEmpty(row.Id)));
            Assert.True(equalsResult.All(row => !string.IsNullOrEmpty(row.Rev)));

            created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value"
            });
            var notEqualsResult = await Fixture.Client.FindBySelectorAsync(new Dictionary<string, FindExpression>()
            {
                { "Foo", new FindExpression(ExpressionType.NotEqual, "test value 2") }
            });

            Assert.NotNull(notEqualsResult);
            Assert.True(notEqualsResult.All(row => row.Foo != "test value 2"));
            Assert.True(notEqualsResult.All(row => !string.IsNullOrEmpty(row.Id)));
            Assert.True(notEqualsResult.All(row => !string.IsNullOrEmpty(row.Rev)));
        }

        [Fact(DisplayName = "Client FindByObjectAsync"), Trait("Category", "Client")]
        public async Task FindByObjectAsync()
        {
            var created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value 2"
            });
            var equalsResult = await Fixture.Client.FindByObjectAsync(new
            {
                Foo = new Dictionary<string, object>
                {
                    { "$eq", "test value 2" }
                }
            });

            Assert.NotNull(equalsResult);
            Assert.True(equalsResult.All(row => row.Foo == "test value 2"));
            Assert.True(equalsResult.All(row => !string.IsNullOrEmpty(row.Id)));
            Assert.True(equalsResult.All(row => !string.IsNullOrEmpty(row.Rev)));

            created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value"
            });
            var notEqualsResult = await Fixture.Client.FindByObjectAsync(new
            {
                Foo = new Dictionary<string, object>
                {
                    { "$ne", "test value 2" }
                }
            });

            Assert.NotNull(notEqualsResult);
            Assert.True(notEqualsResult.All(row => row.Foo != "test value 2"));
            Assert.True(notEqualsResult.All(row => !string.IsNullOrEmpty(row.Id)));
            Assert.True(notEqualsResult.All(row => !string.IsNullOrEmpty(row.Rev)));
        }

        [Fact(DisplayName = "Client ExistsAsync"), Trait("Category", "Client")]
        public async Task ExistsAsync()
        {
            var created = await Fixture.Client.PostAsync(ExampleClass);
            var exists = await Fixture.Client.ExistsAsync(created.Id);
            var existsWithRev = await Fixture.Client.ExistsAsync(created.Id, created.Rev);

            Assert.True(exists);
            Assert.True(existsWithRev);
        }

        [Fact(DisplayName = "Client ExistsByExpressionAsync"), Trait("Category", "Client")]
        public async Task ExistsByExpressionAsync()
        {
            var created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value 2"
            });
            var exists = await Fixture.Client.ExistsByExpressionAsync(doc => doc.Foo == "test value 2");

            Assert.True(exists);
        }

        [Fact(DisplayName = "Client ExistsByDictionaryAsync"), Trait("Category", "Client")]
        public async Task ExistsByDictionaryAsync()
        {
            var created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value 2"
            });
            var exists = await Fixture.Client.ExistsBySelectorAsync(new Dictionary<string, FindExpression>()
            {
                { "Foo", new FindExpression(ExpressionType.Equal, "test value 2") }
            });

            Assert.True(exists);
        }

        [Fact(DisplayName = "Client ExistsByObjectAsync"), Trait("Category", "Client")]
        public async Task ExistsByObjectAsync()
        {
            var created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value 2"
            });
            var exists = await Fixture.Client.ExistsByObjectAsync(new
            {
                Foo = new Dictionary<string, object>
                {
                    { "$eq", "test value 2" }
                }
            });

            Assert.True(exists);
        }

        [Fact(DisplayName = "Client CopyAsync"), Trait("Category", "Client")]
        public async Task CopyAsync()
        {
            var uuid = $"a-unique-string-{DateTime.Now.Millisecond}";
            var createResult = await Fixture.Client.PostAsync(ExampleClass);
            var copyResult = await Fixture.Client.CopyAsync(createResult.Id, uuid);

            Assert.Equal(copyResult.Id, uuid);
        }

        [Fact(DisplayName = "Client ViewAsync"), Trait("Category", "Client")]
        public async Task ViewAsync()
        {
            var created = await Fixture.Client.PostAsync(new MyTestClass()
            {
                Foo = "test value",
                Baz = 15,
            });
            var viewResult = await Fixture.Client.ViewAsync<int>("list", "only-bazs-greater-than-10");

            Assert.True(viewResult.Count() > 0);
            Assert.True(viewResult.Sum(doc => doc.Value) > 0);
        }

        [Fact(DisplayName = "Client CreateDatabaseAsync and DeleteDatabaseAsync"), Trait("Category", "Client")]
        public async Task CreateAndDeleteDatabaseAsync()
        {
            string name = "davenport_net_delete_me";
            var newDb = new Davenport.Client<MyTestClass>("http://localhost:5984", name);
            var createResult = await newDb.CreateDatabaseAsync();

            Assert.True(createResult.Ok);

            // Create the database again to ensure .AlreadyExisted works.
            createResult = await newDb.CreateDatabaseAsync();

            Assert.True(createResult.AlreadyExisted);

            var deleteResult = await newDb.DeleteDatabaseAsync();

            Assert.True(deleteResult.Ok);
        }
    }
}