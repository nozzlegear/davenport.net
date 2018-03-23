# Davenport.NET

Davenport is the comfiest .NET wrapper for CouchDB. Its goal is to simplify interacting with the CouchDB API by wrapping things like getting, listing, creating, updating, finding, copying and deleting documents.

|   |    |
|---|----|
|[![Build status](https://ci.appveyor.com/api/projects/status/8cy7l4f582fnuf9g/branch/master?svg=true)](https://ci.appveyor.com/project/nozzlegear/davenport-net/branch/master)|Project build status|
|[![Davenport NuGet](https://img.shields.io/nuget/v/Davenport.svg?maxAge=3600)](https://www.nuget.org/packages/Davenport/)| Davenport package|
|[![Davenport.Fsharp NuGet](https://img.shields.io/nuget/v/Davenport.Fsharp.svg?maxAge=3600)](https://www.nuget.org/packages/Davenport.Fsharp/)| Davenport.Fsharp package|
|[![license](https://img.shields.io/github/license/nozzlegear/davenport.net.svg?maxAge=3600)](https://raw.githubusercontent.com/nozzlegear/davenport.net/master/LICENSE)| License|

**Davenport and Davenport.Fsharp will soon be merged into the same Davenport package, with the C# package wrapping the F# package.**

## Installation

Davenport is published on [Nuget](https://nuget.org/packages/davenport). You can install Davenport from the dotnet command line:

```sh
dotnet add package davenport
```

Or use the Visual Studio package manager console:

```sh
install-package davenport
```

And if you're using Paket, add this to your `paket.dependencies` file:

```sh
nuget davenport
# You probably want the F# wrapper if you're using paket
nuget davenport.fsharp
```

## C\# Documentation

(Want to use Davenport with F#? [See below.](#f-documentation))

To use Davenport you'll need to create an instance of the `Client` class. Each `Client` is tied to a specific database which is more akin to a "table" in SQL terms, as each CouchDB installation can host thousands of databases; in fact, with CouchDB such a case is even encouraged with the database-per-user strategy.

To create a `Client`, you can either construct the class itself, *or* you can use `Davenport.Configuration.ConfigureDatabaseAsync` which will take care of creating the database (if it doesn't exist), creating design docs, and creating find indexes all at once. `ConfigureDatabaseAsync` will also check that your CouchDB instance is at version 2.0, which is required by the `FindBy*Async`, `CountBy*Async` and `ExistsBy*Async` methods.

```cs
// Create a Find index on the "Foo" property of documents in this database.
string[] indexes = { "Foo" };
var designDocs = new DesignDocConfig[]
{
    new DesignDocConfig()
    {
        Name = "list",
        Views = new View[]
        {
            new View()
            {
                // Name must be URL compatible, e.g. no spaces or invalid URL characters.
                Name = "only-bazs-greater-than-10",
                // Map and Reduce functions must be valid JavaScript strings.
                MapFunction = @"function (doc) {
                    if (doc.Baz > 10) {
                        emit(doc._id, doc);
                    }
                }",
                // Use the built-in _count reduce function.
                ReduceFunction = "_count"
            }
        }
    }
};

Client<DocumentType> client;

try
{
    client = await Configuration.ConfigureDatabaseAsync<DocumentType>(Config, indexes, designDocs);
}
catch (DavenportException ex)
{
    // Handle exception
}
```

It should be noted that configuring the design docs and their views is a **"dumb"** process. Davenport will check to see if the design documents you gave it match the ones that exist on your database **exactly**. That is to say, if you have a design doc named `list` on your database, and it has a view called `list-foos`, Davenport will check that the map and reduce functions match the ones you passed it *exactly, to the letter*. If not, Davenport will overwrite the view's map and reduce functions with the ones you gave it.

Davenport **does not** delete design documents that it encounters but weren't listed in your configuration. It will simply skip them.

If you don't need to configure your database or design docs from code, then you don't need to use `ConfigureDatabaseAsync` at all. You can just create a new `Client` which is immediately ready to interact with your database:

```cs
var client = new Davenport.Client<DocumentType>("http://localhost:5984", "my_database_name");
```

### `where DocumentType : CouchDoc`

CouchDB assigns all documents an `_id` and `_rev` property on every create or update call. To ensure that Davenport can actually execute requests against your database and documents, all objects sent through the `Client` **must** inherit from the `Davenport.CouchDoc` class. The `CouchDoc` class implements the `Id` and `Rev` strings for you, which are then mapped to the `_id` and `_rev` strings when sent to CouchDB.

Like many .NET packages, Davenport uses Newtonsoft.Json to handle de/serialization to and from JSON. That means your document classes can use the entire cadre of Json.Net attributes and serializers on your properties.

For example, you can use the `[JsonProperty("foo")]` attribute on a `Foo` property, which forces the property name to `foo` when sent to your database, and deserializes it back to `Foo` when returned. This is what Davenport uses internally to map `Id` and `Rev` on the `CouchDoc` class to `_id` and `_rev` in CouchDB.

```cs
public class MyClass : Davenport.CouchDoc
{
    [JsonProperty("foo")]
    public string Foo { get; set; }

    // Serializes to { "_id" : "some-id", "_rev" : "some-rev", "foo" : "value" }
}
```

**Davenport is configured to ignore null property values when serializing and deserializing**.

### Usage

Note that the code sample below does not cover all methods, just a fraction of them to show what's possible.

```cs
using Davenport;

public class MyDoc : CouchDoc
{
    string Foo { get; set; }
}

//...

// Create a client for working with the "my_database" database
var client = new Client<MyDoc>("localhost:5984", "my_database");
// Or, use an optional username and password to connect
var client = new Client<MyDoc>(new Configuration("localhost:5984", "my_database")
{
    Username = "username",
    Password = "password"
});

// Create the database if it doesn't exist
await client.CreateDatabaseAsync();

// Create a doc
var myDoc = await client.PostAsync(new MyDoc()
{
    Foo = "Hello world!"
});

// Get a doc
MyDoc doc;
try
{
    doc = await client.GetAsync(docId);
    // Or get one by a specific revision
    doc = await client.GetAsync(docId, rev);
}
catch (DavenportException e)
{
    if (e.Status == 404)
    {
        // Doc was not found
    }
    else
    {
        // Some other error
    }
}

// Find docs by the value of their Foo property
var docs = await client.FindByExprAsync(d => d.Foo == "Hello world!");
// Or use a dictionary
var docs = await client.FindBySelectorAsync(new Dictionary<string, FindExpression>()
{
    { "Foo", new FindExpression(ExpressionType.Equal, "Hello world!")}
});
```

## F\# documentation

I had previously built Davenport with an F# wrapper for the C# methods, making the package much more functional, idiomatic and easier to use with F#. However, I really wanted to make it possible to store several different object types in the same CouchDB database (as is idiomatic for most CouchDB usage). This resulted in a complete rewrite of the F# package; it no longer wraps any C# methods, nor is constrained by any C#-style thinking like it was previously. 

In fact, soon the C# client will be itself rewritten to *wrap the F# client*.

To install the F# package for Davenport, just add the following to your `paket.dependencies` file:

```sh
nuget davenport.fsharp
```

### Microsoft.FsharpLu.Json JsonConverter

**IMPORTANT**: The default `ICouchConverter` in the F# package uses the `Microsoft.FsharpLu.Json.Compact` package internally to serialize things like Options, Union Types and so-on to a friendly JSON format that can then be easily deserialized by the same converter.

To summarize, this means that some values in the CouchDB database may not be in the same format that you might expect if you had serialized them with a plain `Newtonsoft.Json.JsonConverter`, but it will look much more like "normal" json.

For example, this F# data:

```fs
type MyDoc = {
    _id: string
    _rev: string
    numbers: int option list
}

let doc = {
    _id = "someId"
    _rev = "someRev"
    numbers = [Some 5; None; Some 6]
```

Will get converted to this JSON when sent to CouchDB:

```json
{
    "_id": "someId",
    "_rev": "someRev",
    "numbers": [5, 6]
}
```

Whereas if we had used the default `Newtonsoft.Json.JsonConverter` it would have come out more like this:

```json
{
    "_id": "someId",
    "_rev": "someRev",
    "numbers": [
        {
            "Case": "Some",
            "Fields": [ 5 ]
        },
        null,
        {
            "Case": "Some",
            "Fields": [ 6 ]
        }
    ]
}
```

### Usage

Where the C# package for Davenport only supports *one* type of document stored per database (and it must extend the `CouchDoc` type), the F# package has no such constraints. It lets you pass in an (optional) map containing type names (which map to custom Id/Rev labels on your records), and then stores those type names with the document. 

When you retrieve the document you'll receive a `string option * Document` tuple -- where the `string option` is the doc's type name if it's found -- which you can use to decide how the document should be deserialized.

### General usage example

```fs

type FirstDoc = {
    Id: string
    Rev: string 
    Numbers: int option list
}

type SecondDoc = {
    DocId: string
    DocRev: string
    Hello: string
}

let docMapping: FieldMapping = 
    Map.empty 
    // Tell Davenport to map the 'first-doc' Id to _id and Rev to _rev
    |> Map.add "first-doc" ("Id", "Rev") 
    // Tell Davenport to map the 'second-doc' DocId to _id and DocRev to _rev
    |> Map.add "second-doc" ("DocId", "DocRev")

// Create a client connection
let client = 
    "localhost:5984"
    |> database "my_database" // All requests will be to "my_database"
    |> mapFields docMapping   // Tell Davenport which types have custom _id/_rev field names.
    |> username "username"    // Optionally use a username to login
    |> password "password"    // Optionally use a password to login
    |> converter someICouchConverter // Optionally use your own custom converter. 
                                     // NOTE: This must map your id and rev fields for you.

type MyDoc = 
    | First of FirstDoc
    | Second of SecondDoc

// The Insertable type tells Davenport what string to store as the `type` prop on the doc.
let insertable doc: Insertable<obj> = 
    match doc with 
    | MyDoc.First as d -> Some "first-doc", d :> obj
    | MyDoc.Second as d -> Some "second-doc", d :> obj

// Insert multiple documents in one request
let! bulkInsertResult = 
    [
        // CouchDB will fill in the Id and Rev values when the docs are created
        { Id = null; Rev = null; Numbers = [Some 5; Some 10] }
        |> MyDoc.First
        |> insertable

        { MyId = null; MyRev = null; Hello = "world" }
        |> MyDoc.Second
        |> insertable
    ]
    |> bulkInsert BulkMode.AllowNewEdits
    <| client

// Next, we'll list all of the documents that were just inserted since CouchDB doesn't 
// return the full document after insert (only Id and Rev values).

let keys = 
    bulkInsertResult
    |> List.filter (function | BulkResult.Inserted _ -> true | BulkResult.Failed _ -> false)
    |> List.map (fun (d: PostPutCopyResponse) -> d.Id)
    |> ListOption.Keys

let! listResult = 
    client
    |> listAll WithDocs [keys]

let docs = 
    listResult 
    |> List.map (fun d ->
        match d.TypeName with 
        | Some "first-doc" ->
            d.To<FirstDoc>()
            |> MyDoc.First
            |> Some
        | Some "second-doc" ->
            d.To<SecondDoc>()
            |> MyDoc.Second
            |> Some
        | _ ->
            // Unknown doc type
            None)
    |> List.filter Option.isSome

// You now have a list of MyDoc.First and MyDoc.Second!    
```

For more usage examples, check out the Davenport.Fsharp.Tests folder which contains tests for every function in the package.

## Warnings

Though rare if your database and indexes are configured properly, CouchDB may return a warning with `find` requests (and all those that use them: `countBySelector` and `existsBySelector`), particularly `find` requests that used an index that wasn't configured in your database. Instead of logging to Console.WriteLine, Davenport includes a Warning event on all Configuration objects which you can use to log the message in a way more conducive to your application.

To use it in C#:

```cs
var config = new Configuration("http://localhost:5984", "database_name");
var client = new Client<DocumentType>(config);

// Wire up the Warning event:
config.Warning += (object sender, string message) =>
{
    // Do whatever you want with the warning message.
    Console.WriteLine(message);
};
```

And to use it in F#:

```fs
let client =
    "localhost:5984"
    |> database "my_database"
    |> warning (fun message -> printfn "%s" message)
```

There are a few different events that will create a warning message:

1. (C# and F#) You executed a `Find` operation and looked up documents based on indexes (properties) that weren't configured in your database.
    - Hint: You can use `Davenport.Configuration.ConfigureDatabaseAsync` in C#, or `createIndexes` in F#. Using indexes improves the performance of any request that uses the `Find` operation (`Find`, `CountBy` and `ExistsBy`).
2. (C#) Davenport is updating or creating a design doc when configuring a database.
3. (C#) Your CouchDB installation is using a version less than 2.0.0.
    - If this is the case, you won't be able to use `Find` methods, or any other request that uses the `Find` operation (`Find`, `CountBy` and `ExistsBy`).
